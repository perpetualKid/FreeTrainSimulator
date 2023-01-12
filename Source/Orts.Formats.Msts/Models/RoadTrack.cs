using System.Collections.Generic;

using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    /// <summary>
    /// This class represents the Road Track Database. This is pretty similar to the (rail) Track Database. So for more details see there
    /// </summary>
	public class RoadTrackDB
    {
        /// <summary>
        /// Array of all TrackNodes in the road database
        /// Warning, the first TrackNode is always null.
        /// </summary>
#pragma warning disable CA1002 // Do not expose generic lists
        public TrackNodes TrackNodes { get; private set; }

        /// <summary>
        /// Array of all Track Items (TrItem) in the road database
        /// </summary>
        public List<TrackItem> TrackItems { get; private set; }
#pragma warning restore CA1002 // Do not expose generic lists

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        internal RoadTrackDB(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tracknodes", ()=>{
                    stf.MustMatchBlockStart();
                    int count = stf.ReadInt(null);
                    TrackNodes = new TrackNodes(count + 1) { null };
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("tracknode", ()=>{ TrackNodes.Add(TrackNode.ReadTrackNode(stf, TrackNodes.Count, count)); }),
                    });
                }),
                new STFReader.TokenProcessor("tritemtable", ()=>{
                    stf.MustMatchBlockStart();
                    int count = stf.ReadInt(null);
                    TrackItems = new List<TrackItem>(count);
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("levelcritem", ()=>{ TrackItems.Add(new RoadLevelCrossingItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("emptyitem", ()=>{ TrackItems.Add(new EmptyItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("carspawneritem", ()=>{ TrackItems.Add(new RoadCarSpawnerItem(stf, TrackItems.Count)); })
                    });
                }),
            });
        }
    }

    /// <summary>
    /// Represents a Level crossing Item on the road (i.e. where cars must stop when a train is passing).
    /// </summary>
    public class RoadLevelCrossingItem : TrackItem
    {
        ///// <summary>Direction along track: 0 or 1 depending on which way signal is facing</summary>
        //public uint Direction { get; private set; }
        ///// <summary>index to Signal Object Table</summary>
        //public int SignalObject { get; private set; }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        internal RoadLevelCrossingItem(STFReader stf, int idx)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ ParseTrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ ParseTrackItemSData(stf); }),
            });
        }
    }

    /// <summary>
    /// Represent a Car Spawner: the place where cars start to appear or disappear again
    /// </summary>
	public class RoadCarSpawnerItem : TrackItem
    {
        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
		internal RoadCarSpawnerItem(STFReader stf, int idx)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ ParseTrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ ParseTrackItemSData(stf); })
            });
        }
    }
}
