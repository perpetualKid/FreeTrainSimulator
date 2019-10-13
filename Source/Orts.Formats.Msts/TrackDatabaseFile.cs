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

using Microsoft.Xna.Framework;
using Orts.Common;
using Orts.Common.Calc;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Orts.Formats.Msts
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
                                junctionNodes.Add($"{junctionNode.UiD.WorldId}-{junctionNode.UiD.Location.TileX}-{junctionNode.UiD.Location.TileZ}", junctionNode);
                            }
                            //TrackNodes[idx] = new TrackNode(stf, idx, numberOfTrackNodes); 
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
                        new STFReader.TokenProcessor("levelcritem", ()=>{ TrackItems[idx] = new LevelCrItem(stf,idx); }),
                        new STFReader.TokenProcessor("sidingitem", ()=>{ TrackItems[idx] = new SidingItem(stf,idx); }),
                        new STFReader.TokenProcessor("hazzarditem", ()=>{ TrackItems[idx] = new HazzardItem(stf,idx); }),
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
                trackItems[i].TrItemId = (uint)newId;
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

    /// <summary>
    /// Represents a TrackNode. This is either an endNode, a junctionNode, or a vectorNode. 
    /// A VectorNode is a connection between two junctions or endnodes.
    /// </summary>
    public abstract class TrackNode
    {
        //only used to determine the node type during parsing
        private enum NodeType
        {
            TrJunctionNode,
            TrVectorNode,
            TrEndNode,
        }

        /// <summary>'Universal Id', containing location information. Only provided for TrJunctionNode and TrEndNode type of TrackNodes</summary>
        public UiD UiD { get; private set; }
        /// <summary>The array containing the TrPins (Track pins), which are connections to other tracknodes</summary>
        public TrackPin[] TrackPins;
        /// <summary>Number of outgoing pins (connections to other tracknodes)</summary>
        public int InPins { get; private set; }
        /// <summary>Number of outgoing pins (connections to other tracknodes)</summary>
        public int OutPins { get; private set; }

        /// <summary>The index in the array of tracknodes.</summary>
        public uint Index { get; private set; }

        /// <summary>
        /// List of references to Track Circuit sections
        /// </summary>
        public TrackCircuitXRefList TrackCircuitCrossReferences { get; set; }

        protected TrackNode(STFReader stf, uint index, int maxTrackNode, int expectedPins)
        {
            Index = index;
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("uid", ()=>{ UiD = ReadUiD(stf); }),
                new STFReader.TokenProcessor("trjunctionnode", ()=>{ ReadNodeData(stf); }),
                new STFReader.TokenProcessor("trvectornode", ()=>{ ReadNodeData(stf); }),
                new STFReader.TokenProcessor("trendnode", ()=>{ ReadNodeData(stf); }),
                new STFReader.TokenProcessor("trpins", () => ReadPins(stf, maxTrackNode)),
            });
            if (TrackPins.Length < expectedPins)
                Trace.TraceWarning("Track node {0} has unexpected number of pins; expected {1}, got {2}", Index, expectedPins, TrackPins.Length);
        }

        protected abstract void ReadNodeData(STFReader stf);

        protected UiD ReadUiD(STFReader stf)
        {
            return new UiD(stf);
        }

        protected void ReadPins(STFReader stf, int maxTrackNode)
        {
            stf.MustMatch("(");
            InPins = stf.ReadInt(null);
            OutPins = stf.ReadInt(null);
            TrackPins = new TrackPin[InPins + OutPins];
            for (int i = 0; i < TrackPins.Length; ++i)
            {
                stf.MustMatch("TrPin");
                TrackPins[i] = new TrackPin(stf);
                if (TrackPins[i].Link <= 0 || TrackPins[i].Link > maxTrackNode)
                    STFException.TraceWarning(stf, $"Track node {Index} pin {i} has invalid link to track node {TrackPins[i].Link}");
            }
            stf.SkipRestOfBlock();
        }

        internal static TrackNode ReadTrackNode(STFReader stf, int expectedIndex, int maxNodeIndex)
        {
            stf.MustMatch("(");
            uint index = stf.ReadUInt(null);
            Debug.Assert(index == expectedIndex, "TrackNode Index Mismatch");
            if (!EnumExtension.GetValue(stf.ReadString(), out NodeType nodeType))
            {
                throw new STFException(stf, "Unknown TrackNode type");
            }
            stf.StepBackOneItem();
            switch (nodeType)
            {
                case NodeType.TrEndNode:
                    return new TrackEndNode(stf, index, maxNodeIndex);
                case NodeType.TrJunctionNode:
                    return new TrackJunctionNode(stf, index, maxNodeIndex);
                case NodeType.TrVectorNode:
                    return new TrackVectorNode(stf, index, maxNodeIndex);
            }
            return null;
        }
    }

    /// <summary>
    /// This TrackNode has nothing else connected to it (that is, it is
    /// a buffer end or an unfinished track) and trains cannot proceed beyond here.
    /// </summary>
    [DebuggerDisplay("\\{MSTS.TrEndNode\\}")]
    public class TrackEndNode : TrackNode
    {
        public TrackEndNode(STFReader stf, uint index, int maxTrackNode):
            base(stf, index, maxTrackNode, 1)
        {
        }

        protected override void ReadNodeData(STFReader stf)
        {
            stf.MustMatch("(");
            stf.SkipRestOfBlock();
        }
    }

    [DebuggerDisplay("\\{MSTS.TrJunctionNode\\} SelectedRoute={SelectedRoute}, ShapeIndex={ShapeIndex}")]
    public class TrackJunctionNode: TrackNode
    {
        /// <summary>
        /// The route of a switch that is currently in use.
        /// </summary>
        public int SelectedRoute { get; set; }

        /// <summary>
        /// Index to the shape that actually describes the looks of this switch
        /// </summary>
        public uint ShapeIndex { get; set; }

        /// <summary>The angle of this junction</summary>
        private float angle = float.MaxValue;

        public TrackJunctionNode(STFReader stf, uint index, int maxTrackNode):
            base(stf, index, maxTrackNode, 3)
        {
        }

        protected override void ReadNodeData(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ReadString();
            ShapeIndex = stf.ReadUInt(null);
            stf.SkipRestOfBlock();
        }

        /// <summary>
        /// Calculate the angle (direction in 2D) of the current junction (result will be cached).
        /// </summary>
        /// <param name="tsectionDat">The datafile with all the track sections</param>
        /// <returns>The angle calculated</returns>
        public float GetAngle(TrackSectionsFile tsectionDat)
        {
            if (angle != float.MaxValue)
                return angle;

            try //so many things can be in conflict for trackshapes, tracksections etc.
            {
                TrackShape trackShape = tsectionDat.TrackShapes[ShapeIndex];
                SectionIndex[] sectionIndices = trackShape.SectionIndices;

                for (int index = 0; index < sectionIndices.Length; index++)
                {
                    if (index == trackShape.MainRoute)
                        continue;
                    uint[] sections = sectionIndices[index].TrackSections;

                    for (int i = 0; i < sections.Length; i++)
                    {
                        uint sid = sectionIndices[index].TrackSections[i];
                        TrackSection section = tsectionDat.TrackSections[sid];

                        if (section.Curved)
                        {
                            angle = section.Angle;
                            break;
                        }
                    }
                }
            }
            catch (Exception) { }
            return angle;
        }
    }

    /// <summary>
    /// Describes the details of a vectorNode, a connection between two junctions (or endnodes).
    /// A vectorNode itself is made up of various sections. The begin point of each of these sections
    /// is stored (as well as its direction). As a result, VectorNodes have a direction.
    /// Furthermore, a number of TrItems (Track Items) can be located on the vector nodes.
    /// </summary>
    [DebuggerDisplay("\\{MSTS.TrVectorNode\\} TrVectorSections={TrVectorSections?.Length ?? null}, NoItemRefs={NoItemRefs}")]
    public class TrackVectorNode : TrackNode
    {
        private static readonly int[] emptyTrackItemIndices = new int[0];
        /// <summary>Array of sections that together form the vectorNode</summary>
        public TrVectorSection[] TrackVectorSections { get; private set; }
        /// <summary>Array of indexes of TrItems (track items) that are located on this vectorNode</summary>
        public int[] TrackItemIndices { get; private set; } = emptyTrackItemIndices;
        /// <summary>The amount of TrItems in TrItemRefs</summary>

        public TrackVectorNode(STFReader stf, uint index, int maxTrackNode):
            base(stf, index, maxTrackNode, 2)
        {
        }

        protected override void ReadNodeData(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("trvectorsections", ()=>{
                    stf.MustMatch("(");
                    int numberOfVectorSections = stf.ReadInt(null);
                    TrackVectorSections = new TrVectorSection[numberOfVectorSections];
                    for (int i = 0; i < numberOfVectorSections; ++i)
                        TrackVectorSections[i] = new TrVectorSection(stf);
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("tritemrefs", ()=>{
                    stf.MustMatch("(");
                    int expectedItems = stf.ReadInt(null);
                    TrackItemIndices = new int[expectedItems];
                    int index = 0;
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("tritemref", ()=>{
                            if (index >= TrackItemIndices.Length)
                            {
                                STFException.TraceWarning(stf, $"Adding extra TrItemRef in TrVectorNode {Index}. Expected Items: {expectedItems}");
                                int[] temp = new int[index+1];
                                TrackItemIndices.CopyTo(temp,0);
                                TrackItemIndices = temp;
                            }
                            TrackItemIndices[index++] = stf.ReadIntBlock(null);
                        }),
                    });
                    if (index < expectedItems)
                        STFException.TraceWarning(stf, $"{(expectedItems - index)} missing TrItemRef(s) in TrVectorNode {Index}");
                }),
            });
        }

        /// <summary>
        /// Get the index of a vector section in the array of vectorsections 
        /// </summary>
        /// <param name="targetTvs">The vector section for which the index is needed</param>
        /// <returns>the index of the vector section</returns>
        public int TrVectorSectionsIndexOf(TrVectorSection targetTvs)
        {
            for (int i = 0; i < TrackVectorSections.Length; ++i)
            {
                if (TrackVectorSections[i] == targetTvs)
                {
                    return i;
                }
            }
            throw new InvalidOperationException("Program Bug: Can't Find TVS");
        }

        /// <summary>
        /// Add a reference to a new TrItem to the already existing TrItemRefs.
        /// </summary>
        /// <param name="newTrItemRef">The reference to the new TrItem</param>
        public void AddTrackItemIndex(int trackItemIndex)
        {
            int[] temp = new int[TrackItemIndices.Length + 1];
            TrackItemIndices.CopyTo(temp, 0);
            temp[temp.Length - 1] = trackItemIndex;
            TrackItemIndices = temp; //use the new item lists for the track node
        }

        /// <summary>
        /// Add a reference to a new TrItem to the already existing TrItemRefs.
        /// </summary>
        /// <param name="newTrItemRef">The reference to the new TrItem</param>
        public void InsertTrackItemIndex(int trackItemIndex, int index)
        {
            List<int> temp = new List<int>(TrackItemIndices);
            temp.Insert(index, trackItemIndex);
            TrackItemIndices = temp.ToArray(); //use the new item lists for the track node
        }
    }

    #region class TrackPin
    /// <summary>
    /// Represents a pin, being the link from a tracknode to another. 
    /// </summary>
    [DebuggerDisplay("\\{MSTS.TrPin\\} Link={Link}, Dir={Direction}")]
    public class TrackPin
    {
        /// <summary>Index of the tracknode connected to the parent of this pin</summary>
        public int Link { get; set; }
        /// <summary>In case a connection is made to a vector node this determines the side of the vector node that is connected to</summary>
        public int Direction { get; set; }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        public TrackPin(STFReader stf)
        {
            stf.MustMatch("(");
            Link = stf.ReadInt(null);
            Direction = stf.ReadInt(null);
            stf.SkipRestOfBlock();
        }

        public TrackPin(int link, int direction)
        {
            Link = link;
            Direction = direction;
        }
    }
    #endregion

    /// <summary>
    /// Contains the location and initial direction (as an angle in 3 dimensions) of a node (junction or end),
    /// as well as a cross reference to the entry in the world file
    /// </summary>
    //[DebuggerDisplay("\\{MSTS.UiD\\} ID={WorldID}, TileX={location.TileX}, TileZ={location.TileZ}, X={location.Location.X}, Y={location.Location.Y}, Z={location.Location.Z}, AX={AX}, AY={AY}, AZ={AZ}, WorldX={WorldTileX}, WorldZ={WorldTileZ}")]
    [DebuggerDisplay("\\{MSTS.UiD\\} ID={WorldId}, TileX={location.TileX}, TileZ={location.TileZ}, X={location.Location.X}, Y={location.Location.Y}, Z={location.Location.Z}")]
    public class UiD
    {
        private readonly WorldLocation location;
        public ref readonly WorldLocation Location => ref location;

        ///// <summary>Angle around X-axis for describing initial direction of the node</summary>
        //public float AX { get; set; }
        ///// <summary>Angle around Y-axis for describing initial direction of the node</summary>
        //public float AY { get; set; }
        ///// <summary>Angle around Z-axis for describing initial direction of the node</summary>
        //public float AZ { get; set; }

        ///// <summary>Cross-reference to worldFile: X-value of the tile</summary>
        //public int WorldTileX { get; set; }
        ///// <summary>Cross-reference to worldFile: Y-value of the tile</summary>
        //public int WorldTileZ { get; set; }
        /// <summary>Cross-reference to worldFile: World ID</summary>
        public int WorldId { get; set; }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        public UiD(STFReader stf)
        {
            stf.MustMatch("(");
            int worldTileX = stf.ReadInt(null);
            int worldTileZ = stf.ReadInt(null);
            WorldId = stf.ReadInt(null);
            stf.ReadInt(null);
            location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null), stf.ReadFloat(null), stf.ReadFloat(null), stf.ReadFloat(null));
            if (worldTileX != location.TileX || worldTileZ != location.TileZ)
                STFException.TraceInformation(stf, $"Inconsistent WorldTile information in UiD node {WorldId}: WorldTileX({worldTileX}), WorldTileZ({worldTileZ}), Location.TileX({location.TileX}), Location.TileZ({location.TileZ})");
            //AX = stf.ReadFloat(STFReader.Units.None, null);
            //AY = stf.ReadFloat(STFReader.Units.None, null);
            //AZ = stf.ReadFloat(STFReader.Units.None, null);
            stf.SkipRestOfBlock();
        }        
    }

    /// <summary>
    /// Describes the details of a vectorNode, a connection between two junctions (or endnodes).
    /// A vectorNode itself is made up of various sections. The begin point of each of these sections
    /// is stored (as well as its direction). As a result, VectorNodes have a direction.
    /// Furthermore, a number of TrItems (Track Items) can be located on the vector nodes.
    /// </summary>
    public class TrVectorNode
    {
        /// <summary>Array of sections that together form the vectorNode</summary>
        public TrVectorSection[] TrVectorSections;
        /// <summary>Array of indexes of TrItems (track items) that are located on this vectorNode</summary>
        public int[] TrItemRefs;
        /// <summary>The amount of TrItems in TrItemRefs</summary>
        public int NoItemRefs { get; set; } // it would have been better to use TrItemRefs.Length instead of keeping count ourselve

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        public TrVectorNode(STFReader stf)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("trvectorsections", ()=>{
                    stf.MustMatch("(");
                    int numberOfVectorSections = stf.ReadInt(null);
                    TrVectorSections = new TrVectorSection[numberOfVectorSections];
                    for (int i = 0; i < numberOfVectorSections; ++i)
                        TrVectorSections[i] = new TrVectorSection(stf);
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("tritemrefs", ()=>{
                    stf.MustMatch("(");
                    NoItemRefs = stf.ReadInt(null);
                    TrItemRefs = new int[NoItemRefs];
                    int refidx = 0;
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("tritemref", ()=>{
                            if (refidx >= NoItemRefs)
                                STFException.TraceWarning(stf, "Skipped extra TrItemRef");
                            else
                                TrItemRefs[refidx++] = stf.ReadIntBlock(null);
                        }),
                    });
                    if (refidx < NoItemRefs)
                        STFException.TraceWarning(stf, (NoItemRefs - refidx).ToString(System.Globalization.CultureInfo.CurrentCulture)
                            + " missing TrItemRef(s)");
                }),
            });
        }

        /// <summary>
        /// Create a vectorNode from a another VectorNode, by copying all members and arrays.
        /// Not a deep copy, because the arrays are copied shallow.
        /// </summary>
        /// <param name="otherNode">The other node to copy from.</param>
        public TrVectorNode(TrVectorNode otherNode)
        {
            this.NoItemRefs = otherNode.NoItemRefs;
            if (otherNode.TrItemRefs != null)
                this.TrItemRefs = (int[])otherNode.TrItemRefs.Clone();
            if (otherNode.TrVectorSections != null)
                this.TrVectorSections = (TrVectorSection[])otherNode.TrVectorSections.Clone();
        }

        /// <summary>
        /// Get the index of a vector section in the array of vectorsections 
        /// </summary>
        /// <param name="targetTvs">The vector section for which the index is needed</param>
        /// <returns>the index of the vector section</returns>
        public int TrVectorSectionsIndexOf(TrVectorSection targetTvs)
        {
            for (int i = 0; i < TrVectorSections.Length; ++i)
            {
                if (TrVectorSections[i] == targetTvs)
                {
                    return i;
                }
            }
            throw new InvalidOperationException("Program Bug: Can't Find TVS");
        }

        /// <summary>
        /// Add a reference to a new TrItem to the already existing TrItemRefs.
        /// </summary>
        /// <param name="newTrItemRef">The reference to the new TrItem</param>
        public void AddTrItemRef(int newTrItemRef)
        {
            int[] newTrItemRefs = new int[NoItemRefs + 1];
            TrItemRefs.CopyTo(newTrItemRefs, 0);
            newTrItemRefs[NoItemRefs] = newTrItemRef;
            TrItemRefs = newTrItemRefs; //use the new item lists for the track node
            NoItemRefs++;
        }
    }

    /// <summary>
    /// Describes a single section in a vector node. 
    /// </summary>
    public class TrVectorSection
    {
        public WorldLocation Location => new WorldLocation(TileX, TileZ, X, Y, Z);//TODO
        /// <summary>First flag. Not completely clear, usually 0, - may point to the connecting pin entry in a junction. Sometimes 2</summary>
        public int Flag1 { get; set; }
        /// <summary>Second flag. Not completely clear, usually 1, but set to 0 when curve track is flipped around. Sometimes 2</summary>
        public int Flag2 { get; set; }
        /// <summary>Index of the track section in Tsection.dat</summary>
        public uint SectionIndex { get; set; }
        /// <summary>Index to the shape from Tsection.dat</summary>
        public uint ShapeIndex { get; set; }
        /// <summary>X-value of the location-tile</summary>
        public int TileX { get; set; }
        /// <summary>Z-value of the location-tile</summary>
        public int TileZ { get; set; }
        /// <summary>X-value within the tile where the node is located</summary>
        public float X { get; set; }
        /// <summary>Y-value (height) within the tile where the node is located</summary>
        public float Y { get; set; }
        /// <summary>Z-value within the tile where the node is located</summary>
        public float Z { get; set; }
        /// <summary>Angle around X-axis for describing initial direction of the node</summary>
        public float AX { get; set; }
        /// <summary>Angle around Y-axis for describing initial direction of the node</summary>
        public float AY { get; set; }
        /// <summary>Angle around Z-axis for describing initial direction of the node</summary>
        public float AZ { get; set; }

        //The following items are related to super elevation
        /// <summary>The index to the worldFile</summary>
        public uint WorldFileUiD { get; set; }
        /// <summary>The TileX in the WorldFile</summary>
        public int WFNameX { get; set; }
        /// <summary>The TileZ in the WorldFile</summary>
        public int WFNameZ { get; set; }
        /// <summary>The (super)elevation at the start</summary>
        public float StartElev { get; set; }
        /// <summary>The (super)elevation at the end</summary>
        public float EndElev { get; set; }
        /// <summary>The maximum (super) elevation</summary>
        public float MaxElev { get; set; }

        /// <summary>??? (needed for ActivityEditor, but not used here, so why is it defined here?)</summary>
        public bool Reduced { get; set; }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        public TrVectorSection(STFReader stf)
        {
            SectionIndex = stf.ReadUInt(null);
            ShapeIndex = stf.ReadUInt(null);
            WFNameX = stf.ReadInt(null);// worldfilenamex
            WFNameZ = stf.ReadInt(null);// worldfilenamez
            WorldFileUiD = stf.ReadUInt(null); // UID in worldfile
            Flag1 = stf.ReadInt(null); // 0
            Flag2 = stf.ReadInt(null); // 1
            stf.ReadString(); // 00 
            TileX = stf.ReadInt(null);
            TileZ = stf.ReadInt(null);
            X = stf.ReadFloat(STFReader.Units.None, null);
            Y = stf.ReadFloat(STFReader.Units.None, null);
            Z = stf.ReadFloat(STFReader.Units.None, null);
            AX = stf.ReadFloat(STFReader.Units.None, null);
            AY = stf.ReadFloat(STFReader.Units.None, null);
            AZ = stf.ReadFloat(STFReader.Units.None, null);
        }

        /// <summary>
        /// Overriding the ToString, which makes it easier to debug
        /// </summary>
        /// <returns>String giving info on this section</returns>
        public override string ToString()
        {
            return String.Format(System.Globalization.CultureInfo.CurrentCulture,
                "{{TileX:{0} TileZ:{1} X:{2} Y:{3} Z:{4} UiD:{5} Section:{6} Shape:{7}}}", WFNameX, WFNameZ, X, Y, Z, WorldFileUiD, SectionIndex, ShapeIndex);
        }
    }

    /// <summary>
    /// Describes a Track Item, that is an item located on the track that interacts with the train or train operations
    /// This is a base class. 
    /// </summary>
    public abstract class TrackItem
    {
        protected WorldLocation location;
        /// <summary>
        /// The name of the item (used for the label shown by F6)
        /// </summary>
        public string ItemName { get; protected set; }

        public ref readonly WorldLocation Location => ref location;

        /// <summary>Id if track item</summary>
        public uint TrItemId { get; protected internal set; }
        /// <summary>X-value of world tile</summary>
        public int TileX => location.TileX;
        /// <summary>Z-value of world tile</summary>
        public int TileZ => location.TileZ;
        /// <summary>X-location within world tile (tracknode, not shape)</summary>
        public float X => location.Location.X;
        /// <summary>X-location within world tile (tracknode, not shape)</summary>
        public float Y => location.Location.Y;
        /// <summary>X-location within world tile (tracknode, not shape)</summary>
        public float Z => location.Location.Z;
        /// <summary>Appears to be a copy of tileX in Sdata, but only for X and Z</summary>
        public int TilePX { get; protected set; }
        /// <summary>Appears to be a copy of tileZ in Sdata, but only for X and Z</summary>
        public int TilePZ { get; protected set; }
        /// <summary>Appears to be a copy of X in Sdata, but only for X and Z</summary>
        public float PX { get; protected set; }
        /// <summary>Appears to be a copy of X in Sdata, but only for X and Z</summary>
        public float PZ { get; protected set; }
        /// <summary>Extra data 1, related to location along section</summary>
        public float SData1 { get; protected set; }
        /// <summary>Extra data 2</summary>
        public string SData2 { get; protected set; }

        /// <summary>
        /// Base constructor
        /// </summary>
        protected TrackItem()
        {
        }

        /// <summary>
        /// Reads the ID from filestream
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="index">The index of this TrItem in the list of TrItems</param>
        protected void ParseTrackItemId(STFReader stf, int index)
        {
            stf.MustMatch("(");
            TrItemId = stf.ReadUInt(null);
            Debug.Assert(index == TrItemId, "Index Mismatch");
            stf.SkipRestOfBlock();
        }
        
        /// <summary>
        /// Reads the Rdata from filestream
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        protected void TrackItemRData(STFReader stf)
        {
            stf.MustMatch("(");
            float x = stf.ReadFloat(null);
            float y = stf.ReadFloat(null);
            float z = stf.ReadFloat(null);
            location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null), x, y, z);
            stf.SkipRestOfBlock();
        }

        /// <summary>
        /// Reads the PData from filestream
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        protected void TrItemPData(STFReader stf)
        {
            stf.MustMatch("(");
            PX = stf.ReadFloat(STFReader.Units.None, null);
            PZ = stf.ReadFloat(STFReader.Units.None, null);
            TilePX = stf.ReadInt(null);
            TilePZ = stf.ReadInt(null);
            stf.SkipRestOfBlock();
        }

        /// <summary>
        /// Reads the SData from filestream
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        protected void TrItemSData(STFReader stf)
        {
            stf.MustMatch("(");
            SData1 = stf.ReadFloat(STFReader.Units.None, null);
            SData2 = stf.ReadString();
            stf.SkipRestOfBlock();
        }
    } // TrItem

    /// <summary>
    /// Describes a cross-over track item
    /// <summary>A place where two tracks cross over each other</summary>
    /// </summary>
    public class CrossoverItem : TrackItem
    {
        /// <summary>Index to the tracknode</summary>
        public uint TrackNode { get; set; }
        /// <summary>Index to the shape ID</summary>
        public uint ShapeId { get; set; }
        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        public CrossoverItem(STFReader stf, int idx)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("crossovertritemdata", ()=>{
                    stf.MustMatch("(");
                    TrackNode = stf.ReadUInt(null);
                    ShapeId = stf.ReadUInt(null);
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }

    /// <summary>
    /// Describes a signal item
    /// </summary>
    public class SignalItem : TrackItem
    {
        /// <summary>
        /// Struct to describe details of the signal for junctions
        /// </summary>
        public struct StrTrSignalDir
        {
            /// <summary>Index to the junction track node</summary>
            public uint TrackNode { get; set; }
            /// <summary>Used with junction signals, appears to be either 1 or 0</summary>
            public uint Sd1 { get; set; }
            /// <summary>Used with junction signals, appears to be either 1 or 0</summary>
            public uint LinkLRPath { get; set; }
            /// <summary>Used with junction signals, appears to be either 1 or 0</summary>
            public uint Sd3 { get; set; }
        }

        /// <summary>Set to  00000001 if junction link set</summary>
        public string Flags1 { get; set; }
        /// <summary>0 or 1 depending on which way signal is facing</summary>
        public uint Direction { get; set; }
        /// <summary>index to Sigal Object Table</summary>
        public int SigObj { get; set; }
        /// <summary>Signal Data 1</summary>
        public float SigData1 { get; set; }
        /// <summary>Type of signal</summary>
        public string SignalType { get; set; }
        /// <summary>Number of junction links</summary>
        public uint NoSigDirs { get; set; }
        /// <summary></summary>
        public StrTrSignalDir[] TrSignalDirs;

        /// <summary>Get the direction the signal is NOT facing</summary>
        public int ReverseDirection
        {
            get { return Direction == 0 ? 1 : 0; }
        }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        public SignalItem(STFReader stf, int idx)
        {
            SigObj = -1;
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("trsignaltype", ()=>{
                    stf.MustMatch("(");
                    Flags1 = stf.ReadString();
                    Direction = stf.ReadUInt(null);
                    SigData1 = stf.ReadFloat(STFReader.Units.None, null);
                    SignalType = stf.ReadString().ToLowerInvariant();
                    // To do get index to Sigtypes table corresponding to this sigmal
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("trsignaldirs", ()=>{
                    stf.MustMatch("(");
                    NoSigDirs = stf.ReadUInt(null);
                    TrSignalDirs = new StrTrSignalDir[NoSigDirs];
                    int sigidx = 0;
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("trsignaldir", ()=>{
                            if (sigidx >= NoSigDirs)
                                STFException.TraceWarning(stf, "Skipped extra TrSignalDirs");
                            else
                            {
                                TrSignalDirs[sigidx]=new StrTrSignalDir();
                                stf.MustMatch("(");
                                TrSignalDirs[sigidx].TrackNode = stf.ReadUInt(null);
                                TrSignalDirs[sigidx].Sd1 = stf.ReadUInt(null);
                                TrSignalDirs[sigidx].LinkLRPath = stf.ReadUInt(null);
                                TrSignalDirs[sigidx].Sd3 = stf.ReadUInt(null);
                                stf.SkipRestOfBlock();
                                sigidx++;
                            }
                        }),
                    });
                    if (sigidx < NoSigDirs)
                        STFException.TraceWarning(stf, (NoSigDirs - sigidx).ToString(System.Globalization.CultureInfo.CurrentCulture)
                            + " missing TrSignalDirs(s)");
                }),
            });
        }
    }

    /// <summary>
    /// Describes SpeedPost of MilePost (could be Kilometer post as well)
    /// </summary>
    public class SpeedPostItem : TrackItem
    {
        /// <summary>Flags from raw file describing exactly what this is.</summary>
        private uint Flags { get; set; }
        /// <summary>true to be milepost</summary>
        public bool IsMilePost { get; set; }
        /// <summary>speed warning</summary>
        public bool IsWarning { get; set; }
        /// <summary>speed limit</summary>
        public bool IsLimit { get; set; }
        /// <summary>speed resume sign (has no speed defined!)</summary>
        public bool IsResume { get; set; }
        /// <summary>is passenger speed limit</summary>
        public bool IsPassenger { get; set; }
        /// <summary>is freight speed limit</summary>
        public bool IsFreight { get; set; }
        /// <summary>is the digit in MPH or KPH</summary>
        public bool IsMPH { get; set; }
        /// <summary>show numbers instead of KPH, like 5 means 50KMH</summary>
        public bool ShowNumber { get; set; }
        /// <summary>if ShowNumber is true and this is set, will show 1.5 as for 15KMH</summary>
        public bool ShowDot { get; set; }
        /// <summary>Or distance if mile post.</summary>
        public float SpeedInd { get; set; }

        /// <summary>index to Signal Object Table</summary>
        public int SigObj { get; set; }
        /// <summary>speedpost (normalized) angle</summary>
        public float Angle { get; set; }
        /// <summary>derived direction relative to track</summary>
        public int Direction { get; set; }
        /// <summary>number to be displayed if ShowNumber is true</summary>
        public int DisplayNumber { get; set; }

        /// <summary>Get the direction the signal is NOT facing</summary>
        public int ReverseDirection
        {
            get { return Direction == 0 ? 1 : 0; }
        }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        public SpeedPostItem(STFReader stf, int idx)
        {
            SigObj = -1;
            stf.MustMatch("(");
			stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("speedposttritemdata", ()=>{
                    stf.MustMatch("(");
                    Flags = stf.ReadUInt(null);
					if ((Flags & 1) != 0) IsWarning = true;
					if ((Flags & (1 << 1)) != 0) IsLimit = true;
					if (!IsWarning && !IsLimit) {
						IsMilePost = true;
					}
					else {
						if (IsWarning && IsLimit)
						{
							IsWarning = false;
							IsResume = true;
						}

						if ((Flags & (1 << 5)) != 0) IsPassenger = true;
						if ((Flags & (1 << 6)) != 0) IsFreight = true;
						if ((Flags & (1 << 7)) != 0) IsFreight = IsPassenger = true;
						if ((Flags & (1 << 8)) != 0) IsMPH = true;
						if ((Flags & (1 << 4)) != 0) {
							ShowNumber = true;
							if ((Flags & (1 << 9)) != 0) ShowDot = true;
						}
					}


                    //  The number of parameters depends on the flags seeting
                    //  To do: Check flags seetings and parse accordingly.
		            if (!IsResume)
		            {
                        //SpeedInd = stf.ReadFloat(STFReader.Units.None, null);
                        if (IsMilePost && ((Flags & (1 << 9)) == 0)) SpeedInd = (float)Math.Truncate(stf.ReadDouble(null));
                        else SpeedInd = stf.ReadFloat(STFReader.Units.None, null);
		            }

    		        if (ShowNumber)
		            {
			            DisplayNumber = stf.ReadInt(null);
		            }
                    
			        Angle = MathHelper.WrapAngle(stf.ReadFloat(STFReader.Units.None, null));

                    stf.SkipRestOfBlock();
                }),
            });
        }

        // used as base for TempSpeedPostItem
        public SpeedPostItem()
        { }
    }

    public class TempSpeedPostItem : SpeedPostItem
    {      
        /// <summary>
        /// Constructor for creating a speedpost from activity speed restriction zone
        /// </summary>
        /// <param name="routeFile">The routeFile with relevant data about speeds</param>
        /// <param name="position">Position/location of the speedposts</param>
        /// <param name="isStart">Is this the start of a speed zone?</param>
        /// 
        public WorldPosition WorldPosition;

        public TempSpeedPostItem(Tr_RouteFile routeFile, in WorldLocation location,  bool isStart, in WorldPosition worldPosition, bool isWarning)
        {
            // TrItemId needs to be set later
            WorldPosition = worldPosition;
            CreateRPData(location);

            IsMilePost = false;
            IsLimit = true;
            IsFreight = IsPassenger = true;
            IsWarning = isWarning;

            if (!isStart) { IsLimit = true; IsResume = true; }//end zone
            float speed = routeFile.TempRestrictedSpeed;
            if (speed < 0) speed = Speed.MeterPerSecond.FromKpH(25); //todo. Value is not used. Should it be used below instead of TempRestrictedSpeed? And if so, is the +0.01 then still needed?
            if (routeFile.MilepostUnitsMetric == true)
            {
                this.IsMPH = false;
                SpeedInd = (int)(Speed.MeterPerSecond.ToKpH(routeFile.TempRestrictedSpeed) + 0.1f); 
            }
            else
            {
                this.IsMPH = true;
                SpeedInd = (int)(Speed.MeterPerSecond.ToMpH(routeFile.TempRestrictedSpeed) + 0.1f);
            }

            Angle = 0;
        }

        /// <summary>
        /// Create the R P data from a position
        /// </summary>
        /// <param name="position">Position of the speedpost</param>
        private void CreateRPData(in WorldLocation location)
        {
            this.location = location;
            PX = location.Location.X;
            PZ = location.Location.Z;
            TilePX = location.TileX;
            TilePZ = location.TileZ;
        }

        public void Update(float y, float angle, in WorldPosition position)
        {
            this.location = location.SetElevation(y);
            Angle = angle;
            WorldPosition = position;
        }
    }

    /// <summary>
    /// Represents a region where a sound can be played.
    /// </summary>
    public class SoundRegionItem : TrackItem
    {
        /// <summary>Sound region data 1</summary>
        public uint SRData1 { get; set; }
        /// <summary>Sound region data 2</summary>
        public uint SRData2 { get; set; }
        /// <summary>Sound region data 3</summary>
        public float SRData3 { get; set; }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        public SoundRegionItem(STFReader stf, int idx)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("tritemsrdata", ()=>{
                    stf.MustMatch("(");
                    SRData1 = stf.ReadUInt(null);
                    SRData2 = stf.ReadUInt(null);
                    SRData3 = stf.ReadFloat(STFReader.Units.None, null);
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }

    /// <summary>
    /// represent an empty item (which probably should only happen for badly defined routes?)
    /// </summary>
    public class EmptyItem : TrackItem
    {
        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        public EmptyItem(STFReader stf, int idx)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
            });
        }
    }

    /// <summary>
    /// Representa a level Crossing item (so track crossing road)
    /// </summary>
    public class LevelCrItem : TrackItem
    {
        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        public LevelCrItem(STFReader stf, int idx)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),
            });
        }
    }

    /// <summary>
    /// Represents either start or end of a siding.
    /// </summary>
    public class SidingItem : TrackItem
    {
        /// <summary>Flags 1 for a siding ???</summary>
        public string Flags1 { get; set; }
        /// <summary>Flags 2 for a siding, probably the index of the other end of the siding.</summary>
        public uint LinkedSidingId { get; set; }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        public SidingItem(STFReader stf, int idx)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("sidingname", ()=>{ ItemName = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("sidingtritemdata", ()=> {
                    stf.MustMatch("(");
                    Flags1 = stf.ReadString();
                    LinkedSidingId = stf.ReadUInt(null);
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }
    
    /// <summary>
    /// Represents either start or end of a platform (a place where trains can stop).
    /// </summary>
    public class PlatformItem : TrackItem
    {

        /// <summary>Name of the station where the platform is</summary>
        public string Station { get; set; }
        /// <summary>Flags 1 for a platform ???</summary>
        public string Flags1 { get; set; }
        /// <summary>Minimum waiting time at the platform</summary>
        public uint PlatformMinWaitingTime { get; set; }
        /// <summary>Number of passengers waiting at the platform</summary>
        public uint PlatformNumPassengersWaiting { get; set; }
        /// <summary>TrItem Id of the other end of the platform</summary>
        public uint LinkedPlatformItemId { get; set; }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        public PlatformItem(STFReader stf, int idx)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

                new STFReader.TokenProcessor("platformname", ()=>{ ItemName = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("station", ()=>{ Station = stf.ReadStringBlock(""); }),
                new STFReader.TokenProcessor("platformminwaitingtime", ()=>{ PlatformMinWaitingTime = stf.ReadUIntBlock(null); }),
                new STFReader.TokenProcessor("platformnumpassengerswaiting", ()=>{ PlatformNumPassengersWaiting = stf.ReadUIntBlock(null); }),
                new STFReader.TokenProcessor("platformtritemdata", ()=>{
                    stf.MustMatch("(");
                    Flags1 = stf.ReadString();
                    LinkedPlatformItemId = stf.ReadUInt(null);
                    stf.SkipRestOfBlock();
                }),
            });
        }

        /// <summary>
        /// Constructor to create Platform Item out of Siding Item
        /// </summary>
        /// <param name="thisSiding">The siding to use for a platform creation</param>
        public PlatformItem(SidingItem thisSiding)
        {
            TrItemId = thisSiding.TrItemId;
            SData1 = thisSiding.SData1;
            SData2 = thisSiding.SData2;
            ItemName = thisSiding.ItemName;
            Flags1 = thisSiding.Flags1;
            LinkedPlatformItemId = thisSiding.LinkedSidingId;
            Station = String.Copy(ItemName);
        }
    }

    /// <summary>
    /// Represends a hazard, a place where something more or less dangerous happens
    /// </summary>
    public class HazzardItem : TrackItem
    {
        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        public HazzardItem(STFReader stf, int idx)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

            });
        }
    }

    /// <summary>
    /// Represents a pickup, a place to pickup fuel, water, ...
    /// </summary>
    public class PickupItem : TrackItem
    {
        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        /// <param name="idx">The index of this TrItem in the list of TrItems</param>
        public PickupItem(STFReader stf, int idx)
        {
            stf.MustMatch("(");
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tritemid", ()=>{ ParseTrackItemId(stf, idx); }),
                new STFReader.TokenProcessor("tritemrdata", ()=>{ TrackItemRData(stf); }),
                new STFReader.TokenProcessor("tritemsdata", ()=>{ TrItemSData(stf); }),
                new STFReader.TokenProcessor("tritempdata", ()=>{ TrItemPData(stf); }),

            });
        }
    }

    #region CrossReference to TrackCircuitSection
    /// <summary>
    /// To make it possible for a MSTS (vector) TrackNode to have information about the TrackCircuitSections that
    /// represent that TrackNode, this class defines the basic information of a single of these TrackCircuitSections.
    /// </summary>
    public class TrackCircuitSectionXref
    {
        /// <summary>full length</summary>
        public float Length { get; set; }
        /// <summary>Offset length in orig track section, for either forward or backward direction</summary>
        public float[] OffsetLength;
        /// <summary>index of TrackCircuitSection</summary>
        public int Index { get; set; }

        /// <summary>
        /// Constructor
        /// </summary>
        public TrackCircuitSectionXref()
        {
            //Offset indicates length from end of original tracknode, Index 0 is forward, index 1 is backward wrt original tracknode direction.
            OffsetLength = new float[2];
        }

        /// <summary>
        /// Constructor and setting reference, length and offset length from section
        /// </summary>
        /// <param name="sectionIndex"></param>
        /// <param name="sectionLength"></param>
        public TrackCircuitSectionXref(int sectionIndex, float sectionLength, float[] sectionOffsetLength)
        {
            Index = sectionIndex;
            Length = sectionLength;
            OffsetLength = new float[2];
            OffsetLength[0] = sectionOffsetLength[0];
            OffsetLength[1] = sectionOffsetLength[1];
        }
    }

    /// <summary>
    /// Class to make it possible for a MSTS (vector) TrackNode to have information about the TrackCircuitSections that
    /// represent that TrackNode.
    /// </summary>
    public class TrackCircuitXRefList : List<TrackCircuitSectionXref>
    {
        /// <summary>
        /// The tracksections form together a representation of a vector node. Once you give a direction along that vector
        /// and the offset from the start, get the index of the TrackCircuitSectionXref at that location
        /// </summary>
        /// <param name="offset">Offset along the vector node where we want to find the tracksection</param>
        /// <param name="direction">Direction where we start measuring along the vector node</param>
        /// <returns>Index in the current list of crossreferences</returns>
        public int GetXRefIndex(float offset, int direction)
        {
            if (direction == 0)
            {   // search forward, start at the second one (first one should have offsetlength zero
                for (int TC = 1; TC < this.Count; TC++)
                {
                    if (this[TC].OffsetLength[direction] > offset)
                    {
                        return (TC - 1);
                    }
                }

                // not yet found, try the last one
                TrackCircuitSectionXref thisReference = this[this.Count - 1];
                if (offset <= (thisReference.OffsetLength[direction] + thisReference.Length))
                {
                    return (this.Count - 1);
                }

                //really not found, return the first one
                return (0);
            }
            else
            {   // search backward, start at last -1 (because last should end at vector node end anyway
                for (int TC = this.Count - 2; TC >= 0; TC--)
                {
                    if (this[TC].OffsetLength[direction] > offset)
                    {
                        return (TC + 1);
                    }
                }

                //not yet found, try the first one.
                TrackCircuitSectionXref thisReference = this[0];
                if (offset <= (thisReference.OffsetLength[direction] + thisReference.Length))
                {
                    return (0);
                }

                //really not found, return the last one
                return (this.Count - 1);
            }
        }

        /// <summary>
        /// The tracksections form together a representation of a vector node. Once you give a direction along that vector
        /// and the offset from the start, get the index of the TrackCircuitSection at that location
        /// </summary>
        /// <param name="offset">Offset along the vector node where we want to find the tracksection</param>
        /// <param name="direction">Direction where we start measuring along the vector node</param>
        /// <returns>Index of the section that is at the wanted location</returns>
        public int GetSectionIndex(float offset, int direction)
        {
            int XRefIndex = GetXRefIndex(offset, direction);

            if (XRefIndex >= 0)
            {
                TrackCircuitSectionXref thisReference = this[XRefIndex];
                return (thisReference.Index);
            }
            else
            {
                return (-1);
            }
        }
    } // class TrackCircuitXRefList
    #endregion
}
