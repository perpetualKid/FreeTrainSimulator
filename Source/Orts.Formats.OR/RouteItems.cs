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

using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Orts.Formats.Msts;
using Orts.Common;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace Orts.Formats.OR
{

    public enum AllowedDir
    {
        NONE = 0,
        IN = 1,
        OUT = 2,
        InOut = 3
    };

    #region JunctionItem
    /// <summary>
    /// Defines a junction being drawn in a 2D view.
    /// </summary>
    public class AEJunctionItem : GlobalItem
    {
        public uint main;
    }

    #endregion

    #region BufferItem
    public class AEBufferItem : GlobalItem
    {
        [JsonProperty("nameBuffer")]
        public string NameBuffer { get; set; }
        [JsonProperty("configured")]
        public bool Configured { get; set; }
        [JsonProperty("bufferId")]
        private int BufferId { get { return (int)associateNode.Index; } set { } }
        [JsonProperty("dirBuffer")]
        public AllowedDir DirBuffer { get; set; }
        [JsonIgnore]
        List<string> allowedDirections;
        [JsonIgnore]
        public StationPaths stationPaths { get; protected set; }
        [JsonIgnore]
        public StationItem parentStation { get; protected set; }
        [JsonConstructor]
        public AEBufferItem()
        {
            allowedDirections = new List<string>();
            allowedDirections.Add("   ");
            allowedDirections.Add("In");
            allowedDirections.Add("Out");
            allowedDirections.Add("Both");
            DirBuffer = AllowedDir.NONE;
            Configured = false;
            typeItem = (int)TypeItem.BUFFER_ITEM;
            //Coord = new MSTSCoord();
        }

        public override void alignEdition(TypeEditor interfaceType, GlobalItem ownParent)
        {
            if (interfaceType == TypeEditor.ROUTECONFIG)
            {
                setEditable();
                asMetadata = true;
            }
            parentStation = (StationItem)ownParent;
        }

        public List<StationPath> searchPaths(AETraveller myTravel, List<TrackSegment> listConnectors, MSTSItems aeItems, StationItem parent)
        {
            List<StationPath> paths;
            if (!Configured)
                return null;
            if (stationPaths == null)
            {
                stationPaths = new StationPaths();
            }
            stationPaths.Clear();
            paths = stationPaths.explore(myTravel, listConnectors, aeItems, parent);
            return paths;
        }

        public void highlightTrackFromArea(MSTSItems aeItems)
        {
            if (!Configured)
                return;

            if (stationPaths != null)
            {
                stationPaths.highlightTrackFromArea(aeItems);
            }
        }
    }
    #endregion

    #region SideItem

    /// <summary>
    /// Defines a siding sideItem  (platform, difing or passing)
    /// SideStartItem is the place where the Siding Label is attached
    /// SideEndItem is the end place
    /// </summary>
    /// 
    public class SideItem : GlobalItem
    {
        public string Name;
        public float sizeSiding;
        public TrItem trItem{ get; protected set; }
        public TrItem.trItemType type { get { return trItem.ItemType; } protected set { } }
        public float icoAngle;
        public int typeSiding;
    }

    #endregion
}
