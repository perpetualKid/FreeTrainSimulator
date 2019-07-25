// COPYRIGHT 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using Newtonsoft.Json;
using Orts.Formats.Msts;
using Orts.Common;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Orts.Formats.OR
{
    /// <summary>
    /// ORRouteConfig is the main class to access the OpenRail specific data for a route.  These data complete the MSTS one in terms of Station
    /// and Station's connectors to track.
    /// The data are saved in json file into the main repository of the route.
    /// </summary>

    public class ORRouteConfig
    {
        [JsonProperty("FileName")]
        public string FileName;
        [JsonProperty("RoutePath")]
        public string RoutePath { get; protected set; }
        [JsonProperty("RouteName")]
        public string RouteName { get; protected set; }
        [JsonProperty("GenAuxAction")]
        public ActionContainer ActionContainer = new ActionContainer();

        [JsonIgnore]
        public List<GlobalItem> AllItems { get; protected set; }       //  All the items, include the activity items, exclude the MSTS Item, not saved
        [JsonIgnore]
        public bool toSave = false;
        [JsonIgnore]
        public AETraveller traveller { get; protected set; }

        /// <summary>
        /// The class constructor, but, don't use it.  Prefer to use the static method 'LoadConfig' wich return this object
        /// </summary>
        public ORRouteConfig()
        {
            AllItems = new List<GlobalItem>();
            RouteName = "";
        }

        static public ORRouteConfig LoadConfig(string fileName, string path)
        {
            string completeFileName = Path.Combine(path, fileName);
            ORRouteConfig loaded = DeserializeJSON(completeFileName);
            return loaded;
        }

        static public ORRouteConfig DeserializeJSON(string fileName)
        {
            ORRouteConfig p;

            fileName += ".cfg.json";
            //try
            //{
                // TODO: This code is BROKEN. It loads and saves file formats with internal type information included, which causes breakages if the types are moved. This is not acceptable for public, shared data.
                //JsonSerializer serializer = new JsonSerializer();
                //using (StreamReader sr = new StreamReader(fileName))
                //{
                //    ORRouteConfig orRouteConfig = JsonConvert.DeserializeObject<ORRouteConfig>((string)sr.ReadToEnd(), new JsonSerializerSettings
                //    {
                //        PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                //        TypeNameHandling = TypeNameHandling.Auto
                //    });
                //    p = orRouteConfig;
                    
                //    foreach (var item in p.routeItems)
                //    {
                //        p.AllItems.Add(item);
                //        item.alignEdition(interfaceType, null);
                //        if (item.GetType() == typeof(StationItem))
                //        {
                //            if (((StationItem)item).stationArea.Count > 0)
                //            {
                //                foreach (var item2 in ((StationItem)item).stationArea)
                //                {
                //                    ((StationAreaItem)item2).alignEdition(interfaceType, item);
                //                }
                //                ((StationItem)item).areaCompleted = true;
                //            }
                //        }
                //        else if (item.GetType() == typeof(AEBufferItem))
                //        {
                //        }
                //    }
                //    //orRouteConfig.ReduceItems();
                //}
                //
            //}
            //catch (IOException)
            //{
                p = new ORRouteConfig();
                p.FileName = Path.GetFileName(fileName);
                p.RoutePath = Path.GetDirectoryName(fileName);
                p.RouteName = "";
                p.toSave = true;

            //}
            return p;
        }

        public void SetTraveller(TrackSectionsFile TSectionDat, TrackDatabaseFile TDB)
        {
            TrackNode[] TrackNodes = TDB.TrackDB.TrackNodes;
            traveller = new AETraveller(TSectionDat, TDB);
        }

        /// <summary>
        /// Scan the current orRouteConfig and search for items related to the given node
        /// </summary>
        /// <param name="iNode">The current node index</param>
        /// <param name="orRouteConfig">The Open Rail configuration coming from Editor</param>
        /// <param name="trackNodes">The list of MSTS Track Nodes</param>
        /// <param name="tsectiondat">The list of MSTS Section datas</param>
        public List<TrackCircuitElement> GetORItemForNode(int iNode, TrackNode[] trackNodes, TrackSectionsFile tsectiondat)
        {
            List<TrackCircuitElement> trackCircuitElements = new List<TrackCircuitElement>();
            if (AllItems.Count <= 0)
                return trackCircuitElements;
            foreach (var item in AllItems)
            {
                switch (item.typeItem)
                {
                    case (int)TypeItem.STATION_CONNECTOR:
                        if (item.associateNodeIdx != iNode)
                            continue;
                        TrackNode node = trackNodes[iNode];
                        AETraveller travel = new AETraveller(traveller);
                        travel.place(node);
                        float position = travel.DistanceTo(item);
                        TrackCircuitElement element = (TrackCircuitElement)new TrackCircuitElementConnector(item, position);
                        trackCircuitElements.Add(element);
                        break;
                    default:
                        break;
                }
            }

            return trackCircuitElements;
        }
    }
}
