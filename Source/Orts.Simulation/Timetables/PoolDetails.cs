// COPYRIGHT 2014 by the Open Rails project.
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

// This code processes the Timetable definition and converts it into playable train information
//
// #DEBUG_POOLINFO
//

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Models.Imported.State;

using Orts.Formats.Msts;
using Orts.Simulation.Track;

namespace Orts.Simulation.Timetables
{
    public class PoolDetails : ISaveStateApi<TimetablePoolDetailSaveState>
    {
        public TrackCircuitPartialPathRoute StoragePath { get; set; }    // path defined as storage location
        public Traveller StoragePathTraveller { get; set; }              // traveller used to get path position and direction
        public Traveller StoragePathReverseTraveller { get; set; }       // traveller used if path must be reversed
        public string StorageName { get; set; }                          // storage name
        public List<TrackCircuitPartialPathRoute> AccessPaths { get; set; }    // access paths defined for storage location
        public float StorageLength { get; set; }                         // available length
        public float StorageCorrection { get; set; }                     // length correction (e.g. due to switch overlap safety) - difference between length of sections in path and actual storage length

        public int TableExitIndex { get; set; }                          // index in table exit list for this exit
        public int TableVectorIndex { get; set; }                        // index in VectorList of tracknode which is the table
        public float TableMiddleEntry { get; set; }                      // offset of middle of moving table when approaching table (for turntable and transfertable)
        public float TableMiddleExit { get; set; }                       // offset of middle of moving table when exiting table (for turntable and transfertable)

        public List<int> StoredUnits { get; set; }                       // stored no. of units
        public List<int> ClaimUnits { get; set; }                        // units which have claimed storage but not yet in pool
        public int? MaxStoredUnits { get; set; }                         // max. no of stored units for storage track
        public float RemainingLength { get; set; }          // remaining storage length

        public async ValueTask Restore(TimetablePoolDetailSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            StoragePath = new TrackCircuitPartialPathRoute();
            await StoragePath.Restore(saveState.StoragePath).ConfigureAwait(false);

            StoragePathTraveller = new Traveller(false);
            await StoragePathTraveller.Restore(saveState.StoragePathTraveller).ConfigureAwait(false);
            StoragePathReverseTraveller = new Traveller(false);
            await StoragePathReverseTraveller.Restore(saveState.StoragePathReverseTraveller).ConfigureAwait(false);
            StorageName = saveState.StorageName;
            AccessPaths = (await Task.WhenAll(saveState.AccessPaths.Select(async accessPath =>
            {
                TrackCircuitPartialPathRoute accessPathRoute = new TrackCircuitPartialPathRoute();
                await accessPathRoute.Restore(accessPath).ConfigureAwait(false);
                return accessPathRoute;
            })).ConfigureAwait(false)).ToList();
            StoredUnits = new List<int>(saveState.StoredUnits);
            ClaimUnits = new List<int>(saveState.ClaimedUnits);
            StorageLength = saveState.StorageLength;
            StorageCorrection = saveState.StorageOffset;
            TableExitIndex = saveState.TableExitIndex;
            TableVectorIndex = saveState.TableVectorIndex;
            TableMiddleEntry = saveState.TableMiddleEntry;
            TableMiddleExit = saveState.TableMiddleExit;
            RemainingLength = saveState.RemainingLength;
            MaxStoredUnits = saveState.MaxStoredUnits;

        }

        public async ValueTask<TimetablePoolDetailSaveState> Snapshot()
        {
            return new TimetablePoolDetailSaveState()
            {
                StoragePath = await StoragePath.Snapshot().ConfigureAwait(false),
                StoragePathTraveller = await StoragePathTraveller.Snapshot().ConfigureAwait(false),
                StoragePathReverseTraveller = await StoragePathReverseTraveller.Snapshot().ConfigureAwait(false),
                StorageName = StorageName,
                AccessPaths = new Collection<TrackCircuitPartialPathRouteSaveState>(await Task.WhenAll(AccessPaths.Select(async accessPath => await accessPath.Snapshot().ConfigureAwait(false)).ToList()).ConfigureAwait(false)),
                StoredUnits = new Collection<int>(StoredUnits),
                ClaimedUnits = new Collection<int>(ClaimUnits),
                StorageLength = StorageLength,
                StorageOffset = StorageCorrection,
                TableExitIndex = TableExitIndex,
                TableVectorIndex = TableVectorIndex,
                TableMiddleEntry = TableMiddleEntry,
                TableMiddleExit = TableMiddleExit,
                RemainingLength = RemainingLength,
                MaxStoredUnits = MaxStoredUnits,
            };
        }
    }
}
