using System;
using System.Collections.Generic;
using System.Diagnostics;

using Microsoft.Xna.Framework;

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
        public ref Vector3 Offset => ref offset;
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
}
