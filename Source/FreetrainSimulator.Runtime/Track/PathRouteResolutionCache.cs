using System.Threading;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Runtime.Track
{
    /// <summary>
    /// Single-entry cache for the resolution of a path model. <see cref="PathModel"/> is immutable, so every
    /// edit yields a new instance and reference equality is an exact test for "the path changed". Callers that
    /// need both the validation state and the route diagnostics of the same model therefore resolve only once.
    /// </summary>
    /// <remarks>
    /// Not thread safe; intended to be owned by a single component (the path editor runs on the game thread).
    /// </remarks>
    public sealed class PathRouteResolutionCache
    {
        private PathModel cachedModel;
        private PathRouteResolution cachedResolution;

        /// <summary>
        /// Returns the resolution for <paramref name="pathModel"/>, resolving it only when it differs from the
        /// previously resolved model. Returns null for a null model.
        /// </summary>
        public PathRouteResolution Resolve(PathModel pathModel, TrackWorld trackWorld)
        {
            if (pathModel == null)
                return null;

            if (ReferenceEquals(pathModel, cachedModel))
                return cachedResolution;

            cachedResolution = PathRouteResolver.Resolve(pathModel, trackWorld, CancellationToken.None);
            cachedModel = pathModel;
            return cachedResolution;
        }

        /// <summary>
        /// Drops the cached resolution, forcing the next <see cref="Resolve"/> to resolve again. Needed when the
        /// underlying track data changed while the model instance stayed the same.
        /// </summary>
        public void Clear()
        {
            cachedModel = null;
            cachedResolution = null;
        }
    }
}
