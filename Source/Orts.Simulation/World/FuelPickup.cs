// COPYRIGHT 2012, 2013 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team.

using System;
using System.Collections.Generic;
using System.Linq;

using FreeTrainSimulator.Common.Position;

using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.World
{
    public class FuelManager
    {
        public Dictionary<int, FuelPickupItem> FuelPickupItems { get; }

        public FuelManager(Simulator simulator)
        {
            ArgumentNullException.ThrowIfNull(simulator);
            FuelPickupItems = RuntimeData.Instance.TrackDB != null ? 
                GetFuelPickupItemsFromDB(RuntimeData.Instance.TrackDB.TrackNodes, RuntimeData.Instance.TrackDB.TrackItems) : new Dictionary<int, FuelPickupItem>();
        }

        private static Dictionary<int, FuelPickupItem> GetFuelPickupItemsFromDB(TrackNodes trackNodes, IList<TrackItem> trItemTable)
        {
            return (from trackNode in trackNodes
                    where trackNode is TrackVectorNode tvn && tvn.TrackItemIndices.Length > 0
                    from itemRef in (trackNode as TrackVectorNode)?.TrackItemIndices.Distinct()
                    where trItemTable[itemRef] is not null and PickupItem
                    select new KeyValuePair<int, FuelPickupItem>(itemRef, new FuelPickupItem(trackNode, trItemTable[itemRef])))
                    .ToDictionary(_ => _.Key, _ => _.Value);
        }

        public FuelPickupItem CreateFuelStation(in WorldPosition position, IEnumerable<int> trackIds)
        {
            FuelPickupItem[] trackItems = trackIds.Select(id => FuelPickupItems[id]).ToArray();
            return new FuelPickupItem(trackItems);
        }

        public void Update()
        { }
    }

    //public class FuelPickupItem
    //{
    //    internal WorldLocation Location;
    //    private readonly TrackNode TrackNode;

    //    public FuelPickupItem(TrackNode trackNode, TrackItem trItem)
    //    {
    //        TrackNode = trackNode;
    //        Location = trItem.Location;
    //    }

    //    public FuelPickupItem(IEnumerable<FuelPickupItem> items) { }

    //    public bool ReFill()
    //    {
    //        while (MSTSWagon.RefillProcess.OkToRefill)
    //        {
    //            return true;
    //        }
    //        if (!MSTSWagon.RefillProcess.OkToRefill)
    //            return false;
    //        return false;
    //    }
    //}

    public class FuelPickupItem
    {
        private protected readonly WorldLocation location;

        internal ref readonly WorldLocation Location => ref location;
        public TrackNode TrackNode { get; protected set; }

        public FuelPickupItem(TrackNode trackNode, in WorldLocation location)
        {
            TrackNode = trackNode;
            this.location = location;
        }

        public FuelPickupItem(TrackNode trackNode, TrackItem trackItem)
        {
            ArgumentNullException.ThrowIfNull(trackItem);
            TrackNode = trackNode;
            location = trackItem.Location;
        }

        public FuelPickupItem(IEnumerable<FuelPickupItem> items) 
        { }

        public static bool ReFill()
        {
            while (MSTSWagon.RefillProcess.OkToRefill)
                return true;
            if (!MSTSWagon.RefillProcess.OkToRefill)
                return false;
            return false;
        }
    }
}
