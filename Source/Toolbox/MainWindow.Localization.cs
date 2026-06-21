using System;
using System.Diagnostics;
using System.Globalization;
using System.Threading;

using GetText;
using GetText.Wpf;

namespace FreeTrainSimulator.Toolbox
{
    // WPF-side localization coordinator. The hosted game thread owns the gettext catalog and raises
    // GameWindow.LanguageChanged (initial load and every later switch); the host control re-raises it on the
    // WPF dispatcher as MapHost.LanguageChanged. This partial reacts to it by re-localizing the persistent
    // shell against the same shared catalog.
    //
    // Unlike fresh modal dialogs (which can use the {gt:Gettext} markup extension), the long-lived MainWindow
    // must support runtime language switching. WPF markup extensions resolve only once at parse time, so the
    // shell relies on Localizer.Revert/Localize: it restores the original (source) strings, then re-translates
    // every non-bound Text/Title/Content/Header/ToolTip in the visual/logical tree. Data-bound properties
    // (e.g. the dynamic status-bar fields) are intentionally skipped by the localizer and stay dynamic.
    public partial class MainWindow
    {
        // Remembers the original (pre-translation) string values so a later language switch can revert before
        // re-localizing. This is the GetText.Wpf store for the WPF shell tree; the hosted MonoGame host form
        // has no localizable WinForms controls, so it needs no separate WinForms localization store.
        private readonly ObjectPropertiesStore wpfLocalizationStore = new ObjectPropertiesStore();

        private void MapHost_LanguageChanged(object sender, EventArgs e)
        {
            ApplyWpfLanguage(MapHost.CurrentLanguage);
        }

        // Sets the UI thread's culture to match the hosted game's active language, refreshes the shared
        // catalog, points the XAML markup extension/converter at it, and re-localizes the shell tree. Safe to
        // call repeatedly; the first call simply stores originals and translates, later calls revert first.
        private void ApplyWpfLanguage(string language)
        {
            CultureInfo culture = ResolveCulture(language);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;

            // Restore the source strings captured on the previous pass before switching catalogs, so we never
            // translate an already-translated value.
            Localizer.Revert(this, wpfLocalizationStore);
            CatalogManager.Reset();

            Catalog catalog = CatalogManager.Catalog;

            // Drives the {gt:Gettext} markup extension and CatalogConverter used by the modal dialogs.
            GettextExtension.DefaultCatalog = catalog;

            Localizer.Localize(this, catalog, wpfLocalizationStore);
        }

        private static CultureInfo ResolveCulture(string language)
        {
            if (string.IsNullOrEmpty(language))
                return CultureInfo.InstalledUICulture;

            try
            {
                return new CultureInfo(language);
            }
            catch (CultureNotFoundException exception)
            {
                Trace.WriteLine(exception.Message);
                return CultureInfo.InstalledUICulture;
            }
        }
    }
}
