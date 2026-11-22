using System.Collections.Immutable;

using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>An immutable chart of the committed, resolved main traversal, in travel order.</summary>
    public sealed record PathChartProfile
    {
        /// <summary>Whether the entire authored main extent has proven physical geometry.</summary>
        public bool IsAvailable { get; init; }

        /// <summary>Stable English diagnostic template when unavailable; otherwise empty.</summary>
        public string UnavailableReason { get; init; }

        /// <summary>Geometry samples, including section boundaries and exact chart item distances.</summary>
        public ImmutableArray<PathChartSample> Samples { get; init; }

        /// <summary>Consecutive station encounters grouped in interval-start order without global name deduplication.</summary>
        /// <remarks>Overlapping intervals need not have increasing <see cref="PathChartStationVisit.Distance"/> midpoints.</remarks>
        public ImmutableArray<PathChartStationVisit> Stations { get; init; }

        /// <summary>Directional source markers, not a train operating-speed envelope.</summary>
        public ImmutableArray<PathChartMarker> Markers { get; init; }

        /// <summary>Length of this main traversal in metres, including repeated sections.</summary>
        public double Length { get; init; }

        /// <summary>Stable English warning templates for items or branches excluded from the chart.</summary>
        public ImmutableArray<string> Diagnostics { get; init; }

        /// <summary>Initializes a chart result with immutable collections.</summary>
        public PathChartProfile(bool isAvailable, string unavailableReason, ImmutableArray<PathChartSample> samples, ImmutableArray<PathChartStationVisit> stations, ImmutableArray<PathChartMarker> markers, double length, ImmutableArray<string> diagnostics)
        {
            IsAvailable = isAvailable;
            UnavailableReason = unavailableReason ?? string.Empty;
            Samples = samples.IsDefault ? ImmutableArray<PathChartSample>.Empty : samples;
            Stations = stations.IsDefault ? ImmutableArray<PathChartStationVisit>.Empty : stations;
            Markers = markers.IsDefault ? ImmutableArray<PathChartMarker>.Empty : markers;
            Length = length;
            Diagnostics = diagnostics.IsDefault ? ImmutableArray<string>.Empty : diagnostics;
        }
    }

    /// <summary>A point on placed track geometry in the direction of the main traversal.</summary>
    public readonly record struct PathChartSample
    {
        /// <summary>Cumulative travel distance in metres.</summary>
        public double Distance { get; init; }

        /// <summary>Placed track elevation in metres.</summary>
        public double Elevation { get; init; }

        /// <summary>Local rise divided by horizontal run, in percent; positive uphill.</summary>
        public double GradePercent { get; init; }

        /// <summary>Signed reciprocal curve radius per kilometre; positive right in travel direction.</summary>
        public double CurvaturePerKilometre { get; init; }

        /// <summary>Exact location on the placed section geometry.</summary>
        public WorldLocation Location { get; init; }

        /// <summary>Initializes a geometry sample.</summary>
        public PathChartSample(double distance, double elevation, double gradePercent, double curvaturePerKilometre,
            WorldLocation location)
        {
            Distance = distance;
            Elevation = elevation;
            GradePercent = gradePercent;
            CurvaturePerKilometre = curvaturePerKilometre;
            Location = location;
        }
    }

    /// <summary>A station visit consisting of consecutive same-name platform encounters.</summary>
    public sealed record PathChartStationVisit
    {
        /// <summary>Unmodified source station name.</summary>
        public string StationName { get; init; }

        /// <summary>Midpoint of the traversed group interval, in metres.</summary>
        public double Distance { get; init; }

        /// <summary>Clipped start of the group interval, in metres.</summary>
        public double StartDistance { get; init; }

        /// <summary>Clipped end of the group interval, in metres.</summary>
        public double EndDistance { get; init; }

        /// <summary>Location on the traversal at <see cref="Distance"/>, not an average of platform coordinates.</summary>
        public WorldLocation Location { get; init; }

        /// <summary>Constituent platform occurrences, retained in travel order.</summary>
        public ImmutableArray<PathChartPlatformEncounter> Platforms { get; init; }

        /// <summary>Initializes a station visit.</summary>
        public PathChartStationVisit(string stationName, double distance, double startDistance, double endDistance,
            WorldLocation location, ImmutableArray<PathChartPlatformEncounter> platforms)
        {
            StationName = stationName;
            Distance = distance;
            StartDistance = startDistance;
            EndDistance = endDistance;
            Location = location;
            Platforms = platforms.IsDefault ? ImmutableArray<PathChartPlatformEncounter>.Empty : platforms;
        }
    }

    /// <summary>One proven, clipped traversal of a reciprocal platform pair.</summary>
    public readonly record struct PathChartPlatformEncounter
    {
        /// <summary>Source station name.</summary>
        public string StationName { get; init; }

        /// <summary>Source platform name.</summary>
        public string PlatformName { get; init; }

        /// <summary>Lower source endpoint item index, preserving pair identity across repeated visits.</summary>
        public int FirstPlatformItemIndex { get; init; }

        /// <summary>Other source endpoint item index.</summary>
        public int SecondPlatformItemIndex { get; init; }

        /// <summary>Midpoint of the traversed platform interval, in metres.</summary>
        public double Distance { get; init; }

        /// <summary>Clipped interval start, in metres.</summary>
        public double StartDistance { get; init; }

        /// <summary>Clipped interval end, in metres.</summary>
        public double EndDistance { get; init; }

        /// <summary>Location on the traversal at the interval midpoint.</summary>
        public WorldLocation Location { get; init; }

        /// <summary>Initializes a constituent platform occurrence.</summary>
        public PathChartPlatformEncounter(string stationName, string platformName, int firstPlatformItemIndex, int secondPlatformItemIndex, double distance, double startDistance, double endDistance, WorldLocation location)
        {
            StationName = stationName;
            PlatformName = platformName;
            FirstPlatformItemIndex = firstPlatformItemIndex;
            SecondPlatformItemIndex = secondPlatformItemIndex;
            Distance = distance;
            StartDistance = startDistance;
            EndDistance = endDistance;
            Location = location;
        }
    }

    /// <summary>A numeric marker encountered on the selected physical main traversal.</summary>
    public readonly record struct PathChartMarker
    {
        /// <summary>Cumulative travel distance in metres.</summary>
        public double Distance { get; init; }

        /// <summary>Unconverted source value: km/h or mph for speed, kilometres or miles for distance.</summary>
        public double Value { get; init; }

        /// <summary>Source marker category.</summary>
        public PathChartMarkerKind Kind { get; init; }

        /// <summary>Whether the source value is mph or miles, rather than km/h or kilometres.</summary>
        public bool IsMiles { get; init; }

        /// <summary>Location on the selected section geometry.</summary>
        public WorldLocation Location { get; init; }

        /// <summary>Initializes a numeric source marker.</summary>
        public PathChartMarker(double distance, double value, PathChartMarkerKind kind, bool isMiles, WorldLocation location)
        {
            Distance = distance;
            Value = value;
            Kind = kind;
            IsMiles = isMiles;
            Location = location;
        }
    }
}