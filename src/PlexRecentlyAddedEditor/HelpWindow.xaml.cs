using System.Windows;
using System.Windows.Navigation;
using PlexRecentlyAddedEditor.Services;

namespace PlexRecentlyAddedEditor;

public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        BrowserLauncher.Open(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private void CopyPlexLink_Click(object sender, RoutedEventArgs e) =>
        CopyToClipboard("https://support.plex.tv/articles/204059436-finding-an-authentication-token-x-plex-token/");

    private void CopyTmdbLink_Click(object sender, RoutedEventArgs e) =>
        CopyToClipboard("https://www.themoviedb.org/settings/api");

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void CopyToClipboard(string text)
    {
        try
        {
            Clipboard.SetText(text);
        }
        catch
        {
            // Clipboard access can transiently fail on Windows if another app has it locked;
            // not worth surfacing an error dialog for a convenience copy button.
        }
    }
}
