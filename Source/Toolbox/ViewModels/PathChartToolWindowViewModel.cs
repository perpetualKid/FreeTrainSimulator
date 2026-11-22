using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using FreeTrainSimulator.Runtime.Track;
using FreeTrainSimulator.Toolbox.ToolWindows;

using GetText;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>A physical station encounter or the disclosure row for a consecutive station run.</summary>
    internal sealed class PathChartStationItemViewModel : ObservableObject
    {
        private readonly Action<PathChartStationItemViewModel, bool> expansionChanged;
        private string distanceText;
        private bool isExpanded;

        /// <summary>Exact encounter, or the first encounter represented by a disclosure row.</summary>
        public PathChartStationVisit Source { get; }
        /// <summary>Untranslated content-authored station name.</summary>
        public string StationName => Source.StationName;
        /// <summary>Shared chart distance in metres.</summary>
        public double Distance => Source.Distance;
        /// <summary>Physical distance text; an expanded grouping anchor has no distance.</summary>
        public string DistanceText => IsGroupingAnchor ? string.Empty : distanceText;
        /// <summary>All physical encounters shown underneath an expanded run, including its first.</summary>
        public ImmutableArray<PathChartStationItemViewModel> Encounters { get; }
        /// <summary>Whether this row owns repeated consecutive encounters.</summary>
        public bool CanExpand => !Encounters.IsEmpty;
        /// <summary>Whether this row is a child encounter rather than a top-level station row.</summary>
        public bool IsIndented { get; }
        /// <summary>Whether this row currently represents a name-only grouping anchor.</summary>
        public bool IsGroupingAnchor => CanExpand && IsExpanded;
        /// <summary>Independent run expansion, coordinated with the visible list and selection.</summary>
        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                if (value == isExpanded)
                    return;
                if (expansionChanged == null)
                    throw new InvalidOperationException("A single station encounter cannot be expanded.");
                expansionChanged(this, value);
            }
        }
        /// <summary>Localized disclosure tooltip and accessible action name.</summary>
        public string ExpansionActionText => IsExpanded
            ? CatalogManager.Catalog.GetString("Collapse encounters at {0}", StationName)
            : CatalogManager.Catalog.GetString("Expand encounters at {0}", StationName);

        public PathChartStationItemViewModel(PathChartStationVisit source, bool metric)
            : this(source, metric, false)
        {
        }

        private PathChartStationItemViewModel(PathChartStationVisit source, bool metric, bool isIndented)
        {
            Source = source ?? throw new ArgumentNullException(nameof(source));
            IsIndented = isIndented;
            Encounters = ImmutableArray<PathChartStationItemViewModel>.Empty;
            distanceText = FormatDistance(metric);
        }

        public PathChartStationItemViewModel(ImmutableArray<PathChartStationVisit> encounters, bool metric,
            Action<PathChartStationItemViewModel, bool> expansionChanged)
        {
            if (encounters.IsDefaultOrEmpty || encounters.Length < 2)
                throw new ArgumentException("A station run must contain repeated encounters.", nameof(encounters));
            this.expansionChanged = expansionChanged ?? throw new ArgumentNullException(nameof(expansionChanged));
            Source = encounters[0];
            Encounters = encounters.Select(encounter => new PathChartStationItemViewModel(encounter, metric, true)).ToImmutableArray();
            distanceText = FormatDistance(metric);
        }

        internal void SetExpanded(bool expanded)
        {
            if (SetProperty(ref isExpanded, expanded, nameof(IsExpanded)))
            {
                OnPropertyChanged(nameof(IsGroupingAnchor));
                OnPropertyChanged(nameof(DistanceText));
                OnPropertyChanged(nameof(ExpansionActionText));
            }
        }

        internal void RefreshPresentation(bool metric)
        {
            distanceText = FormatDistance(metric);
            OnPropertyChanged(nameof(DistanceText));
            OnPropertyChanged(nameof(ExpansionActionText));
            foreach (PathChartStationItemViewModel encounter in Encounters)
                encounter.RefreshPresentation(metric);
        }

        private string FormatDistance(bool metric) => string.Format(CultureInfo.CurrentCulture, metric ? "{0:N2} km" : "{0:N2} mi",
            metric ? Source.Distance / 1000 : Source.Distance / 1609.344);
    }

    /// <summary>Visible-only snapshot checks; pointer inspection never extracts the route.</summary>
    internal sealed class PathChartToolWindowViewModel : PollingToolWindowViewModel
    {
        private readonly PathChartToolWindow bridge;
        private readonly List<PathChartStationItemViewModel> stationGroups = new List<PathChartStationItemViewModel>();
        private PathChartSnapshot displayed = PathChartSnapshot.Empty;
        private PathChartStationItemViewModel selectedStation;
        private double cursorDistance;
        private bool selectingStation;

        public PathChartToolWindowViewModel(PathChartToolWindow bridge, ToolWindowRefreshScheduler scheduler)
            : base(scheduler, TimeSpan.FromMilliseconds(100))
        {
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            ShowOnMapCommand = new RelayCommand(_ => ShowOnMap(), _ => Profile?.IsAvailable == true
                && !Profile.Samples.IsEmpty && SelectedStation?.IsGroupingAnchor != true);
        }

        /// <summary>Immutable cached profile used by the native drawing control.</summary>
        public PathChartProfile Profile => displayed.Profile;
        /// <summary>Application distance and elevation units.</summary>
        public bool UseMetricUnits => displayed.UseMetricUnits;
        /// <summary>Consecutive station runs and the physical encounter rows of independently expanded runs.</summary>
        public ObservableCollection<PathChartStationItemViewModel> Stations { get; } = new ObservableCollection<PathChartStationItemViewModel>();
        /// <summary>Explicit owner-dispatched map navigation, never triggered by hover.</summary>
        public RelayCommand ShowOnMapCommand { get; }
        /// <summary>Read-only route identity; never the tentative candidate name.</summary>
        public string TitleText => displayed.Source == null ? CatalogManager.Catalog.GetString("Main route")
            : CatalogManager.Catalog.GetString("Main route: {0}", displayed.Source.Name);

        /// <summary>Unavailable reason or navigation result.</summary>
        public string StatusMessage => GetStatusMessage();

        /// <summary>Nearest marker encounter, or an explicit list selection that moves the shared cursor.</summary>
        public PathChartStationItemViewModel SelectedStation
        {
            get => selectedStation;
            set
            {
                if (SetProperty(ref selectedStation, value))
                {
                    if (value != null && !value.IsGroupingAnchor)
                    {
                        selectingStation = true;
                        try
                        {
                            CursorDistance = value.Distance;
                        }
                        finally
                        {
                            selectingStation = false;
                        }
                    }
                    ShowOnMapCommand.RaiseCanExecuteChanged();
                }
            }
        }

        /// <summary>Shared lane cursor in track metres; marking the nearest station never snaps its distance.</summary>
        public double CursorDistance
        {
            get => cursorDistance;
            set
            {
                if (!double.IsFinite(value))
                    throw new ArgumentOutOfRangeException(nameof(value));
                double clamped = Math.Clamp(value, 0, Profile?.Length ?? 0);
                bool cursorChanged = SetProperty(ref cursorDistance, clamped);
                if (!selectingStation && SetProperty(ref selectedStation, FindNearestStationRow(), nameof(SelectedStation)))
                    ShowOnMapCommand.RaiseCanExecuteChanged();
                if (cursorChanged)
                    OnPropertyChanged(nameof(SelectedValuesText));
            }
        }

        /// <summary>Accessible numeric inspection; curvature is positive right and negative left.</summary>
        public string SelectedValuesText
        {
            get
            {
                PathChartSample? nearest = FindNearestSample();
                if (!nearest.HasValue)
                    return string.Empty;
                PathChartSample sample = nearest.Value;
                return UseMetricUnits
                    ? CatalogManager.Catalog.GetString("Distance {0:N2} km; elevation {1:N1} m; grade {2:N2}%; curvature {3:N2} /km (positive right)",
                        sample.Distance / 1000, sample.Elevation, sample.GradePercent, sample.CurvaturePerKilometre)
                    : CatalogManager.Catalog.GetString("Distance {0:N2} mi; elevation {1:N1} ft; grade {2:N2}%; curvature {3:N2} /km (positive right)",
                        sample.Distance / 1609.344, sample.Elevation / 0.3048, sample.GradePercent, sample.CurvaturePerKilometre);
            }
        }

        protected override void OnStarted() => bridge.Active = true;
        protected override void OnStopped() => bridge.Active = false;

        protected override void Refresh()
        {
            PathChartSnapshot current = bridge.CaptureSnapshot();
            if (ReferenceEquals(current, displayed))
                return;

            bool profileChanged = !ReferenceEquals(current.Profile, displayed.Profile);
            bool unitsChanged = current.UseMetricUnits != displayed.UseMetricUnits;
            displayed = current;
            if (profileChanged || unitsChanged)
            {
                if (profileChanged)
                {
                    RebuildStationGroups();
                    CursorDistance = 0;
                }
                else
                    RefreshStationPresentation();
                OnPropertyChanged(nameof(Profile));
                OnPropertyChanged(nameof(UseMetricUnits));
                OnPropertyChanged(nameof(SelectedValuesText));
            }
            OnPropertyChanged(nameof(TitleText));
            OnPropertyChanged(nameof(StatusMessage));
            ShowOnMapCommand.RaiseCanExecuteChanged();
        }

        /// <summary>Refreshes bound presentation text without rebuilding the profile.</summary>
        public void RefreshLocalization()
        {
            RefreshStationPresentation();
            OnPropertyChanged(nameof(TitleText));
            OnPropertyChanged(nameof(StatusMessage));
            OnPropertyChanged(nameof(SelectedValuesText));
        }

        /// <summary>Handles native list keyboard expansion without selecting a different encounter.</summary>
        internal bool TrySetStationGroupExpanded(PathChartStationItemViewModel station, bool expanded)
        {
            if (station == null)
                return false;
            PathChartStationItemViewModel group = stationGroups.FirstOrDefault(row => ReferenceEquals(row, station)
                || row.Encounters.Contains(station));
            if (group?.CanExpand != true)
                return false;
            group.IsExpanded = expanded;
            return true;
        }

        private void RebuildStationGroups()
        {
            selectedStation = null;
            Stations.Clear();
            stationGroups.Clear();
            if (Profile?.IsAvailable == true)
            {
                int index = 0;
                while (index < Profile.Stations.Length)
                {
                    PathChartStationVisit first = Profile.Stations[index++];
                    ImmutableArray<PathChartStationVisit>.Builder encounters = ImmutableArray.CreateBuilder<PathChartStationVisit>();
                    encounters.Add(first);
                    while (index < Profile.Stations.Length && string.Equals(first.StationName, Profile.Stations[index].StationName, StringComparison.Ordinal))
                        encounters.Add(Profile.Stations[index++]);
                    PathChartStationItemViewModel group = encounters.Count == 1
                        ? new PathChartStationItemViewModel(first, UseMetricUnits)
                        : new PathChartStationItemViewModel(encounters.ToImmutable(), UseMetricUnits, SetStationGroupExpanded);
                    stationGroups.Add(group);
                    Stations.Add(group);
                }
            }
            OnPropertyChanged(nameof(SelectedStation));
        }

        private void SetStationGroupExpanded(PathChartStationItemViewModel group, bool expanded)
        {
            if (!stationGroups.Contains(group))
            {
                Trace.TraceWarning("Cannot expand a station run from an outdated path chart.");
                return;
            }
            PathChartStationItemViewModel previousSelection = selectedStation;
            selectedStation = null;
            group.SetExpanded(expanded);
            if (expanded)
            {
                int index = Stations.IndexOf(group) + 1;
                foreach (PathChartStationItemViewModel encounter in group.Encounters)
                    Stations.Insert(index++, encounter);
                selectedStation = ReferenceEquals(previousSelection, group) ? FindNearestStationRow() : previousSelection;
            }
            else
            {
                foreach (PathChartStationItemViewModel encounter in group.Encounters)
                    Stations.Remove(encounter);
                selectedStation = ReferenceEquals(previousSelection, group.Encounters[0]) ? group
                    : ReferenceEquals(previousSelection, group) || group.Encounters.Contains(previousSelection) ? null : previousSelection;
            }
            OnPropertyChanged(nameof(SelectedStation));
            ShowOnMapCommand.RaiseCanExecuteChanged();
        }

        private void RefreshStationPresentation()
        {
            foreach (PathChartStationItemViewModel group in stationGroups)
                group.RefreshPresentation(UseMetricUnits);
        }

        private PathChartStationItemViewModel FindNearestStationRow()
        {
            if (Profile?.IsAvailable != true || Profile.Stations.IsEmpty)
                return null;
            int nearestIndex = 0;
            double nearestError = Math.Abs(Profile.Stations[0].Distance - CursorDistance);
            for (int index = 1; index < Profile.Stations.Length; index++)
            {
                double error = Math.Abs(Profile.Stations[index].Distance - CursorDistance);
                if (error < nearestError || (error == nearestError && Profile.Stations[index].Distance < Profile.Stations[nearestIndex].Distance))
                {
                    nearestIndex = index;
                    nearestError = error;
                }
            }
            int encounterIndex = nearestIndex;
            foreach (PathChartStationItemViewModel group in stationGroups)
            {
                int count = group.CanExpand ? group.Encounters.Length : 1;
                if (encounterIndex < count)
                    return group.IsExpanded ? group.Encounters[encounterIndex] : group;
                encounterIndex -= count;
            }
            throw new InvalidOperationException("The station list does not match the current path chart profile.");
        }

        private PathChartSample? FindNearestSample()
        {
            if (Profile?.IsAvailable != true || Profile.Samples.IsEmpty)
                return null;
            int low = 0;
            int high = Profile.Samples.Length - 1;
            while (low < high)
            {
                int middle = low + (high - low) / 2;
                if (Profile.Samples[middle].Distance < CursorDistance)
                    low = middle + 1;
                else
                    high = middle;
            }
            return low > 0 && CursorDistance - Profile.Samples[low - 1].Distance < Profile.Samples[low].Distance - CursorDistance
                ? Profile.Samples[low - 1] : Profile.Samples[low];
        }

        private void ShowOnMap()
        {
            if (SelectedStation?.IsGroupingAnchor == true)
            {
                Trace.TraceWarning("Select a physical station encounter before navigating from an expanded grouping anchor.");
                return;
            }
            PathChartSample? sample = FindNearestSample();
            if (!sample.HasValue)
                return;
            bridge.ShowOnMap(displayed, SelectedStation != null && Math.Abs(SelectedStation.Distance - CursorDistance) <= 0.01
                ? SelectedStation.Source.Location : sample.Value.Location);
        }

        private string GetStatusMessage()
        {
            if (displayed.World == null)
                return CatalogManager.Catalog.GetString("Load a route to view its path chart.");
            if (displayed.Source == null)
                return CatalogManager.Catalog.GetString("Open or create a path to view its main-route chart.");
            if (Profile == null)
                return CatalogManager.Catalog.GetString("The current route geometry is not available for this path.");
            if (!Profile.IsAvailable)
                return LocalizeProfileDiagnostic(Profile.UnavailableReason);
            string status = string.Join(Environment.NewLine, Profile.Diagnostics.Select(LocalizeProfileDiagnostic));
            if (displayed.NavigationMessage != null)
            {
                string navigation = displayed.NavigationMessage == "Map centered on chart position."
                    ? CatalogManager.Catalog.GetString("Map centered on chart position.")
                    : CatalogManager.Catalog.GetString("The route or path has changed. Select a position on the current chart and try again.");
                status = status.Length == 0 ? navigation : navigation + Environment.NewLine + status;
            }
            return status;
        }

        private static string LocalizeProfileDiagnostic(string message)
        {
            return message switch
            {
                "The path has no resolved main traversal." => CatalogManager.Catalog.GetString("The path has no resolved main traversal."),
                "The authored main path topology is not proven." => CatalogManager.Catalog.GetString("The authored main path topology is not proven."),
                "The path resolution does not match the authored main path." => CatalogManager.Catalog.GetString("The path resolution does not match the authored main path."),
                "The main path contains an unresolved or ambiguous span." => CatalogManager.Catalog.GetString("The main path contains an unresolved or ambiguous span."),
                "The main path contains missing or inconsistent track geometry." => CatalogManager.Catalog.GetString("The main path contains missing or inconsistent track geometry."),
                "The selected main path connector cannot be established." => CatalogManager.Catalog.GetString("The selected main path connector cannot be established."),
                "The path chart exceeds the supported sample limit." => CatalogManager.Catalog.GetString("The path chart exceeds the supported sample limit."),
                "A platform pair cannot be established on the traversed track." => CatalogManager.Catalog.GetString("A platform pair cannot be established on the traversed track."),
                "A chart item cannot be located unambiguously on its owning track." => CatalogManager.Catalog.GetString("A chart item cannot be located unambiguously on its owning track."),
                "Milepost units and direction are not retained in this track model." => CatalogManager.Catalog.GetString("Milepost units and direction are not retained in this track model."),
                "Passing branch profiles are not included in the main path chart." => CatalogManager.Catalog.GetString("Passing branch profiles are not included in the main path chart."),
                _ => CatalogManager.Catalog.GetString(message),
            };
        }
    }
}