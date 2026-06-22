using System.Windows.Controls;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Logging dockable tool window. Its DataContext is supplied by the shell as a
    /// <see cref="ViewModels.LogToolWindowViewModel"/>, so all bindings are relative to that view model.
    /// </summary>
    internal partial class LogToolView : UserControl
    {
        public LogToolView()
        {
            InitializeComponent();
        }

        // Keep the newest log line in view as text streams in.
        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
                textBox.ScrollToEnd();
        }
    }
}
