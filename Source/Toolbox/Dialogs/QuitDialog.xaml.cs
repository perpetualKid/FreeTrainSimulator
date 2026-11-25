using System;
using System.Windows;

using FreeTrainSimulator.Common.Info;

using GetText;

namespace FreeTrainSimulator.Toolbox.Dialogs
{
    /// <summary>
    /// WPF modal exit-confirmation dialog, replacing the legacy MonoGame <c>QuitWindow</c> popup in hosted
    /// mode. Using a WPF window (rather than a Win32 <c>MessageBox</c>) lets it reliably center on the owner
    /// window via <c>WindowStartupLocation="CenterOwner"</c>, including under per-monitor DPI.
    /// </summary>
    public partial class QuitDialog : Window
    {
        internal QuitDialog()
            : this(false)
        {
        }

        internal QuitDialog(bool hasUnsavedPathChanges)
        {
            InitializeComponent();
            Catalog catalog = CatalogManager.Catalog;
            Message = RuntimeInfo.ApplicationName is null
                ? catalog.GetString("Exit?")
                : catalog.GetString("Exit {0}?", RuntimeInfo.ApplicationName);
            if (hasUnsavedPathChanges)
                Message += Environment.NewLine + Environment.NewLine
                    + catalog.GetString("There are unsaved path changes. If you exit now, those changes will be lost.");
            DataContext = this;
        }

        /// <summary>
        /// Confirmation prompt shown to the user. Built from the shared gettext catalog so the message is
        /// localized; the binding is data-bound, so the WPF Localizer skips it and the catalog lookup here is
        /// the single localization point.
        /// </summary>
        public string Message { get; }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
