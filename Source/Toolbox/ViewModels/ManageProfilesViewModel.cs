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
        private Task pendingCreation;

        internal ManageProfilesViewModel(ProfileModel activeProfile)
        {
            this.activeProfile = activeProfile ?? throw new ArgumentNullException(nameof(activeProfile));
        }

        internal bool CanCreate { get; private set; } = true;

        internal Func<ProfileModel, CancellationToken, Task<(ProfileToolboxSettingsModel Toolbox, ProfileUserSettingsModel User)>> CaptureActiveConfigurationAsync { get; init; }

        internal Func<ProfileModel, Task> UseProfileAsync { get; init; }

        internal bool IsOtherProfile(string name) => name != null && !string.Equals(name, activeProfile.Name, StringComparison.OrdinalIgnoreCase);

        internal ImmutableArray<string> ProfileNames { get; private set; } = [];

        internal async Task RefreshAsync(CancellationToken cancellationToken)
        {
            UpdateProfileNames(await activeProfile.RefreshProfiles(cancellationToken).ConfigureAwait(true));
        }

        internal Task<string> CreateAsync(string input, string sourceName, CancellationToken cancellationToken)
        {
            Task<string> creation = CreateProfileAsync(input, sourceName, cancellationToken);
            pendingCreation = creation;
            return creation;
        }

        internal async Task FinishAsync()
        {
            CanCreate = false;
            if (pendingCreation != null)
                await pendingCreation.ConfigureAwait(ConfigureAwaitOptions.ContinueOnCapturedContext | ConfigureAwaitOptions.SuppressThrowing);
        }

        private async Task<string> CreateProfileAsync(string input, string sourceName, CancellationToken cancellationToken)
        {
            string name = input?.Trim();
            // A profile name is also a directory name. The existing loader strips extensions.
            if (string.IsNullOrEmpty(name) || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('.', StringComparison.Ordinal)
                || ReservedDeviceName().IsMatch(name) || string.Equals(name, ProfileModel.TestingProfile, StringComparison.OrdinalIgnoreCase))
            {
                return CatalogManager.Catalog.GetString("Enter a valid profile name without periods or reserved file names.");
            }

            await RefreshAsync(cancellationToken).ConfigureAwait(true);
            if (ProfileNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                return CatalogManager.Catalog.GetString("A profile named '{0}' already exists.", name);

            ProfileModel profile = new ProfileModel(name);
            if (sourceName == null)
            {
                UpdateProfileNames(await profile.CreateWithoutActivating(cancellationToken).ConfigureAwait(true));
                return null;
            }

            if (!ProfileNames.Contains(sourceName, StringComparer.OrdinalIgnoreCase))
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
            ProfileNames = profiles.Select(profile => profile.Name).Append(activeProfile.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase).ToImmutableArray();
        }

        [GeneratedRegex(@"\A(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|(?:COM|LPT)[1-9\u00B9\u00B2\u00B3])\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
        private static partial Regex ReservedDeviceName();
    }
}