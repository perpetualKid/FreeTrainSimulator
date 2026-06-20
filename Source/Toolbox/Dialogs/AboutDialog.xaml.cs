using System.Windows;

using FreeTrainSimulator.Common.Info;

namespace FreeTrainSimulator.Toolbox.Dialogs
{
    /// <summary>
    /// WPF modal dialog showing the application name and version, replacing the legacy MonoGame
    /// <c>AboutWindow</c> popup in hosted mode. Mirrors what the legacy popup displayed.
    /// </summary>
    public partial class AboutDialog : Window
    {
        internal AboutDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>Display name of the application.</summary>
        public static string ApplicationName => RuntimeInfo.ApplicationName;

        /// <summary>Full version string, prefixed with 'v' to match the legacy popup.</summary>
        public static string Version => $"v{VersionInfo.FullVersion}";

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
