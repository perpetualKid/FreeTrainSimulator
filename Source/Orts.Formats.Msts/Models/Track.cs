using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Windows.Forms;

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class TrackSection
    {
        public int SectionIndex { get; protected set; }

        //straight segment
        public float Width { get; private set; } = 1.5f;
        public float Length { get; private set; }

        //curved segment
        public bool Curved { get; private set; }
        public float Radius { get; private set; }    // meters
        public float Angle { get; private set; }	// degrees

        internal TrackSection(STFReader stf, bool routeTrackSection)
        {
            if (routeTrackSection)
            {
                stf.MustMatchBlockStart();
                stf.MustMatch("SectionCurve");
                stf.SkipBlock();
                SectionIndex = (int)stf.ReadUInt(null);

                float a = stf.ReadFloat(STFReader.Units.Distance, null);
                float b = stf.ReadFloat(STFReader.Units.None, null);
                if (b == 0) // Its straight
                    Length = a;
                else // its curved
                {
                    Radius = b;
                    Angle = MathHelper.ToDegrees(a);
                    Curved = true;
                    Length = Radius * Math.Abs(a);
                }
                stf.SkipRestOfBlock();
            }
            else
            {
                stf.MustMatchBlockStart();
                SectionIndex = (int)stf.ReadUInt(null);
                stf.ParseBlock(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("sectionsize", ()=>{ ReadSectionSize(stf); }),
                    new STFReader.TokenProcessor("sectioncurve", ()=>{ ReadSectionCurve(stf); }),
                });
                //if( SectionSize == null )
                //	throw( new STFError( stf, "Missing SectionSize" ) );
                //  note- default TSECTION.DAT does have some missing sections
            }
        }

        /// <summary>
        /// TrackSection from WorldFile
        /// </summary>
        /// <param name="block"></param>
        internal TrackSection(SBR block)
        {
            // TrackSection  ==> :SectionCurve :uint,UiD :float,param1 :float,param2
            // SectionCurve  ==> :uint,isCurved
            // eg:  TrackSection (
            //	       SectionCurve ( 1 ) 40002 -0.3 120
            //      )
            // isCurve = 0 for straight, 1 for curved
            // param1 = length (m) for straight, arc (radians) for curved
            // param2 = 0 for straight, radius (m) for curved

            block.VerifyID(TokenID.TrackSection);
            using (SBR subBlock = block.ReadSubBlock())
            {
                subBlock.VerifyID(TokenID.SectionCurve);
                Curved = subBlock.ReadUInt() != 0;
                subBlock.VerifyEndOfBlock();
            }
            SectionIndex = (int)block.ReadUInt();
            if (Curved)
            {
                Angle = block.ReadFloat();
                Radius = block.ReadFloat();
            }
            else
            {
                Length = block.ReadFloat();
                block.ReadFloat();
                Radius = -1f;
            }
            block.VerifyEndOfBlock();
        }

        private void ReadSectionSize(STFReader stf)
        {
            stf.MustMatchBlockStart();
            Width = stf.ReadFloat(STFReader.Units.Distance, null);
            Length = stf.ReadFloat(STFReader.Units.Distance, null);
            stf.SkipRestOfBlock();
        }

        private void ReadSectionCurve(STFReader stf)
        {
            Curved = true;
            stf.MustMatchBlockStart();
            Radius = stf.ReadFloat(STFReader.Units.Distance, null);
            Angle = stf.ReadFloat(STFReader.Units.None, null);
            Length = Radius * Math.Abs(MathHelper.ToRadians(Angle));
            stf.SkipRestOfBlock();
        }
    }

    public class TrackSections : Dictionary<int, TrackSection>
    {
        public int MaxSectionIndex { get; private set; }

        internal TrackSections(STFReader stf)
        {
            AddRouteStandardTrackSections(stf);
        }

        internal void AddRouteStandardTrackSections(STFReader stf)
        {
            stf.MustMatchBlockStart();
            MaxSectionIndex = stf.ReadInt(null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tracksection", ()=>{ AddSection(stf, new TrackSection(stf, false)); }),
            });
        }

        internal void AddRouteTrackSections(STFReader stf)
        {
            stf.MustMatchBlockStart();
            MaxSectionIndex = stf.ReadInt(null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tracksection", ()=>{ AddSection(stf, new TrackSection(stf, true)); }),
            });
        }

        private void AddSection(STFReader stf, TrackSection section)
        {
            if (ContainsKey(section.SectionIndex))
                STFException.TraceWarning(stf, "Replaced existing TrackSection " + section.SectionIndex);
            this[section.SectionIndex] = section;
        }

        public static int MissingTrackSectionWarnings { get; private set; }

        public TrackSection TryGet(int targetSectionIndex)
        {
            if (!TryGetValue(targetSectionIndex, out TrackSection trackSection) && MissingTrackSectionWarnings++ < 5)
                Trace.TraceWarning("Skipped track section {0} not in global or dynamic TSECTION.DAT", targetSectionIndex);
            return trackSection;
        }
    }

    public class SectionIndex
    {
        private readonly Vector3 offset;
        public uint SectionsCount { get; private set; }
        public ref readonly Vector3 Offset => ref offset;
        public float AngularOffset { get; private set; }  // Angular offset 
#pragma warning disable CA1819 // Properties should not return arrays
        public int[] TrackSections { get; private set; }
#pragma warning restore CA1819 // Properties should not return arrays

        internal SectionIndex(STFReader stf)
        {
            stf.MustMatchBlockStart();
            SectionsCount = stf.ReadUInt(null);
            offset = new Vector3(stf.ReadFloat(null), stf.ReadFloat(null), -stf.ReadFloat(null));
            AngularOffset = stf.ReadFloat(null);
            TrackSections = new int[SectionsCount];
            for (int i = 0; i < SectionsCount; ++i)
            {
                string token = stf.ReadString();
                if (token == ")")
                {
                    STFException.TraceWarning(stf, "Missing track section");
                    return;   // there are many TSECTION.DAT's with missing sections so we will accept this error
                }
                if (!uint.TryParse(token, out uint trackSection))
                    STFException.TraceWarning(stf, "Invalid track section " + token);
                else
                    TrackSections[i] = (int)trackSection;
            }
            stf.SkipRestOfBlock();
        }
    }

    [DebuggerDisplay("TrackShape {ShapeIndex}")]
    public class TrackShape
    {
        public int ShapeIndex { get; private set; }
        public string FileName { get; private set; }
        public int PathsNumber { get; private set; }
        public int MainRoute { get; private set; }
        public double ClearanceDistance { get; private set; }
#pragma warning disable CA1819 // Properties should not return arrays
        public SectionIndex[] SectionIndices { get; private set; }
#pragma warning restore CA1819 // Properties should not return arrays
        public bool TunnelShape { get; private set; }
        public bool RoadShape { get; private set; }

        internal TrackShape(STFReader stf)
        {
            stf.MustMatchBlockStart();
            ShapeIndex = stf.ReadInt(null);
            int nextPath = 0;
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("filename", ()=>{ FileName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("numpaths", ()=>{ SectionIndices = new SectionIndex[PathsNumber = stf.ReadIntBlock(null)]; }),
                new STFReader.TokenProcessor("mainroute", ()=>{ MainRoute = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("clearancedist", ()=>{ ClearanceDistance = stf.ReadFloatBlock(STFReader.Units.Distance, null); }),
                new STFReader.TokenProcessor("sectionidx", ()=>{ SectionIndices[nextPath++] = new SectionIndex(stf); }),
                new STFReader.TokenProcessor("tunnelshape", ()=>{ TunnelShape = stf.ReadBoolBlock(true); }),
                new STFReader.TokenProcessor("roadshape", ()=>{ RoadShape = stf.ReadBoolBlock(true); }),
            });
            // TODO - this was removed since TrackShape( 183 ) is blank
            //if( FileName == null )	throw( new STFError( stf, "Missing FileName" ) );
            //if( SectionIdxs == null )	throw( new STFError( stf, "Missing SectionIdxs" ) );
            //if( NumPaths == 0 ) throw( new STFError( stf, "No Paths in TrackShape" ) );
        }
    }

    public class TrackShapes : Dictionary<int, TrackShape>
    {

        public int MaxShapeIndex { get; private set; }

        internal TrackShapes(STFReader stf)
        {
            AddRouteTrackShapes(stf);
        }

        internal void AddRouteTrackShapes(STFReader stf)
        {
            stf.MustMatchBlockStart();
            MaxShapeIndex = stf.ReadInt(null);
            stf.ParseBlock(new STFReader.TokenProcessor[]
            {
                new STFReader.TokenProcessor("trackshape", ()=>{ Add(stf, new TrackShape(stf)); }),
            });
        }

        private void Add(STFReader stf, TrackShape trackShape)
        {
            if (ContainsKey(trackShape.ShapeIndex))
                STFException.TraceWarning(stf, "Replaced duplicate TrackShape " + trackShape.ShapeIndex);
            this[trackShape.ShapeIndex] = trackShape;
        }
    }

    public class TrackPaths : Dictionary<int, TrackPath> //SectionIdx in the route's tsection.dat
    {
        internal TrackPaths(STFReader stf)
        {
            stf.MustMatchBlockStart();
            uint sectionNumber = stf.ReadUInt(null);
            //new Dictionary<uint, TrackPath>((int)sectionNumber);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("trackpath", ()=>{ AddPath(stf, new TrackPath(stf)); }),
            });
            stf.SkipRestOfBlock();
        }

        private void AddPath(STFReader stf, TrackPath path)
        {
            try
            {
                Add(path.DynamicSectionIndex, path);
            }
            catch (ArgumentException e)
            {
                STFException.TraceWarning(stf, "Warning: in route tsection.dat " + e.Message);
            }
        }
    }

    public class TrackPath //SectionIdx in the route's tsection.dat
    {

        public int DynamicSectionIndex { get; private set; }
#pragma warning disable CA1819 // Properties should not return arrays
        public int[] TrackSections { get; private set; }
#pragma warning restore CA1819 // Properties should not return arrays

        internal TrackPath(STFReader stf)
        {
            stf.MustMatchBlockStart();
            DynamicSectionIndex = stf.ReadInt(null);
            int sectionNumber = (int)stf.ReadUInt(null);
            TrackSections = new int[sectionNumber];
            for (int i = 0; i < sectionNumber; ++i)
            {
                string token = stf.ReadString();
                if (token == ")")
                {
                    STFException.TraceWarning(stf, "Missing track section");
                    return;   // there are many TSECTION.DAT's with missing sections so we will accept this error
                }
                if (!uint.TryParse(token, out uint trackSection))
                    STFException.TraceWarning(stf, "Invalid track section " + token);
                else
                    TrackSections[i] = (int)trackSection;
            }
            stf.SkipRestOfBlock();
        }
    }

    public class TrackType
    {
        public string Label { get; private set; }
        public string InsideSound { get; private set; }
        public string OutsideSound { get; private set; }

        internal TrackType(STFReader stf)
        {
            stf.MustMatchBlockStart();
            Label = stf.ReadString();
            InsideSound = stf.ReadString();
            OutsideSound = stf.ReadString();
            stf.SkipRestOfBlock();
        }
    } // TrackType

    #region Index/Enumerable helpers for TrackNodes
    //Enables List-like (index-based or IEnumerable) access to the Junctions in the TrackNodes
    public abstract class PartialTrackNodeList<T> : IList<T> where T: TrackNode
    {
        private readonly List<int> elements;
        private readonly TrackNodes parent;

        internal PartialTrackNodeList(TrackNodes parent)
        {
            this.parent = parent;
            elements = new List<int>();
        }

        public T this[int index] { get => parent[index] as T; set => throw new NotImplementedException(); }

        public bool IsReadOnly => true;

        public int Count => elements.Count;

        public void Add(T item)
        {
            elements.Add(item?.Index ?? throw new ArgumentNullException(nameof(item)));
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(T item)
        {
            return elements.Contains(item?.Index ?? throw new ArgumentNullException(nameof(item)));
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator GetEnumerator()
        {
            return new NodeEnumerator<T>(elements, parent);
        }

        public int IndexOf(T item) => item?.Index ?? throw new ArgumentNullException(nameof(item));

        public void Insert(int index, T item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(T item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator<T> IEnumerable<T>.GetEnumerator()
        {
            return new NodeEnumerator<T>(elements, parent);
        }

        private class NodeEnumerator<TNodeType> : IEnumerator<TNodeType> where TNodeType : TrackNode
        {
            private readonly List<int> elements;
            private readonly TrackNodes trackNodes;
            private int current;

            public NodeEnumerator(List<int> elements, TrackNodes trackNodes)
            {
                this.elements = elements;
                this.trackNodes = trackNodes;
                current = -1;
            }

            public TNodeType Current => trackNodes[elements[current]] as TNodeType;

            object IEnumerator.Current => Current;

            public void Dispose()
            {
            }

            public bool MoveNext()
            {
                //Avoids going beyond the end of the collection.
                return ++current < elements.Count;
            }

            public void Reset()
            {
                current = -1;
            }
        }
    }

    public sealed class JunctionList : PartialTrackNodeList<TrackJunctionNode>
    {
        internal JunctionList(TrackNodes parent) : base(parent)
        {
        }
    }

    public sealed class VectorNodeList : PartialTrackNodeList<TrackVectorNode>
    {
        internal VectorNodeList(TrackNodes parent) : base(parent)
        {
        }
    }

    public sealed class EndNodeList : PartialTrackNodeList<TrackEndNode>
    {
        internal EndNodeList(TrackNodes parent) : base(parent)
        {
        }
    }

    #endregion

    public class TrackNodes : IList<TrackNode>
    {
        private readonly List<TrackNode> nodes;

        public VectorNodeList VectorNodes { get; }
        public JunctionList JunctionNodes { get; }
        public EndNodeList EndNodes { get; }

        public TrackNodes(int capacity)
        {
            nodes = new List<TrackNode>(capacity);
            JunctionNodes = new JunctionList(this);
            VectorNodes = new VectorNodeList(this);
            EndNodes = new EndNodeList(this);
        }

        public TrackNode this[int index] { get => nodes[index]; set => throw new NotImplementedException(); }

        public int Count => nodes.Count;

        public bool IsReadOnly => true;

        public void Add(TrackNode item)
        {
            nodes.Add(item);
            switch (item)
            {
                case TrackVectorNode vectorNode:
                    VectorNodes.Add(vectorNode);
                    break;
                case TrackJunctionNode junctionNode:
                    JunctionNodes.Add(junctionNode);
                    break;
                case TrackEndNode endNode:
                    EndNodes.Add(endNode);
                    break;
            }
        }

        public void Clear()
        {
            throw new NotImplementedException();
        }

        public bool Contains(TrackNode item) => nodes.Contains(item);

        public void CopyTo(TrackNode[] array, int arrayIndex)
        {
            throw new NotImplementedException();
        }

        public IEnumerator<TrackNode> GetEnumerator() => nodes.GetEnumerator();

        public int IndexOf(TrackNode item) => nodes.IndexOf(item);

        public void Insert(int index, TrackNode item)
        {
            throw new NotImplementedException();
        }

        public bool Remove(TrackNode item)
        {
            throw new NotImplementedException();
        }

        public void RemoveAt(int index)
        {
            throw new NotImplementedException();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    #region TrackDataBase
    /// <summary>
    /// This class represents the Track Database.
    /// </summary>
    public class TrackDB
    {
        private readonly Dictionary<string, TrackJunctionNode> junctionNodes = new Dictionary<string, TrackJunctionNode>();

        /// <summary>
        /// All TrackNodes in the track database
        /// Warning, the first TrackNode is always null.
        /// </summary>
        public TrackNodes TrackNodes { get; private set; }

#pragma warning disable CA1002 // Do not expose generic lists
        /// <summary>
        /// Array of all Track Items (TrItem) in the track database
        /// </summary>
        public List<TrackItem> TrackItems { get; } = new List<TrackItem>();
#pragma warning restore CA1002 // Do not expose generic lists

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        internal TrackDB(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tracknodes", ()=>{
                    stf.MustMatchBlockStart();
                    int numberOfTrackNodes = stf.ReadInt(null);
                    TrackNodes = new TrackNodes(numberOfTrackNodes + 1) { null };
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("tracknode", ()=>{
                            TrackNodes.Add(TrackNode.ReadTrackNode(stf, TrackNodes.Count, numberOfTrackNodes));
                            if (TrackNodes[^1] is TrackJunctionNode junctionNode)
                            {
                                string key = $"{junctionNode.UiD.WorldId}-{junctionNode.UiD.Location.TileX}-{junctionNode.UiD.Location.TileZ}";
                                junctionNodes.TryAdd(key, junctionNode);
                                // only need any (first) junction node with that key here to relate back to ShapeIndex
                            }
                        }),
                    });
                }),
                new STFReader.TokenProcessor("tritemtable", ()=>{
                    stf.MustMatchBlockStart();
                    int numberOfTrItems = stf.ReadInt(null);
                    TrackItems.Capacity = numberOfTrItems;
                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                        new STFReader.TokenProcessor("crossoveritem", ()=>{ TrackItems.Add(new CrossoverItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("signalitem", ()=>{ TrackItems.Add(new SignalItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("speedpostitem", ()=>{ TrackItems.Add(new SpeedPostItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("platformitem", ()=>{ TrackItems.Add(new PlatformItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("soundregionitem", ()=>{ TrackItems.Add(new SoundRegionItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("emptyitem", ()=>{ TrackItems.Add(new EmptyItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("levelcritem", ()=>{ TrackItems.Add(new LevelCrossingItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("sidingitem", ()=>{ TrackItems.Add(new SidingItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("hazzarditem", ()=>{ TrackItems.Add(new HazardItem(stf, TrackItems.Count)); }),
                        new STFReader.TokenProcessor("pickupitem", ()=>{ TrackItems.Add(new PickupItem(stf, TrackItems.Count)); }),
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
            ArgumentNullException.ThrowIfNull(trackItems);

            int i = TrackItems?.Count ?? 0;
            foreach (TrackItem item in trackItems)
            {
                item.TrackItemId = i++;
            }

            TrackItems.AddRange(trackItems);
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

    #region CrossReference to TrackCircuitSection
    /// <summary>
    /// To make it possible for a MSTS (vector) TrackNode to have information about the TrackCircuitSections that
    /// represent that TrackNode, this class defines the basic information of a single of these TrackCircuitSections.
    /// </summary>
    public class TrackCircuitSectionCrossReference
    {
        /// <summary>full length</summary>
        public float Length { get; private set; }
        /// <summary>Offset length in orig track section, for either forward or backward direction</summary>
        /// Offset indicates length from end of original tracknode, Index 0 is forward, index 1 is backward wrt original tracknode direction.
        public EnumArray<float, TrackDirection> OffsetLength { get; } = new EnumArray<float, TrackDirection>();
        /// <summary>index of TrackCircuitSection</summary>
        public int Index { get; private set; }

        /// <summary>
        /// Constructor and setting reference, length and offset length from section
        /// </summary>
        /// <param name="sectionIndex"></param>
        /// <param name="sectionLength"></param>
        public TrackCircuitSectionCrossReference(int sectionIndex, float sectionLength, float sectionOffsetLengthAhead, float sectionOffsetLengthReverse)
        {
            Index = sectionIndex;
            Length = sectionLength;
            OffsetLength[TrackDirection.Ahead] = sectionOffsetLengthAhead;
            OffsetLength[TrackDirection.Reverse] = sectionOffsetLengthReverse;
        }
    }

    /// <summary>
    /// Class to make it possible for a MSTS (vector) TrackNode to have information about the TrackCircuitSections that
    /// represent that TrackNode.
    /// </summary>
    public class TrackCircuitCrossReferences : List<TrackCircuitSectionCrossReference>
    {
        /// <summary>
        /// The tracksections form together a representation of a vector node. Once you give a direction along that vector
        /// and the offset from the start, get the index of the TrackCircuitSectionXref at that location
        /// </summary>
        /// <param name="offset">Offset along the vector node where we want to find the tracksection</param>
        /// <param name="direction">Direction where we start measuring along the vector node</param>
        /// <returns>Index in the current list of crossreferences</returns>
        public int GetCrossReferenceIndex(float offset, TrackDirection direction)
        {
            if (direction == TrackDirection.Ahead)
            {   // search forward, start at the second one (first one should have offsetlength zero
                for (int trackCircuit = 1; trackCircuit < Count; trackCircuit++)
                {
                    if (this[trackCircuit].OffsetLength[direction] > offset)
                    {
                        return (trackCircuit - 1);
                    }
                }

                // not yet found, try the last one
                TrackCircuitSectionCrossReference thisReference = this[Count - 1];
                if (offset <= (thisReference.OffsetLength[direction] + thisReference.Length))
                {
                    return (Count - 1);
                }

                //really not found, return the first one
                return (0);
            }
            else
            {   // search backward, start at last -1 (because last should end at vector node end anyway
                for (int trackCircuit = Count - 2; trackCircuit >= 0; trackCircuit--)
                {
                    if (this[trackCircuit].OffsetLength[direction] > offset)
                    {
                        return (trackCircuit + 1);
                    }
                }

                //not yet found, try the first one.
                TrackCircuitSectionCrossReference thisReference = this[0];
                if (offset <= (thisReference.OffsetLength[direction] + thisReference.Length))
                {
                    return (0);
                }

                //really not found, return the last one
                return (Count - 1);
            }
        }

        /// <summary>
        /// The tracksections form together a representation of a vector node. Once you give a direction along that vector
        /// and the offset from the start, get the index of the TrackCircuitSection at that location
        /// </summary>
        /// <param name="offset">Offset along the vector node where we want to find the tracksection</param>
        /// <param name="direction">Direction where we start measuring along the vector node</param>
        /// <returns>Index of the section that is at the wanted location</returns>
        public int GetSectionIndex(float offset, TrackDirection direction)
        {
            int index = GetCrossReferenceIndex(offset, direction);

            return index >= 0 ? this[index].Index : -1;
        }
    } // class TrackCircuitXRefList
    #endregion

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
#pragma warning disable CA1819 // Properties should not return arrays
        public TrackPin[] TrackPins { get; private set; }
#pragma warning restore CA1819 // Properties should not return arrays
        /// <summary>Number of outgoing pins (connections to other tracknodes)</summary>
        public int InPins { get; private set; }
        /// <summary>Number of outgoing pins (connections to other tracknodes)</summary>
        public int OutPins { get; private set; }

        /// <summary>The index in the array of tracknodes.</summary>
        public int Index { get; private set; }

        private TrackCircuitCrossReferences circuitCrossReferences;
        /// <summary>
        /// List of references to Track Circuit sections
        /// </summary>
        public TrackCircuitCrossReferences TrackCircuitCrossReferences
        {
            get
            {
                if (null == circuitCrossReferences)
                    circuitCrossReferences = new TrackCircuitCrossReferences();
                return circuitCrossReferences;
            }
        }

        private protected TrackNode(STFReader stf, int index, int maxTrackNode, int expectedPins)
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

        private protected abstract void ReadNodeData(STFReader stf);

        private protected static UiD ReadUiD(STFReader stf)
        {
            return new UiD(stf);
        }

        private protected void ReadPins(STFReader stf, int maxTrackNode)
        {
            stf.MustMatchBlockStart();
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
            stf.MustMatchBlockStart();
            int index = stf.ReadInt(null);
            Debug.Assert(index == expectedIndex, "TrackNode Index Mismatch");
            if (!EnumExtension.GetValue(stf.ReadString(), out NodeType nodeType))
            {
                throw new STFException(stf, "Unknown TrackNode type");
            }
            stf.StepBackOneItem();
            return nodeType switch
            {
                NodeType.TrEndNode => new TrackEndNode(stf, index, maxNodeIndex),
                NodeType.TrJunctionNode => new TrackJunctionNode(stf, index, maxNodeIndex),
                NodeType.TrVectorNode => new TrackVectorNode(stf, index, maxNodeIndex),
                _ => null,
            };
        }
    }

    /// <summary>
    /// This TrackNode has nothing else connected to it (that is, it is
    /// a buffer end or an unfinished track) and trains cannot proceed beyond here.
    /// </summary>
    [DebuggerDisplay("\\{MSTS.TrEndNode\\}")]
    public class TrackEndNode : TrackNode
    {
        internal TrackEndNode(STFReader stf, int index, int maxTrackNode) :
            base(stf, index, maxTrackNode, 1)
        {
        }

        private protected override void ReadNodeData(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.SkipRestOfBlock();
        }
    }

    [DebuggerDisplay("\\{MSTS.TrJunctionNode\\} SelectedRoute={SelectedRoute}, ShapeIndex={ShapeIndex}")]
    public class TrackJunctionNode : TrackNode
    {
        /// <summary>
        /// The route of a switch that is currently in use.
        /// </summary>
        public int SelectedRoute { get; set; }

        /// <summary>
        /// Index to the shape that actually describes the looks of this switch
        /// </summary>
        public int ShapeIndex { get; private set; }

        /// <summary>The angle of this junction</summary>
        private float angle = float.NaN;

        internal TrackJunctionNode(STFReader stf, int index, int maxTrackNode) :
            base(stf, index, maxTrackNode, 3)
        {
        }

        private protected override void ReadNodeData(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ReadString();
            ShapeIndex = stf.ReadInt(null);
            stf.SkipRestOfBlock();
        }

        /// <summary>
        /// Calculate the angle (direction in 2D) of the current junction (result will be cached).
        /// </summary>
        /// <returns>The angle calculated</returns>
        public float Angle
        {
            get
            {
                if (!float.IsNaN(angle))
                    return angle;

                TrackShape trackShape = RuntimeData.Instance.TSectionDat.TrackShapes[ShapeIndex];
                SectionIndex[] sectionIndices = trackShape.SectionIndices;

                for (int index = 0; index < sectionIndices.Length; index++)
                {
                    if (index == trackShape.MainRoute)
                        continue;
                    int[] sections = sectionIndices[index].TrackSections;

                    for (int i = 0; i < sections.Length; i++)
                    {
                        int sid = sectionIndices[index].TrackSections[i];
                        TrackSection section = RuntimeData.Instance.TSectionDat.TrackSections[sid];

                        if (section.Curved)
                        {
                            angle = section.Angle;
                            break;
                        }
                    }
                }
                return angle;
            }
        }
    }

    /// <summary>
    /// Describes the details of a vectorNode, a connection between two junctions (or endnodes).
    /// A vectorNode itself is made up of various sections. The begin point of each of these sections
    /// is stored (as well as its direction). As a result, VectorNodes have a direction.
    /// Furthermore, a number of TrItems (Track Items) can be located on the vector nodes.
    /// </summary>
    [DebuggerDisplay("\\{MSTS.TrVectorNode\\} TrVectorSections={TrackVectorSections?.Length ?? null}, TrItemsRefs={TrackItemIndices?.Length ?? null}")]
    public class TrackVectorNode : TrackNode
    {
        private static readonly int[] emptyTrackItemIndices = Array.Empty<int>();
        /// <summary>Array of sections that together form the vectorNode</summary>
#pragma warning disable CA1819 // Properties should not return arrays
        public TrackVectorSection[] TrackVectorSections { get; private set; }
        /// <summary>Array of indexes of TrItems (track items) that are located on this vectorNode</summary>
        public int[] TrackItemIndices { get; private set; } = emptyTrackItemIndices;
        /// <summary>The amount of TrItems in TrItemRefs</summary>
#pragma warning restore CA1819 // Properties should not return arrays

        internal TrackVectorNode(STFReader stf, int index, int maxTrackNode) :
            base(stf, index, maxTrackNode, 2)
        {
        }

        private protected override void ReadNodeData(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("trvectorsections", ()=>{
                    stf.MustMatchBlockStart();
                    int numberOfVectorSections = stf.ReadInt(null);
                    TrackVectorSections = new TrackVectorSection[numberOfVectorSections];
                    for (int i = 0; i < numberOfVectorSections; ++i)
                        TrackVectorSections[i] = new TrackVectorSection(stf);
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("tritemrefs", ()=>{
                    stf.MustMatchBlockStart();
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
        /// Add a reference to a new TrItem to the already existing TrItemRefs.
        /// </summary>
        /// <param name="newTrItemRef">The reference to the new TrItem</param>
        public void AddTrackItemIndex(int trackItemIndex)
        {
            int[] temp = new int[TrackItemIndices.Length + 1];
            TrackItemIndices.CopyTo(temp, 0);
            temp[^1] = trackItemIndex;
            TrackItemIndices = temp; //use the new item lists for the track node
        }

        /// <summary>
        /// Insert a reference to a new TrItem to the already existing TrItemRefs.
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
    public readonly struct TrackPin
    {
        /// <summary>Index of the tracknode connected to the parent of this pin</summary>
        public int Link { get; }
        /// <summary>In case a connection is made to a vector node this determines the side of the vector node that is connected to</summary>
        public TrackDirection Direction { get; }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        internal TrackPin(STFReader stf)
        {
            stf.MustMatchBlockStart();
            Link = stf.ReadInt(null);
            Direction = (TrackDirection)stf.ReadInt(null);
            stf.SkipRestOfBlock();
        }

        public TrackPin(int link, TrackDirection direction)
        {
            Link = link;
            Direction = direction;
        }

        public TrackPin FromLink(int link)
        {
            return new TrackPin(link, Direction);
        }

        public static readonly TrackPin Empty = new TrackPin(-1, (TrackDirection)(-1));
    }

    public class TrackPinComparer : IEqualityComparer<TrackPin>
    {
        private TrackPinComparer() { }

        public bool Equals(TrackPin x, TrackPin y)
        {
            return x.Link == y.Link;
        }

        public int GetHashCode(TrackPin obj)
        {
            return obj.Link;
        }

        public static TrackPinComparer LinkOnlyComparer { get; } = new TrackPinComparer();
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
        internal UiD(STFReader stf)
        {
            stf.MustMatchBlockStart();
            int worldTileX = stf.ReadInt(null);
            int worldTileZ = stf.ReadInt(null);
            WorldId = stf.ReadInt(null);
            stf.ReadInt(null);
            location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null), stf.ReadFloat(null), stf.ReadFloat(null), stf.ReadFloat(null));

            //if (worldTileX != location.TileX || worldTileZ != location.TileZ)
            //    STFException.TraceInformation(stf, $"Inconsistent WorldTile information in UiD node {WorldId}: WorldTileX({worldTileX}), WorldTileZ({worldTileZ}), Location.TileX({location.TileX}), Location.TileZ({location.TileZ})");

            //AX = stf.ReadFloat(STFReader.Units.None, null);
            //AY = stf.ReadFloat(STFReader.Units.None, null);
            //AZ = stf.ReadFloat(STFReader.Units.None, null);
            stf.SkipRestOfBlock();
        }
    }
    #endregion

    #region TrackVectorSection
    /// <summary>
    /// Describes a single section in a vector node. 
    /// </summary>
    public class TrackVectorSection
    {
        private readonly WorldLocation location;
        private readonly Vector3 direction;

        public ref readonly WorldLocation Location => ref location;
        public ref readonly Vector3 Direction => ref direction;
        /// <summary>First flag. Not completely clear, usually 0, - may point to the connecting pin entry in a junction. Sometimes 2</summary>
        public int Flag1 { get; }
        /// <summary>Second flag. Not completely clear, usually 1, but set to 0 when curve track is flipped around. Sometimes 2</summary>
        public int Flag2 { get; }
        /// <summary>Index of the track section in Tsection.dat</summary>
        public int SectionIndex { get; }
        /// <summary>Index to the shape from Tsection.dat</summary>
        public int ShapeIndex { get; }

        //The following items are related to super elevation
        /// <summary>Cross-reference to worldFile: World ID</summary>
        public uint WorldFileUiD { get; }
        /// <summary>The (super)elevation at the start</summary>
        public float StartElev { get; set; }
        /// <summary>The (super)elevation at the end</summary>
        public float EndElev { get; set; }
        /// <summary>The maximum (super) elevation</summary>
        public float MaxElev { get; set; }

        /// <summary>
        /// Default constructor used during file parsing.
        /// </summary>
        /// <param name="stf">The STFreader containing the file stream</param>
        internal TrackVectorSection(STFReader stf)
        {
            SectionIndex = (int)stf.ReadUInt(null);
            ShapeIndex = stf.ReadInt(null);
            int worldTileX = stf.ReadInt(null);// worldfilenamex
            int worldTileZ = stf.ReadInt(null);// worldfilenamez
            WorldFileUiD = stf.ReadUInt(null); // UID in worldfile
            Flag1 = stf.ReadInt(null); // 0
            Flag2 = stf.ReadInt(null); // 1
            stf.ReadString(); // 00 
            location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null), stf.ReadFloat(null), stf.ReadFloat(null), stf.ReadFloat(null));
            direction = new Vector3(stf.ReadFloat(null), stf.ReadFloat(null), stf.ReadFloat(null));

            //if (worldTileX != location.TileX || worldTileZ != location.TileZ)
            //    STFException.TraceInformation(stf, $"Inconsistent WorldTile information in UiD node {WorldFileUiD}: WorldTileX({worldTileX}), WorldTileZ({worldTileZ}), Location.TileX({location.TileX}), Location.TileZ({location.TileZ})");
        }

        /// <summary>
        /// Overriding the ToString, which makes it easier to debug
        /// </summary>
        /// <returns>String giving info on this section</returns>
        public override string ToString()
        {
            return $"{{TileX:{location.TileX} TileZ:{location.TileZ} X:{location.Location.X} Y:{location.Location.Y} Z:{location.Location.Z} UiD:{WorldFileUiD} Section:{SectionIndex} Shape:{ShapeIndex}}}";
        }
    }
    #endregion
}
