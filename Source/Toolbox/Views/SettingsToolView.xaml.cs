using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Toolbox.ToolWindows;
using FreeTrainSimulator.Toolbox.ViewModels;

using GetText;

using ProfileRow = FreeTrainSimulator.Toolbox.ViewModels.ManageProfilesViewModel.ProfileRow;

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
        private readonly CollectionContainer storedProfileRows = new CollectionContainer();
        private readonly CompositeCollection profileRows;
        private bool profileOperationInProgress;
        private ListBoxItem profileDraftRow;
        private TextBox profileDraftName;
        private string draftSourceName;
        private string selectionBeforeDraft;

        public SettingsToolView()
        {
            profileRows = new CompositeCollection { storedProfileRows };
            InitializeComponent();
            ProfilesList.ItemsSource = profileRows;
            DataContextChanged += SettingsToolView_DataContextChanged;
        }

        private ManageProfilesViewModel ProfileManagement => (DataContext as SettingsToolWindowViewModel)?.ProfileManagement;

        internal int SelectedTabIndex
        {
            get => SettingsTabs.SelectedIndex;
            set => SettingsTabs.SelectedIndex = value >= 0 && value < SettingsTabs.Items.Count ? value : 0;
        }

        internal void ShowProfiles() => ProfilesTab.IsSelected = true;

        private async void SettingsToolView_DataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ReferenceEquals((e.OldValue as SettingsToolWindowViewModel)?.ProfileManagement, ProfileManagement))
                return;
            RemoveProfileDraft();
            if (ProfilesTab.IsSelected)
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
            if (profiles == null || profileOperationInProgress || profileDraftRow != null)
                return;

            SetProfileOperationInProgress(true);
            try
            {
                await profiles.RefreshAsync(CancellationToken.None).ConfigureAwait(true);
                UpdateProfileList(profiles, (ProfilesList.SelectedItem as ProfileRow)?.Name);
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

        private void CreateProfileButton_Click(object sender, RoutedEventArgs e) => BeginProfileDraft(null);

        private void DuplicateProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (ProfilesList.SelectedItem is ProfileRow row)
                BeginProfileDraft(row.Name);
        }

        private void BeginProfileDraft(string sourceName)
        {
            if (ProfileManagement?.CanCreate != true || profileOperationInProgress || profileDraftRow != null)
                return;

            selectionBeforeDraft = (ProfilesList.SelectedItem as ProfileRow)?.Name;
            draftSourceName = sourceName;
            profileDraftRow = (ListBoxItem)ProfilesList.FindResource("ProfileDraftRow");
            profileRows.Insert(0, profileDraftRow);
            ProfilesList.SelectedItem = profileDraftRow;
            ProfilesList.ScrollIntoView(profileDraftRow);
            ProfileStatus.Text = string.Empty;
            UpdateProfileButtons();
        }

        private void ProfileDraftName_Loaded(object sender, RoutedEventArgs e)
        {
            profileDraftName = (TextBox)sender;
            if (ProfilesList.SelectedItem == profileDraftRow)
                profileDraftName.Focus();
        }

        private async void SaveProfileName_Click(object sender, RoutedEventArgs e) => await SaveProfileDraftAsync().ConfigureAwait(true);

        private void CancelProfileName_Click(object sender, RoutedEventArgs e) => CancelProfileDraft();

        private async void ProfilesList_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (profileDraftRow == null)
                return;
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                CancelProfileDraft();
            }
            else if (e.Key == Key.Enter && ReferenceEquals(e.OriginalSource, profileDraftName))
            {
                e.Handled = true;
                if (!e.IsRepeat)
                    await SaveProfileDraftAsync().ConfigureAwait(true);
            }
        }

        private async Task SaveProfileDraftAsync()
        {
            ManageProfilesViewModel profiles = ProfileManagement;
            ListBoxItem draft = profileDraftRow;
            if (profiles?.CanCreate != true || profileOperationInProgress || draft == null || profileDraftName == null)
                return;

            string destination = profileDraftName.Text.Trim();
            SetProfileOperationInProgress(true);
            try
            {
                string error = await profiles.CreateAsync(destination, draftSourceName, CancellationToken.None).ConfigureAwait(true);
                if (profileDraftRow != draft)
                    return;
                ProfileStatus.Text = error ?? CatalogManager.Catalog.GetString("Profile created. The active profile has not changed.");
                if (error == null)
                {
                    RemoveProfileDraft();
                    UpdateProfileList(profiles, destination);
                    ProfilesList.ScrollIntoView(ProfilesList.SelectedItem);
                    ProfilesList.Focus();
                }
            }
            catch (Exception failure) when (CleanupExceptionPolicy.CanContinue(failure))
            {
                Trace.TraceError($"Failed to create a Toolbox profile: {failure}");
                if (profileDraftRow == draft)
                    ProfileStatus.Text = CatalogManager.Catalog.GetString("Could not create the profile: {0}", failure.Message);
            }
            finally
            {
                SetProfileOperationInProgress(false);
                if (profileDraftRow == draft)
                    profileDraftName.Focus();
            }
        }

        private void CancelProfileDraft()
        {
            if (profileOperationInProgress || profileDraftRow == null)
                return;

            string previousSelection = selectionBeforeDraft;
            RemoveProfileDraft();
            UpdateProfileList(ProfileManagement, previousSelection);
            ProfileStatus.Text = string.Empty;
            ProfilesList.Focus();
        }

        private void RemoveProfileDraft()
        {
            if (profileDraftRow == null)
                return;

            ListBoxItem draft = profileDraftRow;
            profileDraftRow = null;
            profileDraftName = null;
            draftSourceName = null;
            selectionBeforeDraft = null;
            profileRows.Remove(draft);
            UpdateProfileButtons();
        }

        private async void ProfilesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left || ItemsControl.ContainerFromElement(ProfilesList, e.OriginalSource as DependencyObject) is not ListBoxItem { DataContext: ProfileRow row })
                return;
            e.Handled = true;
            await UseProfileAsync(row).ConfigureAwait(true);
        }

        private async void ProfilesList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || e.IsRepeat || ProfilesList.SelectedItem is not ProfileRow row)
                return;
            e.Handled = true;
            await UseProfileAsync(row).ConfigureAwait(true);
        }

        private async Task UseProfileAsync(ProfileRow row)
        {
            if (profileOperationInProgress || profileDraftRow != null || ProfileManagement is not { CanCreate: true, UseProfileAsync: not null } profiles || row.IsActive)
                return;

            SetProfileOperationInProgress(true);
            try
            {
                await profiles.UseProfileAsync(new ProfileModel(row.Name)).ConfigureAwait(true);
            }
            finally
            {
                SetProfileOperationInProgress(false);
            }
        }

        private async void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
        {
            if (profileOperationInProgress || profileDraftRow != null || ProfileManagement is not { CanCreate: true } profiles
                || ProfilesList.SelectedItem is not ProfileRow { CanDelete: true } row)
                return;

            SetProfileOperationInProgress(true);
            try
            {
                Catalog catalog = CatalogManager.Catalog;
                string message = row.IsActive
                    ? catalog.GetString("Permanently delete profile '{0}' and its stored settings? Toolbox will close the map and open another profile.", row.Name)
                    : catalog.GetString("Permanently delete profile '{0}' and its stored settings?", row.Name);
                if (MessageBox.Show(Window.GetWindow(this), message, catalog.GetString("Delete Profile"),
                    MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes)
                    return;

                if (row.IsActive)
                {
                    await profiles.DeleteActiveProfileAsync().ConfigureAwait(true);
                }
                else
                {
                    await profiles.DeleteOtherProfileAsync(row.Name, CancellationToken.None).ConfigureAwait(true);
                    UpdateProfileList(profiles, null);
                    ProfileStatus.Text = catalog.GetString("Profile '{0}' was deleted.", row.Name);
                }
            }
            catch (Exception failure) when (CleanupExceptionPolicy.CanContinue(failure))
            {
                UpdateProfileList(profiles, row.Name);
                Trace.TraceError($"Failed to delete a Toolbox profile: {failure}");
                ProfileStatus.Text = CatalogManager.Catalog.GetString("Could not complete the profile deletion: {0}", failure.Message);
            }
            finally
            {
                SetProfileOperationInProgress(false);
            }
        }

        private void UpdateProfileList(ManageProfilesViewModel profiles, string selectedName)
        {
            storedProfileRows.Collection = profiles.Profiles;
            ProfilesList.SelectedItem = profiles.Profiles.FirstOrDefault(row => string.Equals(row.Name, selectedName, StringComparison.OrdinalIgnoreCase))
                ?? profiles.Profiles.FirstOrDefault(row => row.IsActive);
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
            bool canManage = !profileOperationInProgress && profiles?.CanCreate == true;
            if (profileDraftRow != null)
                profileDraftRow.IsEnabled = canManage;
            CreateProfileButton.IsEnabled = canManage && profileDraftRow == null;
            ProfileRow selected = ProfilesList.SelectedItem as ProfileRow;
            DuplicateProfileButton.IsEnabled = CreateProfileButton.IsEnabled && selected != null && profiles.CaptureActiveConfigurationAsync != null;
            DeleteProfileButton.IsEnabled = CreateProfileButton.IsEnabled && selected?.CanDelete == true && (!selected.IsActive || profiles.DeleteActiveProfileAsync != null);
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
