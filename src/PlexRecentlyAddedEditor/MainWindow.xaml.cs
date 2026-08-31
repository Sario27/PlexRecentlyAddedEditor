using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using PlexRecentlyAddedEditor.Models;
using PlexRecentlyAddedEditor.Services;

namespace PlexRecentlyAddedEditor;

public partial class MainWindow : Window
{
    private readonly ScanService _scanService = new();
    private readonly PlexAuthService _authService = new();
    private readonly ObservableCollection<RecentlyAddedRow> _rows = new();
    private List<PlexSection> _sections = new();

    public MainWindow()
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
        ResultsGrid.ItemsSource = _rows;

        var localToken = PlexAuthService.TryReadLocalServerToken();
        if (!string.IsNullOrEmpty(localToken))
        {
            TokenBox.Password = localToken;
            StatusText.Text = "Found a Plex token on this machine - review and click Connect.";
        }
    }

    private void DetectTokenButton_Click(object sender, RoutedEventArgs e)
    {
        var token = PlexAuthService.TryReadLocalServerToken();
        if (token is null)
        {
            MessageBox.Show(this,
                "Couldn't find a local Plex Media Server install with a signed-in token in the usual places.\n\n" +
                "If your server's data directory was moved to another drive or folder, try \"Browse for " +
                "Preferences.xml...\" instead, or use \"Sign in with Plex\" / \"How do I get these?\" for other options.",
                "No local token found", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        TokenBox.Password = token;
    }

    private void BrowseTokenButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Locate your Plex Media Server's Preferences.xml",
            Filter = "Preferences.xml|Preferences.xml|All files (*.*)|*.*",
            FileName = "Preferences.xml"
        };
        if (dialog.ShowDialog(this) != true) return;

        var token = PlexAuthService.TryReadTokenFromFile(dialog.FileName);
        if (token is null)
        {
            MessageBox.Show(this,
                "That file didn't contain a PlexOnlineToken value. Make sure your server is signed in " +
                "to plex.tv, or try \"Sign in with Plex\" instead.",
                "No token found in that file", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        TokenBox.Password = token;
        StatusText.Text = "Token loaded from the file you selected.";
    }

    private void HelpButton_Click(object sender, RoutedEventArgs e)
    {
        var help = new HelpWindow { Owner = this };
        help.ShowDialog();
    }

    private void GetTmdbKeyButton_Click(object sender, RoutedEventArgs e)
    {
        // TMDb has no PIN-style sign-in like Plex - an API key is just a static value
        // generated on your account's settings page. This link takes you straight there
        // (it'll prompt you to sign in first if you aren't already).
        BrowserLauncher.Open("https://www.themoviedb.org/settings/api");
    }

    private void AboutButton_Click(object sender, RoutedEventArgs e)
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private async void SignInButton_Click(object sender, RoutedEventArgs e)
    {
        SignInButton.IsEnabled = false;
        StatusText.Text = "Opening your browser to sign in to Plex...";
        try
        {
            var token = await _authService.SignInWithBrowserAsync();
            if (token is null)
            {
                StatusText.Text = "Sign-in timed out or was not completed.";
                return;
            }
            TokenBox.Password = token;
            StatusText.Text = "Signed in - click Connect.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Sign-in failed: {ex.Message}", "Sign-in failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var serverUrl = ServerUrlBox.Text.Trim();
        var token = TokenBox.Password.Trim();

        if (string.IsNullOrWhiteSpace(serverUrl) || string.IsNullOrWhiteSpace(token))
        {
            MessageBox.Show(this, "Please enter a server URL and a Plex token.", "Missing info", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        ConnectButton.IsEnabled = false;
        StatusText.Text = "Connecting...";
        try
        {
            var plex = new PlexClient(serverUrl, token);
            _sections = await plex.GetSectionsAsync();

            LibraryList.Items.Clear();
            foreach (var section in _sections)
            {
                LibraryList.Items.Add(new System.Windows.Controls.CheckBox
                {
                    Content = $"{section.Title} ({section.Type})",
                    Tag = section,
                    IsChecked = true
                });
            }

            LibraryGroup.IsEnabled = true;
            StatusText.Text = $"Connected. Found {_sections.Count} movie/show libraries.";

            var tmdbKey = TmdbKeyBox.Password.Trim();
            if (!string.IsNullOrWhiteSpace(tmdbKey))
            {
                var tmdbOk = await new TmdbClient(tmdbKey).ValidateApiKeyAsync();
                StatusText.Text += tmdbOk
                    ? " TMDb key looks valid."
                    : " TMDb key could not be validated - check it or generate a new one.";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Could not connect: {ex.Message}", "Connection failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Connection failed.";
        }
        finally
        {
            ConnectButton.IsEnabled = true;
        }
    }

    private List<PlexSection> GetSelectedSections() =>
        LibraryList.Items.Cast<System.Windows.Controls.CheckBox>()
            .Where(cb => cb.IsChecked == true)
            .Select(cb => (PlexSection)cb.Tag)
            .ToList();

    private async void ScanButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedSections = GetSelectedSections();
        if (selectedSections.Count == 0)
        {
            MessageBox.Show(this, "Choose at least one library.", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (!int.TryParse(ItemsPerLibraryBox.Text, out var count) || count <= 0)
        {
            MessageBox.Show(this, "Enter a positive number of items per library.", "Invalid value", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var tmdbKey = TmdbKeyBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(tmdbKey))
        {
            var result = MessageBox.Show(this,
                "No TMDb API key entered - suggested dates won't be filled in automatically, but you can still type dates in by hand.\n\nContinue anyway?",
                "No TMDb key", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
        }

        var options = new ScanOptions
        {
            ServerUrl = ServerUrlBox.Text.Trim(),
            PlexToken = TokenBox.Password.Trim(),
            TmdbApiKey = tmdbKey,
            SectionsToScan = selectedSections,
            ItemsPerLibrary = count
        };

        SetBusy(true);
        var progress = new Progress<ScanProgress>(p =>
        {
            StatusText.Text = p.Status;
            if (p.Total > 0)
            {
                ScanProgressBar.IsIndeterminate = false;
                ScanProgressBar.Maximum = p.Total;
                ScanProgressBar.Value = p.Current;
            }
            else
            {
                ScanProgressBar.IsIndeterminate = true;
            }
        });

        try
        {
            var rows = await _scanService.LoadRecentlyAddedAsync(options, progress);
            _rows.Clear();
            foreach (var row in rows) _rows.Add(row);

            ResultsSummaryText.Text = $"{_rows.Count} items loaded. Select the ones you want to change (click, Ctrl+click, or Shift+click), edit the date if needed, then pick an action below.";
            UpdateButton.IsEnabled = _rows.Count > 0;
            ExportSelectedButton.IsEnabled = _rows.Count > 0;
            ExportAllButton.IsEnabled = _rows.Count > 0;
            StatusText.Text = "Done.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Scan failed: {ex.Message}", "Scan failed", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusText.Text = "Scan failed.";
        }
        finally
        {
            SetBusy(false);
        }
    }

    private List<RecentlyAddedRow> GetSelectedRows() =>
        ResultsGrid.SelectedItems.Cast<RecentlyAddedRow>().ToList();

    private void SelectAllButton_Click(object sender, RoutedEventArgs e) => ResultsGrid.SelectAll();

    private void SelectNoneButton_Click(object sender, RoutedEventArgs e) => ResultsGrid.UnselectAll();

    private async void UpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedRows();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Select at least one row first (click, Ctrl+click, or Shift+click).", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var badDates = selected.Where(r => !DateOnly.TryParse(r.NewDateText, CultureInfo.InvariantCulture, out _)).ToList();
        if (badDates.Count > 0)
        {
            MessageBox.Show(this,
                $"{badDates.Count} selected item(s) have an invalid or empty date (expected yyyy-MM-dd). Fix those or deselect them first.",
                "Invalid dates", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(this,
            $"This will update the \"date added\" for {selected.Count} item(s) on your Plex server. Continue?",
            "Confirm changes", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        var options = new ScanOptions
        {
            ServerUrl = ServerUrlBox.Text.Trim(),
            PlexToken = TokenBox.Password.Trim(),
            TmdbApiKey = TmdbKeyBox.Password.Trim(),
            SectionsToScan = _sections
        };

        SetBusy(true);
        var progress = new Progress<ScanProgress>(p =>
        {
            StatusText.Text = p.Status;
            ScanProgressBar.IsIndeterminate = false;
            ScanProgressBar.Maximum = Math.Max(p.Total, 1);
            ScanProgressBar.Value = p.Current;
        });

        try
        {
            await _scanService.ApplySelectedAsync(options, selected, progress);
            var reportPath = ReportService.WriteSyncReport(selected);
            var succeeded = selected.Count(r => r.Applied);
            var failed = selected.Count - succeeded;

            // A row that was successfully updated no longer reflects reality (its
            // CurrentAdded/Hint are now stale), so drop it from the list instead of
            // making the user reload to see it disappear. Failed rows stay so they
            // can be fixed and retried.
            foreach (var row in selected.Where(r => r.Applied).ToList())
                _rows.Remove(row);
            ResultsSummaryText.Text = $"{_rows.Count} items loaded. Select the ones you want to change (click, Ctrl+click, or Shift+click), edit the date if needed, then pick an action below.";
            StatusText.Text = $"Done. {succeeded} updated, {failed} failed.";
            MessageBox.Show(this,
                $"Updated {succeeded} item(s). {(failed > 0 ? $"{failed} failed - see the report for details.\n\n" : "")}Report saved to:\n{reportPath}",
                "Update complete", MessageBoxButton.OK, failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"Update failed: {ex.Message}", "Update failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ExportAllButton_Click(object sender, RoutedEventArgs e)
    {
        var path = ReportService.WriteCsv(_rows.ToList(), "plex-recently-added-all");
        StatusText.Text = $"Saved CSV to {path}";
        MessageBox.Show(this, $"CSV saved to:\n{path}\n\nNo changes were made to Plex.", "CSV saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void ExportSelectedButton_Click(object sender, RoutedEventArgs e)
    {
        var selected = GetSelectedRows();
        if (selected.Count == 0)
        {
            MessageBox.Show(this, "Select at least one row first (click, Ctrl+click, or Shift+click).", "Nothing selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var path = ReportService.WriteCsv(selected, "plex-recently-added-selected");
        StatusText.Text = $"Saved CSV to {path}";
        MessageBox.Show(this, $"CSV saved to:\n{path}\n\nNo changes were made to Plex.", "CSV saved", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void SetBusy(bool busy)
    {
        LibraryGroup.IsEnabled = !busy;
        ScanButton.IsEnabled = !busy;
        UpdateButton.IsEnabled = !busy && _rows.Count > 0;
        ExportSelectedButton.IsEnabled = !busy && _rows.Count > 0;
        ExportAllButton.IsEnabled = !busy && _rows.Count > 0;
        ScanProgressBar.IsIndeterminate = busy;
        if (!busy)
        {
            ScanProgressBar.IsIndeterminate = false;
            ScanProgressBar.Value = 0;
        }
    }
}
