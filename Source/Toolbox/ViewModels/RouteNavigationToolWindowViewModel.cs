using System;
using System.Collections.Immutable;
using System.Collections.ObjectModel;

using FreeTrainSimulator.Toolbox.ToolWindows;

namespace FreeTrainSimulator.Toolbox.ViewModels
{
    /// <summary>
    /// Bindable view model for the hosted route-navigation dockable tool window. Pulls an immutable
    /// <see cref="RouteNavigationSnapshot"/> from the <see cref="RouteNavigationToolWindow"/> bridge on the
    /// shared <see cref="ToolWindowRefreshScheduler"/> and exposes filterable station, platform, and siding
    /// lists (selecting a row centers and highlights the map) plus by-id track item and track node searches.
    /// All navigation is forwarded back to the bridge, which marshals it onto the game thread.
    /// </summary>
    internal sealed class RouteNavigationToolWindowViewModel : PollingToolWindowViewModel
    {
        private readonly RouteNavigationToolWindow toolWindow;
        private string stationFilter = string.Empty;
        private string platformFilter = string.Empty;
        private string sidingFilter = string.Empty;
        private string trackItemSearchText = string.Empty;
        private string trackNodeSearchText = string.Empty;
        private bool searchRoads;
        private RouteNavigationItemViewModel selectedStation;
        private RouteNavigationItemViewModel selectedPlatform;
        private RouteNavigationItemViewModel selectedSiding;
        private bool suppressSelectionCommand;

        public RouteNavigationToolWindowViewModel(RouteNavigationToolWindow toolWindow, ToolWindowRefreshScheduler scheduler)
            : base(scheduler, ToolWindowRefreshScheduler.BaseInterval)
        {
            ArgumentNullException.ThrowIfNull(toolWindow);

            this.toolWindow = toolWindow;
            TrackItemSearchCommand = new RelayCommand(_ => toolWindow.SearchTrackItemByIndex(TrackItemSearchText));
            TrackNodeSearchCommand = new RelayCommand(_ => toolWindow.SearchTrackNodeByIndex(TrackNodeSearchText, SearchRoads));
        }

        public string Title => toolWindow.Title;

        public ObservableCollection<RouteNavigationItemViewModel> Stations { get; } = new ObservableCollection<RouteNavigationItemViewModel>();

        public ObservableCollection<RouteNavigationItemViewModel> Platforms { get; } = new ObservableCollection<RouteNavigationItemViewModel>();

        public ObservableCollection<RouteNavigationItemViewModel> Sidings { get; } = new ObservableCollection<RouteNavigationItemViewModel>();

        /// <summary>Name/value detail rows for the selected (pinned) entity, or the item currently hovered on the map.</summary>
        public ObservableCollection<DebugToolWindowRowViewModel> DetailRows { get; } = new ObservableCollection<DebugToolWindowRowViewModel>();

        public RelayCommand TrackItemSearchCommand { get; }

        public RelayCommand TrackNodeSearchCommand { get; }

        public string StationFilter
        {
            get => stationFilter;
            set
            {
                if (SetProperty(ref stationFilter, value))
                    ApplyFilter(Stations, value);
            }
        }

        public string PlatformFilter
        {
            get => platformFilter;
            set
            {
                if (SetProperty(ref platformFilter, value))
                    ApplyFilter(Platforms, value);
            }
        }

        public string SidingFilter
        {
            get => sidingFilter;
            set
            {
                if (SetProperty(ref sidingFilter, value))
                    ApplyFilter(Sidings, value);
            }
        }

        public string TrackItemSearchText
        {
            get => trackItemSearchText;
            set => SetProperty(ref trackItemSearchText, value);
        }

        public string TrackNodeSearchText
        {
            get => trackNodeSearchText;
            set => SetProperty(ref trackNodeSearchText, value);
        }

        /// <summary>When true the track node search targets road nodes; otherwise rail nodes.</summary>
        public bool SearchRoads
        {
            get => searchRoads;
            set => SetProperty(ref searchRoads, value);
        }

        public RouteNavigationItemViewModel SelectedStation
        {
            get => selectedStation;
            set
            {
                if (!SetProperty(ref selectedStation, value) || suppressSelectionCommand)
                    return;

                if (value != null)
                    toolWindow.NavigateToStation(value.Index);
            }
        }

        public RouteNavigationItemViewModel SelectedPlatform
        {
            get => selectedPlatform;
            set
            {
                if (!SetProperty(ref selectedPlatform, value) || suppressSelectionCommand)
                    return;

                if (value != null)
                    toolWindow.NavigateToPlatform(value.Index);
            }
        }

        public RouteNavigationItemViewModel SelectedSiding
        {
            get => selectedSiding;
            set
            {
                if (!SetProperty(ref selectedSiding, value) || suppressSelectionCommand)
                    return;

                if (value != null)
                    toolWindow.NavigateToSiding(value.Index);
            }
        }

        protected override void OnStarted() => toolWindow.Active = true;

        protected override void OnStopped() => toolWindow.Active = false;

        protected override void Refresh()
        {
            RouteNavigationSnapshot snapshot = toolWindow.CaptureRouteNavigationSnapshot();

            Sync(Stations, snapshot.Stations, stationFilter);
            Sync(Platforms, snapshot.Platforms, platformFilter);
            Sync(Sidings, snapshot.Sidings, sidingFilter);
            DebugToolWindowRowViewModel.Sync(DetailRows, snapshot.DetailRows);
        }

        private void Sync(ObservableCollection<RouteNavigationItemViewModel> target, ImmutableArray<RouteNavigationRow> rows, string filter)
        {
            int selectedIndex = SelectedIndexOf(target);

            for (int i = 0; i < rows.Length; i++)
            {
                RouteNavigationRow row = rows[i];
                if (i < target.Count)
                    target[i].Update(row.Index, row.Name, row.GroupName);
                else
                    target.Add(new RouteNavigationItemViewModel(row.Index, row.Name, row.GroupName));
            }

            for (int i = target.Count - 1; i >= rows.Length; i--)
                target.RemoveAt(i);

            ApplyFilter(target, filter);

            if (selectedIndex >= 0)
                RestoreSelection(target, selectedIndex);
        }

        private int SelectedIndexOf(ObservableCollection<RouteNavigationItemViewModel> target)
        {
            if (ReferenceEquals(target, Stations))
                return SelectedStation?.Index ?? -1;
            if (ReferenceEquals(target, Platforms))
                return SelectedPlatform?.Index ?? -1;
            return SelectedSiding?.Index ?? -1;
        }

        private void RestoreSelection(ObservableCollection<RouteNavigationItemViewModel> target, int index)
        {
            suppressSelectionCommand = true;
            try
            {
                RouteNavigationItemViewModel match = null;
                foreach (RouteNavigationItemViewModel item in target)
                {
                    if (item.Index == index)
                    {
                        match = item;
                        break;
                    }
                }

                if (ReferenceEquals(target, Stations))
                    SelectedStation = match;
                else if (ReferenceEquals(target, Platforms))
                    SelectedPlatform = match;
                else
                    SelectedSiding = match;
            }
            finally
            {
                suppressSelectionCommand = false;
            }
        }

        private static void ApplyFilter(ObservableCollection<RouteNavigationItemViewModel> target, string filter)
        {
            foreach (RouteNavigationItemViewModel item in target)
                item.IsVisible = string.IsNullOrEmpty(filter)
                    || (item.Name?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (item.GroupName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
        }
    }

    /// <summary>Bindable row for a route-navigation name list. Observable so it can be updated in place.</summary>
    internal sealed class RouteNavigationItemViewModel : ObservableObject
    {
        private int index;
        private string name;
        private string groupName;
        private bool isVisible = true;

        public RouteNavigationItemViewModel(int index, string name, string groupName)
        {
            this.index = index;
            this.name = name;
            this.groupName = groupName;
        }

        public int Index
        {
            get => index;
            private set => SetProperty(ref index, value);
        }

        public string Name
        {
            get => name;
            private set => SetProperty(ref name, value);
        }

        public string GroupName
        {
            get => groupName;
            private set => SetProperty(ref groupName, value);
        }

        public bool IsVisible
        {
            get => isVisible;
            set => SetProperty(ref isVisible, value);
        }

        internal void Update(int index, string name, string groupName)
        {
            Index = index;
            Name = name;
            GroupName = groupName;
        }
    }
}
