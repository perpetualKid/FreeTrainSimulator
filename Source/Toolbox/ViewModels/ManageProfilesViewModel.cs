using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Settings;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Toolbox.Settings;

using GetText;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    internal sealed partial class ManageProfilesViewModel
    {
        private readonly ProfileModel activeProfile;
        private Task pendingProfileWrite;

        internal ManageProfilesViewModel(ProfileModel activeProfile)
        {
            this.activeProfile = activeProfile ?? throw new ArgumentNullException(nameof(activeProfile));
        }

        internal bool CanCreate { get; private set; } = true;

        internal Func<ProfileModel, CancellationToken, Task<(ProfileToolboxSettingsModel Toolbox, ProfileUserSettingsModel User)>> CaptureActiveConfigurationAsync { get; init; }

        internal Func<ProfileModel, Task> UseProfileAsync { get; init; }
        internal Func<Task> DeleteActiveProfileAsync { get; init; }

        internal bool IsOtherProfile(string name) => name != null && !string.Equals(name, activeProfile.Name, StringComparison.OrdinalIgnoreCase);

        internal ImmutableArray<ProfileRow> Profiles { get; private set; } = [];

        internal async Task RefreshAsync(CancellationToken cancellationToken)
        {
            UpdateProfileNames(await activeProfile.RefreshProfiles(cancellationToken).ConfigureAwait(true));
        }

        internal Task<string> CreateAsync(string input, string sourceName, CancellationToken cancellationToken)
        {
            Task<string> creation = CreateProfileAsync(input, sourceName, cancellationToken);
            pendingProfileWrite = creation;
            return creation;
        }

        internal Task DeleteOtherProfileAsync(string name, CancellationToken cancellationToken)
        {
            if (!IsOtherProfile(name))
                throw new InvalidOperationException("The active profile must be removed through the workspace handoff.");

            Task deletion = DeleteInactiveProfileAsync(new ProfileModel(name), cancellationToken);
            pendingProfileWrite = deletion;
            return deletion;
        }

        internal async Task FinishAsync()
        {
            CanCreate = false;
            if (pendingProfileWrite != null)
                await pendingProfileWrite.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
        }

        private async Task DeleteInactiveProfileAsync(ProfileModel profile, CancellationToken cancellationToken)
        {
            ProfileModel launcherProfile = await profile.Current(cancellationToken).ConfigureAwait(true);
            UpdateProfileNames(await DeleteStoredProfileAsync(profile, cancellationToken).ConfigureAwait(true));
            if (string.Equals(launcherProfile.Name, profile.Name, StringComparison.OrdinalIgnoreCase))
            {
                ProfileModel fallback = await profile.Current(cancellationToken).ConfigureAwait(true);
                await fallback.UpdateCurrent(cancellationToken).ConfigureAwait(true);
            }
        }

        internal static async Task<ImmutableArray<ProfileModel>> DeleteStoredProfileAsync(ProfileModel profile, CancellationToken cancellationToken)
        {
            ImmutableArray<ProfileModel> stored = await profile.RefreshProfiles(cancellationToken).ConfigureAwait(true);
            ProfileModel existing = stored.GetByName(profile.Name);
            if (existing == null || !IsValidProfileName(existing.Name))
                throw new InvalidOperationException(CatalogManager.Catalog.GetString("The selected profile no longer exists."));

            ImmutableArray<ProfileModel> remaining = await existing.Delete(cancellationToken).ConfigureAwait(true);
            
            return remaining.GetByName(existing.Name) != null
                ? throw new IOException(CatalogManager.Catalog.GetString("The profile '{0}' could not be removed from disk.", existing.Name))
                : remaining;
        }

        private async Task<string> CreateProfileAsync(string input, string sourceName, CancellationToken cancellationToken)
        {
            string name = input?.Trim();
            // A profile name is also a directory name. The existing loader strips extensions.
            if (!IsValidProfileName(name))
            {
                return CatalogManager.Catalog.GetString("Enter a valid profile name without periods or reserved file names.");
            }

            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            if (Profiles.Any(row => string.Equals(row.Name, name, StringComparison.OrdinalIgnoreCase)))
                return CatalogManager.Catalog.GetString("A profile named '{0}' already exists.", name);

            ProfileModel profile = new ProfileModel(name);
            if (sourceName == null)
            {
                UpdateProfileNames(await profile.CreateWithoutActivating(cancellationToken).ConfigureAwait(true));
                return null;
            }

            if (!Profiles.Any(row => string.Equals(row.Name, sourceName, StringComparison.OrdinalIgnoreCase)))
                return CatalogManager.Catalog.GetString("The selected profile no longer exists.");

            ProfileModel source = new ProfileModel(sourceName);
            (ProfileToolboxSettingsModel preferences, ProfileUserSettingsModel userSettings) = IsOtherProfile(sourceName)
                ? ((await source.LoadSettingsModel<ProfileToolboxSettingsModel>(cancellationToken).ConfigureAwait(true)).CopyPreferences(profile),
                    await source.LoadSettingsModel<ProfileUserSettingsModel>(cancellationToken).ConfigureAwait(true))
                : await CaptureActiveConfigurationAsync(profile, cancellationToken).ConfigureAwait(true);

            ImmutableArray<ProfileModel> profiles = await profile.CreateWithoutActivating(cancellationToken).ConfigureAwait(true);
            try
            {
                await source.CopySimulatorConfigurationTo(profile, userSettings, cancellationToken).ConfigureAwait(true);
                await profile.UpdateSettingsModel(preferences, cancellationToken).ConfigureAwait(true);
            }
            catch (Exception failure) when (CleanupExceptionPolicy.CanContinue(failure))
            {
                ImmutableArray<ProfileModel> remaining = await profile.Delete(CancellationToken.None).ConfigureAwait(true);
                UpdateProfileNames(remaining);
                if (remaining.Any(item => string.Equals(item.Name, profile.Name, StringComparison.OrdinalIgnoreCase)))
                    Trace.TraceWarning($"Could not remove incomplete profile copy '{profile.Name}'.");
                throw;
            }
            UpdateProfileNames(profiles);
            return null;
        }

        private void UpdateProfileNames(ImmutableArray<ProfileModel> profiles)
        {
            Profiles = profiles.Select(profile => profile.Name).Append(activeProfile.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                .Select(name => new ProfileRow(name, !IsOtherProfile(name), profiles.GetByName(name) != null && IsValidProfileName(name)))
                .ToImmutableArray();
        }

        private static bool IsValidProfileName(string name) => !string.IsNullOrEmpty(name) && name.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
            && !name.Contains('.', StringComparison.Ordinal) && !ReservedDeviceName().IsMatch(name)
            && !string.Equals(name, ProfileModel.TestingProfile, StringComparison.OrdinalIgnoreCase);

        internal sealed class ProfileRow
        {
            /// <summary>The profile name stored on disk.</summary>
            public string Name { get; }

            /// <summary>Whether this profile owns the current Toolbox workspace.</summary>
            public bool IsActive { get; }

            /// <summary>Whether this is a stored profile rather than a virtual fallback.</summary>
            internal bool CanDelete { get; }

            internal ProfileRow(string name, bool isActive, bool canDelete)
            {
                Name = name;
                IsActive = isActive;
                CanDelete = canDelete;
            }
        }

        [GeneratedRegex(@"\A(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|(?:COM|LPT)[1-9\u00B9\u00B2\u00B3])\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ReservedDeviceName();
    }
}