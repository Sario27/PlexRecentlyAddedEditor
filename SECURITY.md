# Security & Trust

This app reads from and writes to your Plex library, so it's fair to ask exactly what it
does with your data and what it talks to. Here's the whole picture.

## No telemetry. No phone-home.

This app collects, transmits, and stores nothing outside your own computer and the services
you explicitly connect it to. There is no analytics, no telemetry, no crash reporting, and no
usage tracking of any kind, and it never contacts the developer or any third party on its own.

### What each component talks to

| Component | Destination | When | Purpose |
|---|---|---|---|
| Plex client | The server URL you enter | Always, once you click Connect | Read library/recently-added data, write corrected added-dates |
| Plex sign-in | `plex.tv` | Only if you click "Sign in with Plex" | Plex's own official sign-in service; the app never sees your Plex password |
| TMDb client | `api.themoviedb.org` | Only if you provide a TMDb API key | Look up a title's release date or an episode's air date |

No other endpoint is ever contacted. There is no bundled analytics SDK, ad SDK, or update
pinger of any kind.

### Where your credentials live

- Your Plex token and TMDb API key are held in memory for the current session only. Neither
  is written to disk by this app, and neither is logged anywhere.
- The one thing this app writes locally on its own is a random client identifier (a GUID, not
  a secret) used to identify this app instance during the Plex sign-in flow, stored in a small
  text file in your system's temp folder. It's meaningless without your active approval in the
  Plex sign-in flow.
- CSV and report files are written only to your local Desktop, only when you click "Export" or
  "Update", and only ever contain what's already visible on screen.

## How it's built

The full source is in this repository - nothing is hidden or obfuscated. You can read every
network call this app makes (there are exactly three `HttpClient`/`Process.Start` call sites
in `Services/`, matching the table above), or build it yourself from source instead of using a
downloaded binary:

```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## About the release build

The released `.exe` is **not code-signed** - a signing certificate is a recurring paid cost
that doesn't make sense for a small open-source utility. This means Windows SmartScreen will
likely show an "unrecognized app" warning the first time you run a downloaded release, which
is normal for indie open-source Windows tools and not a sign of tampering. If you'd rather not
take that on faith, build it yourself with the command above - the source is right here.

## Reporting a vulnerability

If you find a security issue, please open a GitHub issue on this repository, or use GitHub's
private vulnerability reporting (Security tab → "Report a vulnerability") if you'd rather not
disclose it publicly first. Include enough detail to reproduce it. This is a small
community-maintained project rather than a company with an SLA, but reports will be looked at
and taken seriously.
