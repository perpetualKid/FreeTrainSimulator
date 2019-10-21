using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Xna.Framework;
using Orts.Common;
using Orts.Formats.Msts.Files;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class TrackSection
    {
        public uint SectionIndex { get; protected set; }

        //straight segment
        public float Width { get; private set; } = 1.5f;
        public float Length { get; private set; }

        //curved segment
        public bool Curved { get; private set; }
        public float Radius { get; private set; }    // meters
        public float Angle { get; private set; }	// degrees

        public TrackSection(STFReader stf, bool routeTrackSection)
        {
            if (routeTrackSection)
            {
                stf.MustMatch("(");
                stf.MustMatch("SectionCurve");
                stf.SkipBlock();
                SectionIndex = stf.ReadUInt(null);

                float a = stf.ReadFloat(STFReader.Units.Distance, null);
                float b = stf.ReadFloat(STFReader.Units.None, null);
                if (b == 0) // Its straight
                    Length = a;
                else // its curved
                {
                    Radius = b;
                    Angle = MathHelper.ToDegrees(a);
                    Curved = true;
                }
                stf.SkipRestOfBlock();
            }
            else
            {
                stf.MustMatch("(");
                SectionIndex = stf.ReadUInt(null);
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
        public TrackSection(SBR block)
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
            using (var subBlock = block.ReadSubBlock())
            {
                subBlock.VerifyID(TokenID.SectionCurve);
                Curved = subBlock.ReadUInt() != 0;
                subBlock.VerifyEndOfBlock();
            }
            SectionIndex = block.ReadUInt();
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
            stf.MustMatch("(");
            Width = stf.ReadFloat(STFReader.Units.Distance, null);
            Length = stf.ReadFloat(STFReader.Units.Distance, null);
            stf.SkipRestOfBlock();
        }

        private void ReadSectionCurve(STFReader stf)
        {
            Curved = true;
            stf.MustMatch("(");
            Radius = stf.ReadFloat(STFReader.Units.Distance, null);
            Angle = stf.ReadFloat(STFReader.Units.None, null);
            stf.SkipRestOfBlock();
        }
    }

    public class TrackSections : Dictionary<uint, TrackSection>
    {
        public uint MaxSectionIndex { get; private set; }

        public TrackSections(STFReader stf)
        {
            AddRouteStandardTrackSections(stf);
        }

        public void AddRouteStandardTrackSections(STFReader stf)
        {
            stf.MustMatch("(");
            MaxSectionIndex = stf.ReadUInt(null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("tracksection", ()=>{ AddSection(stf, new TrackSection(stf, false)); }),
            });
        }

        public void AddRouteTrackSections(STFReader stf)
        {
            stf.MustMatch("(");
            MaxSectionIndex = stf.ReadUInt(null);
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

        public TrackSection Get(uint targetSectionIndex)
        {
            if (TryGetValue(targetSectionIndex, out TrackSection ts))
                return ts;
            if (MissingTrackSectionWarnings++ < 5)
                Trace.TraceWarning("Skipped track section {0} not in global or dynamic TSECTION.DAT", targetSectionIndex);
            return null;
        }
    }

    public class SectionIndex
    {
        private Vector3 offset;
        public uint SectionsCount { get; private set; }
        public ref readonly Vector3 Offset => ref offset;
        public float AngularOffset { get; private set; }  // Angular offset 
        public uint[] TrackSections { get; private set; }

        public SectionIndex(STFReader stf)
        {
            stf.MustMatch("(");
            SectionsCount = stf.ReadUInt(null);
            offset = new Vector3(stf.ReadFloat(null), stf.ReadFloat(null), -stf.ReadFloat(null));
            AngularOffset = stf.ReadFloat(null);
            TrackSections = new uint[SectionsCount];
            for (int i = 0; i < SectionsCount; ++i)
            {
                string token = stf.ReadString();
                if (token == ")")
                {
                    STFException.TraceWarning(stf, "Missing track section");
                    return;   // there are many TSECTION.DAT's with missing sections so we will accept this error
                }
                if (!uint.TryParse(token, out TrackSections[i]))
                    STFException.TraceWarning(stf, "Invalid track section " + token);
            }
            stf.SkipRestOfBlock();
        }
    }

    [DebuggerDisplay("TrackShape {ShapeIndex}")]
    public class TrackShape
    {
        public uint ShapeIndex { get; private set; }
        public string FileName { get; private set; }
        public uint PathsNumber { get; private set; }
        public uint MainRoute { get; private set; }
        public double ClearanceDistance { get; private set; }
        public SectionIndex[] SectionIndices { get; private set; }
        public bool TunnelShape { get; private set; }
        public bool RoadShape { get; private set; }

        public TrackShape(STFReader stf)
        {
            stf.MustMatch("(");
            ShapeIndex = stf.ReadUInt(null);
            int nextPath = 0;
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("filename", ()=>{ FileName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("numpaths", ()=>{ SectionIndices = new SectionIndex[PathsNumber = stf.ReadUIntBlock(null)]; }),
                new STFReader.TokenProcessor("mainroute", ()=>{ MainRoute = stf.ReadUIntBlock(null); }),
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

    public class TrackShapes : Dictionary<uint, TrackShape>
    {

        public uint MaxShapeIndex { get; private set; }

        public TrackShapes(STFReader stf)
        {
            AddRouteTrackShapes(stf);
        }

        public void AddRouteTrackShapes(STFReader stf)
        {
            stf.MustMatch("(");
            MaxShapeIndex = stf.ReadUInt(null);
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

    public class TrackPaths : Dictionary<uint, TrackPath> //SectionIdx in the route's tsection.dat
    {
        public TrackPaths(STFReader stf)
        {
            stf.MustMatch("(");
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
            catch (Exception e)
            {
                STFException.TraceWarning(stf, "Warning: in route tsection.dat " + e.Message);
            }
        }
    }

    public class TrackPath //SectionIdx in the route's tsection.dat
    {

        public uint DynamicSectionIndex { get; private set; }
        public uint[] TrackSections { get; private set; }

        public TrackPath(STFReader stf)
        {
            stf.MustMatch("(");
            DynamicSectionIndex = stf.ReadUInt(null);
            uint sectionNumber = stf.ReadUInt(null);
            TrackSections = new uint[sectionNumber];
            for (int i = 0; i < sectionNumber; ++i)
            {
                string token = stf.ReadString();
                if (token == ")")
                {
                    STFException.TraceWarning(stf, "Missing track section");
                    return;   // there are many TSECTION.DAT's with missing sections so we will accept this error
                }
                if (!uint.TryParse(token, out TrackSections[i]))
                    STFException.TraceWarning(stf, "Invalid track section " + token);
            }
            stf.SkipRestOfBlock();
        }
    }

    public class TrackType
    {
        public string Label { get; private set; }
        public string InsideSound { get; private set; }
        public string OutsideSound { get; private set; }

        public TrackType(STFReader stf)
        {
            stf.MustMatch("(");
            Label = stf.ReadString();
            InsideSound = stf.ReadString();
            OutsideSound = stf.ReadString();
            stf.SkipRestOfBlock();
        }
    } // TrackType

    #region TrackDataBase
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
        public float[] OffsetLength { get; } = new float[2];
        /// <summary>index of TrackCircuitSection</summary>
        public int Index { get; private set; }

        /// <summary>
        /// Constructor and setting reference, length and offset length from section
        /// </summary>
        /// <param name="sectionIndex"></param>
        /// <param name="sectionLength"></param>
        public TrackCircuitSectionCrossReference(int sectionIndex, float sectionLength, float[] sectionOffsetLength)
        {
            Index = sectionIndex;
            Length = sectionLength;
            OffsetLength[0] = sectionOffsetLength[0];
            OffsetLength[1] = sectionOffsetLength[1];
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
        public int GetCrossReferenceIndex(float offset, int direction)
        {
            if (direction == 0)
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
        public int GetSectionIndex(float offset, int direction)
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
        public TrackPin[] TrackPins;
        /// <summary>Number of outgoing pins (connections to other tracknodes)</summary>
        public int InPins { get; private set; }
        /// <summary>Number of outgoing pins (connections to other tracknodes)</summary>
        public int OutPins { get; private set; }

        /// <summary>The index in the array of tracknodes.</summary>
        public uint Index { get; private set; }

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
        public TrackEndNode(STFReader stf, uint index, int maxTrackNode) :
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
    public class TrackJunctionNode : TrackNode
    {
        /// <summary>
        /// The route of a switch that is currently in use.
        /// </summary>
        public int SelectedRoute { get; set; }

        /// <summary>
        /// Index to the shape that actually describes the looks of this switch
        /// </summary>
        public uint ShapeIndex { get; private set; }

        /// <summary>The angle of this junction</summary>
        private float angle = float.MaxValue;

        public TrackJunctionNode(STFReader stf, uint index, int maxTrackNode) :
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
    [DebuggerDisplay("\\{MSTS.TrVectorNode\\} TrVectorSections={TrackVectorSections?.Length ?? null}, TrItemsRefs={TrackItemIndices?.Length ?? null}")]
    public class TrackVectorNode : TrackNode
    {
        private static readonly int[] emptyTrackItemIndices = new int[0];
        /// <summary>Array of sections that together form the vectorNode</summary>
        public TrackVectorSection[] TrackVectorSections { get; private set; }
        /// <summary>Array of indexes of TrItems (track items) that are located on this vectorNode</summary>
        public int[] TrackItemIndices { get; private set; } = emptyTrackItemIndices;
        /// <summary>The amount of TrItems in TrItemRefs</summary>

        public TrackVectorNode(STFReader stf, uint index, int maxTrackNode) :
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
                    TrackVectorSections = new TrackVectorSection[numberOfVectorSections];
                    for (int i = 0; i < numberOfVectorSections; ++i)
                        TrackVectorSections[i] = new TrackVectorSection(stf);
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
    #endregion

    #region TrackVectorSection
    /// <summary>
    /// Describes a single section in a vector node. 
    /// </summary>
    public class TrackVectorSection
    {
        private WorldLocation location;
        private Vector3 direction;

        public ref readonly WorldLocation Location => ref location;
        public ref readonly Vector3 Direction => ref direction;
        /// <summary>First flag. Not completely clear, usually 0, - may point to the connecting pin entry in a junction. Sometimes 2</summary>
        public int Flag1 { get; set; }
        /// <summary>Second flag. Not completely clear, usually 1, but set to 0 when curve track is flipped around. Sometimes 2</summary>
        public int Flag2 { get; set; }
        /// <summary>Index of the track section in Tsection.dat</summary>
        public uint SectionIndex { get; set; }
        /// <summary>Index to the shape from Tsection.dat</summary>
        public uint ShapeIndex { get; set; }

        //The following items are related to super elevation
        /// <summary>Cross-reference to worldFile: World ID</summary>
        public uint WorldFileUiD { get; set; }
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
        public TrackVectorSection(STFReader stf)
        {
            SectionIndex = stf.ReadUInt(null);
            ShapeIndex = stf.ReadUInt(null);
            int worldTileX = stf.ReadInt(null);// worldfilenamex
            int worldTileZ = stf.ReadInt(null);// worldfilenamez
            WorldFileUiD = stf.ReadUInt(null); // UID in worldfile
            Flag1 = stf.ReadInt(null); // 0
            Flag2 = stf.ReadInt(null); // 1
            stf.ReadString(); // 00 
            location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null), stf.ReadFloat(null), stf.ReadFloat(null), stf.ReadFloat(null));
            direction = new Vector3(stf.ReadFloat(null), stf.ReadFloat(null), stf.ReadFloat(null));

            if (worldTileX != location.TileX || worldTileZ != location.TileZ)
                STFException.TraceInformation(stf, $"Inconsistent WorldTile information in UiD node {WorldFileUiD}: WorldTileX({worldTileX}), WorldTileZ({worldTileZ}), Location.TileX({location.TileX}), Location.TileZ({location.TileZ})");

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
