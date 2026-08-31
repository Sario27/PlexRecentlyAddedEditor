using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using PlexRecentlyAddedEditor.Services;

namespace PlexRecentlyAddedEditor;

public partial class AboutWindow : Window
{
    private const string RepoUrl = "https://github.com/Sario27/PlexRecentlyAddedEditor";
    private const string SecurityUrl = "https://github.com/Sario27/PlexRecentlyAddedEditor/blob/main/SECURITY.md";

    public AboutWindow()
    {
        InitializeComponent();
        DarkTitleBar.Apply(this);

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionParagraph.Inlines.Add(new Run($"Version {version?.ToString(3) ?? "dev"}"));

        RepoLink.NavigateUri = new Uri(RepoUrl);
        SecurityLink.NavigateUri = new Uri(SecurityUrl);
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        BrowserLauncher.Open(e.Uri.AbsoluteUri);
        e.Handled = true;
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
