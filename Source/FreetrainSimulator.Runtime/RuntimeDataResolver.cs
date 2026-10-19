using System;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Models.Shim;
using FreeTrainSimulator.Models.Signalling;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Runtime
{
    public class RuntimeDataResolver
    {
        public RouteModel RouteData { get; }
        public TrackSectionModel TrackSections { get; }
        public SignalConfigurationModel SignalConfiguration { get; }
        public Track.TrackWorld TrackWorld { get; }
        public bool MetricUnits { get; }
        public IRuntimeReferenceResolver RuntimeReferenceResolver { get; }

        public static RuntimeDataResolver Instance => GameService<RuntimeDataResolver>.Instance;

        public static RuntimeDataResolver GameInstance(Game game) => GameService<RuntimeDataResolver>.Get(game);

        /// <summary>
        /// Prepares and publishes the runtime data for a route.
        /// </summary>
        public static async Task Initialize(RouteModel route, bool metricUnits, IRuntimeReferenceResolver runtimeReferenceResolver, CancellationToken cancellationToken)
        {
            RuntimeDataResolver resolver = await PrepareAsync(route, metricUnits, runtimeReferenceResolver, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            resolver.Publish();
        }

        /// <summary>
        /// Loads native route models and builds a track world without changing the published runtime services.
        /// </summary>
        /// <param name="route">The route whose native runtime models are loaded.</param>
        /// <param name="metricUnits">Whether the runtime uses metric units.</param>
        /// <param name="runtimeReferenceResolver">The runtime reference resolver, or <see langword="null"/> when unavailable.</param>
        /// <param name="cancellationToken">Cancels preparation without canceling shared cached model reads.</param>
        /// <returns>The prepared resolver, ready for an explicit call to <see cref="Publish"/>.</returns>
        public static async Task<RuntimeDataResolver> PrepareAsync(RouteModel route, bool metricUnits, IRuntimeReferenceResolver runtimeReferenceResolver, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(route);
            cancellationToken.ThrowIfCancellationRequested();

            TrackSectionModel trackSectionModel = await route.GetTrackSectionModel(CancellationToken.None).WaitAsync(cancellationToken).ConfigureAwait(false);
            TrackModel trackModel = await route.GetTrackModel(CancellationToken.None).WaitAsync(cancellationToken).ConfigureAwait(false);
            SignalConfigurationModel signalConfigurationModel = await route.GetSignalConfigurationModel(CancellationToken.None).WaitAsync(cancellationToken).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();
            Track.TrackWorld trackWorld = Track.TrackWorld.Create(trackModel, trackSectionModel);
            cancellationToken.ThrowIfCancellationRequested();

            return new RuntimeDataResolver(route, trackSectionModel, signalConfigurationModel, trackWorld, metricUnits, runtimeReferenceResolver);
        }

        /// <summary>
        /// Synchronously publishes this resolver's track world followed by this resolver as the process-wide runtime services.
        /// </summary>
        public void Publish()
        {
            _ = GameService<Track.TrackWorld>.Set(null, TrackWorld);
            _ = GameService<RuntimeDataResolver>.Set(null, this);
        }

        protected RuntimeDataResolver(RouteModel route, TrackSectionModel trackSectionModel, SignalConfigurationModel signalConfiguration, Track.TrackWorld trackWorld, bool useMetricUnits, IRuntimeReferenceResolver runtimeReferenceResolver)
        {
            RouteData = route;
            TrackSections = trackSectionModel;
            SignalConfiguration = signalConfiguration;
            TrackWorld = trackWorld;
            MetricUnits = useMetricUnits;
            RuntimeReferenceResolver = runtimeReferenceResolver;
        }

        protected RuntimeDataResolver()
        { }
    }
}
