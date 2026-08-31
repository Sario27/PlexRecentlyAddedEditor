using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Xml;

namespace PlexRecentlyAddedEditor.Services;

/// Handles the two ways this app can get a Plex token without the user pasting one in:
/// reading it straight off a local Plex Media Server install, or the standard
/// plex.tv PIN sign-in flow used by most third-party Plex apps.
public sealed class PlexAuthService
{
    private const string ClientIdentifierFileName = "plex-recently-added-editor-client-id.txt";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };

    /// Tries to read PlexOnlineToken out of the local Plex Media Server's Preferences.xml,
    /// checking the handful of places it lives by default. Only works when this app runs
    /// on the same machine as the server and the server owner is signed in there - which
    /// is the common case for a home server admin. Many admins relocate the whole data
    /// directory to another drive though (to keep it off C:), which this can't guess -
    /// TryReadTokenFromFile lets the user just point at their Preferences.xml directly.
    public static string? TryReadLocalServerToken()
    {
        try
        {
            var candidatePaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Plex Media Server", "Preferences.xml"),
                Environment.ExpandEnvironmentVariables(
                    @"%ProgramData%\Plex Media Server\Preferences.xml")
            };

            foreach (var path in candidatePaths)
            {
                var token = TryReadTokenFromFile(path);
                if (token is not null) return token;
            }
        }
        catch
        {
            // Not fatal - the user can still browse for the file, sign in via the
            // browser flow, or paste a token manually.
        }
        return null;
    }

    /// Reads PlexOnlineToken from a specific Preferences.xml path. Used both by the
    /// auto-detect above and by the "browse for my Preferences.xml" fallback in the UI,
    /// for setups where the Plex data directory has been moved off its default location.
    public static string? TryReadTokenFromFile(string preferencesXmlPath)
    {
        try
        {
            if (!File.Exists(preferencesXmlPath)) return null;
            var doc = new XmlDocument();
            doc.Load(preferencesXmlPath);
            var token = doc.SelectSingleNode("/Preferences")?.Attributes?["PlexOnlineToken"]?.Value;
            return string.IsNullOrWhiteSpace(token) ? null : token;
        }
        catch
        {
            return null;
        }
    }

    private static string GetOrCreateClientIdentifier()
    {
        var path = Path.Combine(Path.GetTempPath(), ClientIdentifierFileName);
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (!string.IsNullOrEmpty(existing)) return existing;
        }
        var id = Guid.NewGuid().ToString("N");
        File.WriteAllText(path, id);
        return id;
    }

    private sealed class PinResponse
    {
        [JsonPropertyName("id")] public long Id { get; set; }
        [JsonPropertyName("code")] public string Code { get; set; } = "";
        [JsonPropertyName("authToken")] public string? AuthToken { get; set; }
    }

    /// Starts the plex.tv PIN sign-in flow: creates a pin, opens the user's browser to
    /// approve it, then polls until they do (or the timeout elapses). Returns the token,
    /// or null if the user never completes sign-in.
    public async Task<string?> SignInWithBrowserAsync(CancellationToken ct = default)
    {
        var clientId = GetOrCreateClientIdentifier();

        using var createReq = new HttpRequestMessage(HttpMethod.Post, "https://plex.tv/api/v2/pins");
        createReq.Headers.Add("X-Plex-Product", "Plex Recently Added Editor");
        createReq.Headers.Add("X-Plex-Client-Identifier", clientId);
        createReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        createReq.Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["strong"] = "true" });

        using var createResp = await Http.SendAsync(createReq, ct);
        createResp.EnsureSuccessStatusCode();
        var pin = await createResp.Content.ReadFromJsonAsync<PinResponse>(cancellationToken: ct)
                  ?? throw new InvalidOperationException("Plex did not return a pin.");

        var authUrl = "https://app.plex.tv/auth#?" +
                      $"clientID={Uri.EscapeDataString(clientId)}" +
                      $"&code={Uri.EscapeDataString(pin.Code)}" +
                      "&context%5Bdevice%5D%5Bproduct%5D=" + Uri.EscapeDataString("Plex Recently Added Editor");

        Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

        var deadline = DateTime.UtcNow.AddMinutes(3);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), ct);

            using var pollReq = new HttpRequestMessage(HttpMethod.Get,
                $"https://plex.tv/api/v2/pins/{pin.Id}?X-Plex-Client-Identifier={Uri.EscapeDataString(clientId)}");
            pollReq.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var pollResp = await Http.SendAsync(pollReq, ct);
            if (!pollResp.IsSuccessStatusCode) continue;

            var polled = await pollResp.Content.ReadFromJsonAsync<PinResponse>(cancellationToken: ct);
            if (!string.IsNullOrEmpty(polled?.AuthToken)) return polled.AuthToken;
        }
        return null;
    }
}
