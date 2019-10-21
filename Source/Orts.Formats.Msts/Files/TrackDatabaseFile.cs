// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

using System.Collections.Generic;
using System.Diagnostics;

using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Files
{
    /// <summary>
    /// TDBFile is a representation of the .tdb file, that contains the track data base.
    /// The database contains two kinds of items: TrackNodes and TrItems (Track Items).
    /// </summary>
    public class TrackDatabaseFile
    {
        /// <summary>
        /// Contains the Database with all the  tracks.
        /// </summary>
        public TrackDB TrackDB { get; private set; }

        /// <summary>
        /// Constructor from file
        /// </summary>
        /// <param name="filenamewithpath">Full file name of the .rdb file</param>
        public TrackDatabaseFile(string fileName)
        {
            using (STFReader stf = new STFReader(fileName, false))
                stf.ParseFile(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("trackdb", ()=>{ TrackDB = new TrackDB(stf); }),
                });
        }

    }

    /// <summary>
    /// This class represents the Track Database.
    /// </summary>
    public class TrackDB
    {
        private readonly Dictionary<string, TrackJunctionNode> junctionNodes = new Dictionary<string, TrackJunctionNode>();
        
        /// <summary>
        /// Array of all TrackNodes in the track database
        /// Warning, the first TrackNode is always null.
        /// </summary>
        public TrackNode[] TrackNodes { get; private set; }
        
        /// <summary>
        /// Array of all Track Items (TrItem) in the track database
        /// </summary>
        public TrackItem[] TrackItems { get; private set; }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        public TrackDB(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tracknodes", ()=>{
                    stf.MustMatch("(");
                    int numberOfTrackNodes = stf.ReadInt(null);
                    TrackNodes = new TrackNode[numberOfTrackNodes + 1];
                    int idx = 1;
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("tracknode", ()=>{
                            TrackNodes[idx] = TrackNode.ReadTrackNode(stf, idx, numberOfTrackNodes);
                            if (TrackNodes[idx] is TrackJunctionNode junctionNode)
                            {
                                string key = $"{junctionNode.UiD.WorldId}-{junctionNode.UiD.Location.TileX}-{junctionNode.UiD.Location.TileZ}";
                                if (!junctionNodes.ContainsKey(key))
                                    junctionNodes.Add(key, junctionNode);
                                // only need any (first) junction node with that key here to relate back to ShapeIndex
                            }
                            ++idx;
                        }),
                    });
                }),
                new STFReader.TokenProcessor("tritemtable", ()=>{
                    stf.MustMatch("(");
                    int numberOfTrItems = stf.ReadInt(null);
                    TrackItems = new TrackItem[numberOfTrItems];
                    int idx = -1;
                    stf.ParseBlock(()=> ++idx == -1, new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("crossoveritem", ()=>{ TrackItems[idx] = new CrossoverItem(stf,idx); }),
                        new STFReader.TokenProcessor("signalitem", ()=>{ TrackItems[idx] = new SignalItem(stf,idx); }),
                        new STFReader.TokenProcessor("speedpostitem", ()=>{ TrackItems[idx] = new SpeedPostItem(stf,idx); }),
                        new STFReader.TokenProcessor("platformitem", ()=>{ TrackItems[idx] = new PlatformItem(stf,idx); }),
                        new STFReader.TokenProcessor("soundregionitem", ()=>{ TrackItems[idx] = new SoundRegionItem(stf,idx); }),
                        new STFReader.TokenProcessor("emptyitem", ()=>{ TrackItems[idx] = new EmptyItem(stf,idx); }),
                        new STFReader.TokenProcessor("levelcritem", ()=>{ TrackItems[idx] = new LevelCrossingItem(stf,idx); }),
                        new STFReader.TokenProcessor("sidingitem", ()=>{ TrackItems[idx] = new SidingItem(stf,idx); }),
                        new STFReader.TokenProcessor("hazzarditem", ()=>{ TrackItems[idx] = new HazardItem(stf,idx); }),
                        new STFReader.TokenProcessor("pickupitem", ()=>{ TrackItems[idx] = new PickupItem(stf,idx); }),
                    });
                }),
            });
        }
        
        /// <summary>
        /// Add a number of TrItems (Track Items), created outside of the file, to the table of TrItems.
        /// This will also set the ID of the TrItems (since that gives the index in that array)
        /// </summary>
        /// <param name="trackItems">The array of new items.</param>
        public void AddTrackItems(TrackItem[] trackItems)
        {
            TrackItem[] tempTrackItems;

            if (TrackItems == null)
            {
                tempTrackItems = new TrackItem[trackItems.Length];
            }
            else
            {
                tempTrackItems = new TrackItem[TrackItems.Length + trackItems.Length];
                TrackItems.CopyTo(tempTrackItems, 0);
            }

            for (int i = 0; i < trackItems.Length; i++)
            {
                int newId = i + TrackItems.Length;
                trackItems[i].TrackItemId = (uint)newId;
                tempTrackItems[newId] = trackItems[i];
            }

            TrackItems = tempTrackItems;
        }

        /// <summary>
        /// Provide a link to the TrJunctionNode for the switch track with 
        /// the specified UiD on the specified tile.
        /// 
        /// Called by switch track shapes to determine the correct position of the points.
        /// </summary>
        /// <param name="tileX">X-value of the current Tile</param>
        /// <param name="tileZ">Z-value of the current Tile</param>
        /// <param name="worldId">world ID as defined in world file</param>
        /// <returns>The TrackJunctionNode corresponding the the tile and worldID, null if not found</returns>
        public TrackJunctionNode GetJunctionNode(int tileX, int tileZ, int worldId)
        {
            if (!junctionNodes.TryGetValue($"{worldId}-{tileX}-{tileZ}", out TrackJunctionNode result))
                Trace.TraceWarning("{{TileX:{0} TileZ:{1}}} track node {2} could not be found in TDB", tileX, tileZ, worldId);
            return result;
        }
    }

}
