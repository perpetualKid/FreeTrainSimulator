using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;

namespace Orts.TrackViewer
{
    public class TrackData
    {
        private readonly string routePath;

        public string RouteName { get; private set; }

        public TrackDB TrackDB { get; private set; }

        public RoadTrackDB RoadTrackDB { get; private set; }

        public TrackSectionsFile TrackSections { get; private set; }

        public SignalConfigurationFile SignalConfig { get; private set; }

        public bool UseMetricUnits { get; private set; }

        public TrackData(string routePath)
        {
            this.routePath = routePath;
        }

        internal async Task LoadTrackData(bool? useMetricUnits)
        {
            List<Task> loadTasks = new List<Task>();

            FolderStructure.ContentFolder.RouteFolder routeFolder = FolderStructure.Route(routePath);
            RouteFile routeFile = new RouteFile(routeFolder.TrackFileName);
            RouteName = routeFile.Route.Name;
            UseMetricUnits = useMetricUnits.GetValueOrDefault(routeFile.Route.MilepostUnitsMetric);

            loadTasks.Add(Task.Run(() => TrackDB = new TrackDatabaseFile(routeFolder.TrackDatabaseFile(routeFile)).TrackDB));
            loadTasks.Add(Task.Run(() =>
            {
                TrackSections = new TrackSectionsFile(routeFolder.TrackSectionFile);
                if (File.Exists(routeFolder.RouteTrackSectionFile))
                    TrackSections.AddRouteTSectionDatFile(routeFolder.RouteTrackSectionFile);
            }));
            loadTasks.Add(Task.Run(() => RoadTrackDB = new RoadDatabaseFile(routeFolder.RoadTrackDatabaseFile(routeFile)).RoadTrackDB));
            loadTasks.Add(Task.Run(() => SignalConfig = new SignalConfigurationFile(routeFolder.SignalConfigurationFile, routeFolder.ORSignalConfigFile)));

            await Task.WhenAll(loadTasks).ConfigureAwait(false);
        }
    }
}
