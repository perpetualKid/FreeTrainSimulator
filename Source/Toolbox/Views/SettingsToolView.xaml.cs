using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

using GetText;

namespace FreeTrainSimulator.Toolbox.Views
{
    /// <summary>
    /// Designable view for the Settings dockable tool window. Its
    /// DataContext is supplied by the shell as a <see cref="ViewModels.SettingsToolWindowViewModel"/>, so all
    /// bindings are relative to that view model. The Reset button binds to the view model's
    /// <c>ResetCommand</c>, which the shell assigns so the reset works whether the pane is docked or floating.
    /// </summary>
    internal partial class SettingsToolView : UserControl
    {
        private bool profileOperationInProgress;

        public SettingsToolView()
        {
            InitializeComponent();
            DataContextChanged += SettingsToolView_DataContextChanged;
        }

        private ManageProfilesViewModel ProfileManagement => (DataContext as SettingsToolWindowViewModel)?.ProfileManagement;

        internal void ShowProfiles() => ProfilesTab.IsSelected = true;

        private async void SettingsToolView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ProfilesTab.IsSelected && !ReferenceEquals((e.OldValue as SettingsToolWindowViewModel)?.ProfileManagement, ProfileManagement))
                await RefreshProfilesAsync().ConfigureAwait(true);
        }

        private async void SettingsTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ReferenceEquals(e.Source, sender) && ProfilesTab?.IsSelected == true)
                await RefreshProfilesAsync().ConfigureAwait(true);
        }

        private async Task RefreshProfilesAsync()
        {
            ManageProfilesViewModel profiles = ProfileManagement;
            if (profiles == null || profileOperationInProgress)
                return;

            SetProfileOperationInProgress(true);
            try
            {
                await profiles.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
                ProfilesList.ItemsSource = profiles.ProfileNames;
            }
            catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
            {
                Trace.TraceError($"Failed to list Toolbox profiles: {failure}");
                ProfileStatus.Text = CatalogManager.Catalog.GetString("Could not load profiles: {0}", failure.Message);
            }
            finally
            {
                SetProfileOperationInProgress(false);
            }
        }

        private async void CreateProfileButton_Click(object sender, RoutedEventArgs e) => await CreateProfileAsync(null).ConfigureAwait(true);

        private async void DuplicateProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesList.SelectedItem is string sourceName)
                await CreateProfileAsync(sourceName).ConfigureAwait(true);
        }

        private async Task CreateProfileAsync(string sourceName)
        {
            ManageProfilesViewModel profiles = ProfileManagement;
            if (profiles?.CanCreate != true || profileOperationInProgress)
                return;

            SetProfileOperationInProgress(true);
            try
            {
                string destination = ProfileNameInput.Text.Trim();
                string error = await profiles.CreateAsync(destination, sourceName, CancellationToken.None).ConfigureAwait(true);
                ProfilesList.ItemsSource = profiles.ProfileNames;
                ProfileStatus.Text = error ?? CatalogManager.Catalog.GetString("Profile created. The active profile has not changed.");
                if (error == null)
                {
                    ProfilesList.SelectedItem = destination;
                    ProfileNameInput.Clear();
                }
            }
            catch (Exception failure) when (FreeTrainSimulator.Common.CleanupExceptionPolicy.CanContinue(failure))
            {
                Trace.TraceError($"Failed to create a Toolbox profile: {failure}");
                ProfileStatus.Text = CatalogManager.Catalog.GetString("Could not create the profile: {0}", failure.Message);
            }
            finally
            {
                SetProfileOperationInProgress(false);
            }
        }

        private async void UseProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (profileOperationInProgress || ProfileManagement is not { CanCreate: true, UseProfileAsync: not null } profiles
                || ProfilesList.SelectedItem is not string name || !profiles.IsOtherProfile(name))
                return;

            SetProfileOperationInProgress(true);
            try
            {
                await profiles.UseProfileAsync(new FreeTrainSimulator.Models.Settings.ProfileModel(name)).ConfigureAwait(true);
            }
            finally
            {
                SetProfileOperationInProgress(false);
            }
        }

        private void ProfilesList_SelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateProfileButtons();

        private void SetProfileOperationInProgress(bool value)
        {
            profileOperationInProgress = value;
            UpdateProfileButtons();
        }

        private void UpdateProfileButtons()
        {
            if (ProfilesList == null)
                return;
            ManageProfilesViewModel profiles = ProfileManagement;
            ProfileNameInput.IsEnabled = !profileOperationInProgress && profiles?.CanCreate == true;
            CreateProfileButton.IsEnabled = ProfileNameInput.IsEnabled;
            string selected = ProfilesList.SelectedItem as string;
            DuplicateProfileButton.IsEnabled = CreateProfileButton.IsEnabled && selected != null && profiles.CaptureActiveConfigurationAsync != null;
            UseProfileButton.IsEnabled = CreateProfileButton.IsEnabled && profiles.UseProfileAsync != null && profiles.IsOtherProfile(selected);
        }

        // The language/color ComboBoxes bind SelectedItem OneWay and forward user picks here; see
        // ToolWindowSelection for why a TwoWay binding is unreliable in these hosted tool windows.
        private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DataContext is SettingsToolWindowViewModel viewModel && ToolWindowSelection.TryGetAddedItem(e, out LanguageOption language))
                viewModel.UserSelectLanguage(language);
        }

        // The color ComboBox lives in an ItemsControl template, so its DataContext is a per-row
        // ColorItemViewModel rather than the hosted settings view model.
        private void ColorCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox combo && combo.DataContext is ColorItemViewModel colorItem && ToolWindowSelection.TryGetAddedItem(e, out string colorName))
                colorItem.UserSelectColorName(colorName);
        }
    }
}
