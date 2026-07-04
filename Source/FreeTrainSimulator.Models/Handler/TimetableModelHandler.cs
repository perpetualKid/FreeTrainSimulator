using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;

namespace FreeTrainSimulator.Models.Handler
{
    /// <summary>
    /// Handler for timetable models. Loads individual <see cref="TimetableModel"/> instances
    /// from disk and enumerates all timetable sets available for a route.
    /// </summary>
    internal class TimetableModelHandler : ContentHandlerBase<TimetableModel>
    {
        public static Task<TimetableModel> GetCore(TimetableModel timetableModel, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(timetableModel, nameof(timetableModel));
            return GetCore(timetableModel.Id, timetableModel.Parent, cancellationToken);
        }

        public static Task<TimetableModel> GetCore(string timetableId, RouteModelHeader routeModel, CancellationToken cancellationToken)
            => GetOrAddCore(timetableId, routeModel, cancellationToken);

        public static Task<ImmutableArray<TimetableModel>> GetTimetables(RouteModelHeader routeModel, CancellationToken cancellationToken)
            => GetOrAddCollection(routeModel, cancellationToken);
    }
}
