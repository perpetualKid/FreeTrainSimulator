﻿// COPYRIGHT 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Common;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Common;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Simulation;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Schema;

namespace Orts.ActivityRunner.Viewer3D
{
    public static class DynamicTrack
    {
        /// <summary>
        /// Decompose an MSTS multi-subsection dynamic track section into multiple single-subsection sections.
        /// </summary>
        /// <param name="viewer">Viewer reference.</param>
        /// <param name="trackList">DynamicTrackViewer list.</param>
        /// <param name="trackObj">Dynamic track section to decompose.</param>
        /// <param name="worldMatrix">Position matrix.</param>
        public static void Decompose(Viewer viewer, List<DynamicTrackViewer> trackList, DynamicTrackObject trackObj, in WorldPosition worldMatrix)
        {
            // DYNAMIC TRACK
            // =============
            // Objectives:
            // 1-Decompose multi-subsection DT into individual sections.  
            // 2-Create updated transformation objects (instances of WorldPosition) to reflect 
            //   root of next subsection.
            // 3-Distribute elevation change for total section through subsections. (ABANDONED)
            // 4-For each meaningful subsection of dtrack, build a separate DynamicTrackPrimitive.
            //
            // Method: Iterate through each subsection, updating WorldPosition for the root of
            // each subsection.  The rotation component changes only in heading.  The translation 
            // component steps along the path to reflect the root of each subsection.

            // The following vectors represent local positioning relative to root of original (5-part) section:
            Vector3 localV = Vector3.Zero; // Local position (in x-z plane)
            Vector3 localProjectedV; // Local next position (in x-z plane)
            Vector3 displacement;  // Local displacement (from y=0 plane)
            Vector3 heading = Vector3.Forward; // Local heading (unit vector)

            WorldPosition nextRoot = worldMatrix; // Will become initial root
            Vector3 sectionOrigin = worldMatrix.XNAMatrix.Translation; // Save root position
            Matrix root = MatrixExtension.RemoveTranslation(worldMatrix.XNAMatrix); // worldMatrix now rotation-only

            // Iterate through all subsections
            for (int iTkSection = 0; iTkSection < trackObj.TrackSections.Count; iTkSection++)
            {
                if ((trackObj.TrackSections[iTkSection].Length == 0f && trackObj.TrackSections[iTkSection].Angle == 0f) 
                    || trackObj.TrackSections[iTkSection].SectionIndex == -1) continue; // Consider zero-length subsections vacuous

                // Create new DT object copy; has only one meaningful subsection
                DynamicTrackObject subsection = new DynamicTrackObject(trackObj, iTkSection);

                //uint uid = subsection.trackSections[0].UiD; // for testing

                // Create a new WorldPosition for this subsection, initialized to nextRoot,
                // which is the WorldPosition for the end of the last subsection.
                // In other words, beginning of present subsection is end of previous subsection.
                WorldPosition current = nextRoot;

                // Now we need to compute the position of the end (nextRoot) of this subsection,
                // which will become current for the next subsection.

                // Clear nextRoot's translation vector so that nextRoot matrix contains rotation only
                nextRoot = nextRoot.SetTranslation(Vector3.Zero);

                // Straight or curved subsection?
                if (!subsection.TrackSections[0].Curved) // Straight section
                {   // Heading stays the same; translation changes in the direction oriented
                    // Rotate Vector3.Forward to orient the displacement vector
                    localProjectedV = localV + subsection.TrackSections[0].Length * heading;
                    displacement = InterpolateHelper.MSTSInterpolateAlongStraight(localV, heading, subsection.TrackSections[0].Length, root, out localProjectedV);
                }
                else // Curved section
                {   // Both heading and translation change 
                    // nextRoot is found by moving from Point-of-Curve (PC) to
                    // center (O)to Point-of-Tangent (PT).
                    float radius = subsection.TrackSections[0].Radius * Math.Sign(-subsection.TrackSections[0].Angle); // meters
                    Vector3 left = radius * Vector3.Cross(Vector3.Up, heading); // Vector from PC to O
                    Matrix rot = Matrix.CreateRotationY(-subsection.TrackSections[0].Angle); // Heading change (rotation about O)
                    // Shared method returns displacement from present world position and, by reference,
                    // local position in x-z plane of end of this section
                    displacement = InterpolateHelper.MSTSInterpolateAlongCurve(localV, left, rot, root, out localProjectedV);

                    heading = Vector3.Transform(heading, rot); // Heading change
                    nextRoot = new WorldPosition(nextRoot.TileX, nextRoot.TileZ, MatrixExtension.Multiply(rot, nextRoot.XNAMatrix));// Store heading change
                }

                // Update nextRoot with new translation component
                nextRoot = nextRoot.SetTranslation(sectionOrigin + displacement);

                // Create a new DynamicTrackViewer for the subsection
                trackList.Add(new DynamicTrackViewer(viewer, subsection, current, nextRoot));
                localV = localProjectedV; // Next subsection
            }
        }
    }

    public class DynamicTrackViewer
    {
        private Viewer Viewer;
        private WorldPosition worldPosition;
        public DynamicTrackPrimitive Primitive;

        public DynamicTrackViewer(Viewer viewer, DynamicTrackObject dtrack, in WorldPosition position, in WorldPosition endPosition)
        {
            Viewer = viewer;
            worldPosition = position;

            if (viewer.TRP == null)
            {
                // First to need a track profile creates it
                Trace.Write(" TRP");
                // Creates profile and loads materials into SceneryMaterials
                TRPFile.CreateTrackProfile(viewer, viewer.Simulator.RouteFolder.CurrentFolder, out TRPFile trp);
                viewer.TRP = trp;

            }

            // Instantiate classes
            Primitive = new DynamicTrackPrimitive(Viewer, dtrack, worldPosition, endPosition);
        }

        public DynamicTrackViewer(Viewer viewer, in WorldPosition position, in WorldPosition endPosition)
        {
            Viewer = viewer;
            worldPosition = position;
        }

        /// <summary>
        /// PrepareFrame adds any object mesh in-FOV to the RenderItemCollection. 
        /// and marks the last LOD that is in-range.
        /// </summary>
        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            // Offset relative to the camera-tile origin
            int dTileX = worldPosition.TileX - Viewer.Camera.TileX;
            int dTileZ = worldPosition.TileZ - Viewer.Camera.TileZ;
            Vector3 tileOffsetWrtCamera = new Vector3(dTileX * 2048, 0, -dTileZ * 2048);

            // Find midpoint between track section end and track section root.
            // (Section center for straight; section chord center for arc.)
            Vector3 xnaLODCenter = 0.5f * (Primitive.XNAEnd + worldPosition.XNAMatrix.Translation +
                                            2 * tileOffsetWrtCamera);
            Primitive.MSTSLODCenter = new Vector3(xnaLODCenter.X, xnaLODCenter.Y, -xnaLODCenter.Z);

            // Ignore any mesh not in field-of-view
            if (!Viewer.Camera.InFov(Primitive.MSTSLODCenter, Primitive.ObjectRadius)) return;

            // Scan LODs in forward order, and find first LOD in-range
            LOD lod;
            int lodIndex;
            for (lodIndex = 0; lodIndex < Primitive.TrProfile.LODs.Count; lodIndex++)
            {
                lod = Primitive.TrProfile.LODs[lodIndex];
                if (Viewer.Camera.InRange(Primitive.MSTSLODCenter, 0, lod.CutoffRadius)) break;
            }
            if (lodIndex == Primitive.TrProfile.LODs.Count) return;
            // lodIndex marks first in-range LOD

            // Initialize xnaXfmWrtCamTile to object-tile to camera-tile translation:
            Matrix xnaXfmWrtCamTile = Matrix.CreateTranslation(tileOffsetWrtCamera);
            xnaXfmWrtCamTile = worldPosition.XNAMatrix * xnaXfmWrtCamTile; // Catenate to world transformation
            // (Transformation is now with respect to camera-tile origin)

            int lastIndex;
            // Add in-view LODs to the RenderItems collection
            if (Primitive.TrProfile.LODMethod == TrProfile.LODMethods.CompleteReplacement)
            {
                // CompleteReplacement case
                lastIndex = lodIndex; // Add only the LOD that is the first in-view
            }
            else
            {
                // ComponentAdditive case
                // Add all LODs from the smallest in-view CutOffRadius to the last
                lastIndex = Primitive.TrProfile.LODs.Count - 1;
            }
            while (lodIndex <= lastIndex)
            {
                lod = Primitive.TrProfile.LODs[lodIndex];
                for (int j = lod.PrimIndexStart; j < lod.PrimIndexStop; j++)
                {
                    frame.AddPrimitive(Primitive.ShapePrimitives[j].Material, Primitive.ShapePrimitives[j], RenderPrimitiveGroup.World, ref xnaXfmWrtCamTile, ShapeFlags.AutoZBias);
                }
                lodIndex++;
            }
        }

        public void Mark()
        {
            foreach (LOD lod in Primitive.TrProfile.LODs)
                lod.Mark();
        }
    }

    // A track profile consists of a number of groups used for LOD considerations.  There are LODs,
    // Levels-Of-Detail, each of which contains subgroups.  Here, these subgroups are called "LODItems."  
    // Each group consists of one of more "polylines".  A polyline is a chain of line segments successively 
    // interconnected. A polyline of n segments is defined by n+1 "vertices."  (Use of a polyline allows 
    // for use of more than single segments.  For example, a ballast LOD could be defined as left slope, 
    // level, right slope - a single polyline of four vertices.)

    /// <summary>
    ///  Track profile file class
    /// </summary>
    public class TRPFile
    {
        public TrProfile TrackProfile; // Represents the track profile
        //public RenderProcess RenderProcess; // TODO: Pass this along in function calls

        /// <summary>
        /// Creates a TRPFile instance from a track profile file (XML or STF) or canned.
        /// (Precedence is XML [.XML], STF [.DAT], default [canned]).
        /// </summary>
        /// <param name="viewer">Viewer.</param>
        /// <param name="routePath">Path to route.</param>
        /// <param name="trpFile">TRPFile created (out).</param>
        public static void CreateTrackProfile(Viewer viewer, string routePath, out TRPFile trpFile)
        {
            string path = routePath + @"\TrackProfiles";
            //Establish default track profile
            if (File.Exists(path + @"\TrProfile.xml"))
            {
                // XML-style
                trpFile = new TRPFile(viewer, path + @"\TrProfile.xml");
            }
            else if (File.Exists(path + @"\TrProfile.stf"))
            {
                // MSTS-style
                trpFile = new TRPFile(viewer, path + @"\TrProfile.stf");
            }
            else
            {
                // default
                trpFile = new TRPFile(viewer, "");
            }
            // FOR DEBUGGING: Writes XML file from current TRP
            //TRP.TrackProfile.SaveAsXML(@"C:/Users/Walt/Desktop/TrProfile.xml");
        }

        /// <summary>
        /// Create TrackProfile from a track profile file.  
        /// (Defaults on empty or nonexistent filespec.)
        /// </summary>
        /// <param name="viewer">Viewer 3D.</param>
        /// <param name="filespec">Complete filepath string to track profile file.</param>
        public TRPFile(Viewer viewer, string filespec)
        {
            if (string.IsNullOrEmpty(filespec))
            {
                // No track profile provided, use default
                TrackProfile = new TrProfile(viewer);
                Trace.Write("(default)");
                return;
            }
            FileInfo fileInfo = new FileInfo(filespec);
            if (!fileInfo.Exists)
            {
                TrackProfile = new TrProfile(viewer); // Default profile if no file
                Trace.Write("(default)");
            }
            else
            {
                string fext = filespec.Substring(filespec.LastIndexOf('.')); // File extension

                switch (fext.ToUpper())
                {
                    case ".STF": // MSTS-style
                        using (STFReader stf = new STFReader(filespec, false))
                        {
                            // "EXPERIMENTAL" header is temporary
                            if (stf.SimisSignature != "SIMISA@@@@@@@@@@JINX0p0t______")
                            {
                                STFException.TraceWarning(stf, "Invalid header - file will not be processed. Using DEFAULT profile.");
                                TrackProfile = new TrProfile(viewer); // Default profile if no file
                            }
                            else
                                try
                                {
                                    stf.ParseBlock(new STFReader.TokenProcessor[] {
                                        new STFReader.TokenProcessor("trprofile", ()=>{ TrackProfile = new TrProfile(viewer, stf); }),
                                    });
                                }
                                catch (Exception e)
                                {
                                    STFException.TraceWarning(stf, "Track profile STF constructor failed because " + e.Message + ". Using DEFAULT profile.");
                                    TrackProfile = new TrProfile(viewer); // Default profile if no file
                                }
                                finally
                                {
                                    if (TrackProfile == null)
                                    {
                                        STFException.TraceWarning(stf, "Track profile STF constructor failed. Using DEFAULT profile.");
                                        TrackProfile = new TrProfile(viewer); // Default profile if no file
                                    }
                                }
                        }
                        Trace.Write("(.STF)");
                        break;

                    case ".XML": // XML-style
                        // Convention: .xsd filename must be the same as .xml filename and in same path.
                        // Form filespec for .xsd file
                        string xsdFilespec = filespec.Substring(0, filespec.LastIndexOf('.')) + ".xsd"; // First part

                        // Specify XML settings
                        XmlReaderSettings settings = new XmlReaderSettings();
                        settings.ConformanceLevel = ConformanceLevel.Auto; // Fragment, Document, or Auto
                        settings.IgnoreComments = true;
                        settings.IgnoreWhitespace = true;
                        // Settings for validation
                        settings.ValidationEventHandler += new ValidationEventHandler(ValidationCallback);
                        settings.ValidationFlags |= XmlSchemaValidationFlags.ReportValidationWarnings;
                        settings.ValidationType = ValidationType.Schema; // Independent external file
                        settings.Schemas.Add("TrProfile.xsd", XmlReader.Create(xsdFilespec)); // Add schema from file

                        // Create an XML reader for the .xml file
                        using (XmlReader reader = XmlReader.Create(filespec, settings))
                        {
                            TrackProfile = new TrProfile(viewer, reader);
                        }
                        Trace.Write("(.XML)");
                        break;

                    default:
                        // File extension not supported; create a default track profile
                        TrackProfile = new TrProfile(viewer);
                        Trace.Write("(default)");
                        break;
                }
            }
        }

        // ValidationEventHandler callback function
        private void ValidationCallback(object sender, ValidationEventArgs args)
        {
            Console.WriteLine(); // Terminate pending Write
            if (args.Severity == XmlSeverityType.Warning)
            {
                Console.WriteLine("XML VALIDATION WARNING:");
            }
            if (args.Severity == XmlSeverityType.Error)
            {
                Console.WriteLine("XML VALIDATION ERROR:");
            }
            Console.WriteLine("{0} (Line {1}, Position {2}):",
                args.Exception.SourceUri, args.Exception.LineNumber, args.Exception.LinePosition);
            Console.WriteLine(args.Message);
            Console.WriteLine("----------");
        }
    }

    // Dynamic track profile class
    public class TrProfile
    {
        public string Name; // e.g., "Default track profile"
        public int ReplicationPitch; //TBD: Replication pitch alternative
        public LODMethods LODMethod = LODMethods.None; // LOD method of control
        public float ChordSpan; // Base method: No. of profiles generated such that span is ChordSpan degrees
        // If a PitchControl is defined, then the base method is compared to the PitchControl method,
        // and the ChordSpan is adjusted to compensate.
        public PitchControls PitchControl = PitchControls.None; // Method of control for profile replication pitch
        public float PitchControlScalar; // Scalar parameter for PitchControls
        public List<LOD> LODs { get; } = new List<LOD>(); // Array of Levels-Of-Detail

        /// <summary>
        /// Enumeration of LOD control methods
        /// </summary>
        public enum LODMethods
        {
            /// <summary>
            /// None -- No LODMethod specified; defaults to ComponentAdditive.
            /// </summary>
            None = 0,

            /// <summary>
            /// ComponentAdditive -- Each LOD is a COMPONENT that is ADDED as the camera gets closer.
            /// </summary>
            ComponentAdditive = 1,

            /// <summary>
            /// CompleteReplacement -- Each LOD group is a COMPLETE model that REPLACES another as the camera moves.
            /// </summary>
            CompleteReplacement = 2
        }

        /// <summary>
        /// Enumeration of cross section replication pitch control methods.
        /// </summary>
        public enum PitchControls
        {
            /// <summary>
            /// None -- No pitch control method specified.
            /// </summary>
            None = 0,

            /// <summary>
            /// ChordLength -- Constant length of chord.
            /// </summary>
            ChordLength,

            /// <summary>
            /// Chord Displacement -- Constant maximum displacement of chord from arc.
            /// </summary>
            ChordDisplacement
        }

        /// <summary>
        /// TrProfile constructor (default - builds from self-contained data)
        /// <param name="viewer">Viewer.</param>
        /// </summary>
        public TrProfile(Viewer viewer)
        {
            // Default TrProfile constructor

            Name = "Default Dynatrack profile";
            LODMethod = LODMethods.ComponentAdditive;
            ChordSpan = 1.0f; // Base Method: Generates profiles spanning no more than 1 degree

            PitchControl = PitchControls.ChordLength;       // Target chord length
            PitchControlScalar = 10.0f;                     // Hold to no more than 10 meters
            //PitchControl = PitchControls.ChordDisplacement; // Target chord displacement from arc
            //PitchControlScalar = 0.034f;                    // Hold to no more than 34 mm (half rail width)

            LOD lod;            // Local LOD instance
            LODItem lodItem;    // Local LODItem instance
            Polyline pl;        // Local Polyline instance

            // RAILSIDES
            lod = new LOD(700.0f); // Create LOD for railsides with specified CutoffRadius
            lodItem = new LODItem("Railsides");
            lodItem.TexName = "acleantrack2.ace";
            lodItem.ShaderName = "TexDiff";
            lodItem.LightModelName = "OptSpecular0";
            lodItem.AlphaTestMode = 0;
            lodItem.TexAddrModeName = "Wrap";
            lodItem.ESD_Alternative_Texture = 0;
            lodItem.MipMapLevelOfDetailBias = 0;
            LODItem.LoadMaterial(viewer, lodItem);
            var gauge = viewer.Settings.SuperElevationGauge / 1000f;
            var inner = gauge / 2f;
            var outer = inner + 0.15f * gauge / 1.435f;

            pl = new Polyline(this, "left_outer", 2);
            pl.DeltaTexCoord = new Vector2(.1673372f, 0f);
            pl.Vertices.Add(new Vertex(-outer, .200f, 0.0f, -1f, 0f, 0f, -.139362f, .101563f));
            pl.Vertices.Add(new Vertex(-outer, .325f, 0.0f, -1f, 0f, 0f, -.139363f, .003906f));
            lodItem.Polylines.Add(pl);
            lodItem.Accum(pl.Vertices.Count);

            pl = new Polyline(this, "left_inner", 2);
            pl.DeltaTexCoord = new Vector2(.1673372f, 0f);
            pl.Vertices.Add(new Vertex(-inner, .325f, 0.0f, 1f, 0f, 0f, -.139363f, .003906f));
            pl.Vertices.Add(new Vertex(-inner, .200f, 0.0f, 1f, 0f, 0f, -.139362f, .101563f));
            lodItem.Polylines.Add(pl);
            lodItem.Accum(pl.Vertices.Count);

            pl = new Polyline(this, "right_inner", 2);
            pl.DeltaTexCoord = new Vector2(.1673372f, 0f);
            pl.Vertices.Add(new Vertex(inner, .200f, 0.0f, -1f, 0f, 0f, -.139362f, .101563f));
            pl.Vertices.Add(new Vertex(inner, .325f, 0.0f, -1f, 0f, 0f, -.139363f, .003906f));
            lodItem.Polylines.Add(pl);
            lodItem.Accum(pl.Vertices.Count);

            pl = new Polyline(this, "right_outer", 2);
            pl.DeltaTexCoord = new Vector2(.1673372f, 0f);
            pl.Vertices.Add(new Vertex(outer, .325f, 0.0f, 1f, 0f, 0f, -.139363f, .003906f));
            pl.Vertices.Add(new Vertex(outer, .200f, 0.0f, 1f, 0f, 0f, -.139362f, .101563f));
            lodItem.Polylines.Add(pl);
            lodItem.Accum(pl.Vertices.Count);

            lod.LODItems.Add(lodItem); // Append this LODItem to LODItems array
            LODs.Add(lod); // Append this LOD to LODs array

            // RAILTOPS
            lod = new LOD(1200.0f); // Create LOD for railtops with specified CutoffRadius
            // Single LODItem in this case
            lodItem = new LODItem("Railtops");
            lodItem.TexName = "acleantrack2.ace";
            lodItem.ShaderName = "TexDiff";
            lodItem.LightModelName = "OptSpecular25";
            lodItem.AlphaTestMode = 0;
            lodItem.TexAddrModeName = "Wrap";
            lodItem.ESD_Alternative_Texture = 0;
            lodItem.MipMapLevelOfDetailBias = 0;
            LODItem.LoadMaterial(viewer, lodItem);

            pl = new Polyline(this, "right", 2);
            pl.DeltaTexCoord = new Vector2(.0744726f, 0f);
            pl.Vertices.Add(new Vertex(-outer, .325f, 0.0f, 0f, 1f, 0f, .232067f, .126953f));
            pl.Vertices.Add(new Vertex(-inner, .325f, 0.0f, 0f, 1f, 0f, .232067f, .224609f));
            lodItem.Polylines.Add(pl);
            lodItem.Accum(pl.Vertices.Count);

            pl = new Polyline(this, "left", 2);
            pl.DeltaTexCoord = new Vector2(.0744726f, 0f);
            pl.Vertices.Add(new Vertex(inner, .325f, 0.0f, 0f, 1f, 0f, .232067f, .126953f));
            pl.Vertices.Add(new Vertex(outer, .325f, 0.0f, 0f, 1f, 0f, .232067f, .224609f));
            lodItem.Polylines.Add(pl);
            lodItem.Accum(pl.Vertices.Count);

            lod.LODItems.Add(lodItem); // Append this LODItem to LODItems array
            LODs.Add(lod); // Append this LOD to LODs array

            // BALLAST
            lod = new LOD(float.MaxValue); // Create LOD for ballast with specified CutoffRadius (infinite)
            // Single LODItem in this case
            lodItem = new LODItem("Ballast");
            lodItem.TexName = "acleantrack1.ace";
            lodItem.ShaderName = "BlendATexDiff";
            lodItem.LightModelName = "OptSpecular0";
            lodItem.AlphaTestMode = 0;
            lodItem.TexAddrModeName = "Wrap";
            lodItem.ESD_Alternative_Texture = (int)Helpers.TextureFlags.SnowTrack; // Match MSTS global road/track behaviour.
            lodItem.MipMapLevelOfDetailBias = -1f;
            LODItem.LoadMaterial(viewer, lodItem);

            pl = new Polyline(this, "ballast", 2);
            pl.DeltaTexCoord = new Vector2(0.0f, 0.2088545f);
            pl.Vertices.Add(new Vertex(-2.5f * gauge / 1.435f, 0.2f, 0.0f, 0f, 1f, 0f, -.153916f, -.280582f));
            pl.Vertices.Add(new Vertex(2.5f * gauge / 1.435f, 0.2f, 0.0f, 0f, 1f, 0f, .862105f, -.280582f));
            lodItem.Polylines.Add(pl);
            lodItem.Accum(pl.Vertices.Count);

            lod.LODItems.Add(lodItem); // Append this LODItem to LODItems array
            LODs.Add(lod); // Append this LOD to LODs array
        }

        /// <summary>
        /// TrProfile constructor from STFReader-style profile file
        /// </summary>
        public TrProfile(Viewer viewer, STFReader stf)
        {
            Name = "Default Dynatrack profile";

            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("name", ()=>{ Name = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("lodmethod", ()=> { LODMethod = GetLODMethod(stf.ReadStringBlock(null)); }),
                new STFReader.TokenProcessor("chordspan", ()=>{ ChordSpan = stf.ReadFloatBlock(STFReader.Units.Distance, null); }),
                new STFReader.TokenProcessor("pitchcontrol", ()=> { PitchControl = GetPitchControl(stf.ReadStringBlock(null)); }),
                new STFReader.TokenProcessor("pitchcontrolscalar", ()=>{ PitchControlScalar = stf.ReadFloatBlock(STFReader.Units.Distance, null); }),
                new STFReader.TokenProcessor("lod", ()=> { LODs.Add(new LOD(viewer, stf)); }),
            });

            if (LODs.Count == 0) throw new InvalidDataException("missing LODs");
        }

        /// <summary>
        /// TrProfile constructor from XML profile file
        /// </summary>
        public TrProfile(Viewer viewer, XmlReader reader)
        {
            if (reader.IsStartElement())
            {
                if (reader.Name == "TrProfile")
                {
                    // root
                    Name = reader.GetAttribute("Name");
                    LODMethod = GetLODMethod(reader.GetAttribute("LODMethod"));
                    ChordSpan = float.Parse(reader.GetAttribute("ChordSpan"));
                    PitchControl = GetPitchControl(reader.GetAttribute("PitchControl"));
                    PitchControlScalar = float.Parse(reader.GetAttribute("PitchControlScalar"));
                }
                else
                {
                    //TODO: Need to handle ill-formed XML profile
                }
            }
            LOD lod = null;
            LODItem lodItem = null;
            Polyline pl = null;
            Vertex v;
            string[] s;
            char[] sep = new char[] { ' ' };
            while (reader.Read())
            {
                if (reader.IsStartElement())
                {
                    switch (reader.Name)
                    {
                        case "LOD":
                            lod = new LOD(float.Parse(reader.GetAttribute("CutoffRadius")));
                            LODs.Add(lod);
                            break;
                        case "LODItem":
                            lodItem = new LODItem(reader.GetAttribute("Name"));
                            lodItem.TexName = reader.GetAttribute("TexName");

                            lodItem.ShaderName = reader.GetAttribute("ShaderName");
                            lodItem.LightModelName = reader.GetAttribute("LightModelName");
                            lodItem.AlphaTestMode = int.Parse(reader.GetAttribute("AlphaTestMode"));
                            lodItem.TexAddrModeName = reader.GetAttribute("TexAddrModeName");
                            lodItem.ESD_Alternative_Texture = int.Parse(reader.GetAttribute("ESD_Alternative_Texture"));
                            lodItem.MipMapLevelOfDetailBias = float.Parse(reader.GetAttribute("MipMapLevelOfDetailBias"));

                            LODItem.LoadMaterial(viewer, lodItem);
                            lod.LODItems.Add(lodItem);
                            break;
                        case "Polyline":
                            pl = new Polyline();
                            pl.Name = reader.GetAttribute("Name");
                            s = reader.GetAttribute("DeltaTexCoord").Split(sep, StringSplitOptions.RemoveEmptyEntries);
                            pl.DeltaTexCoord = new Vector2(float.Parse(s[0]), float.Parse(s[1]));
                            lodItem.Polylines.Add(pl);
                            break;
                        case "Vertex":
                            v = new Vertex();
                            s = reader.GetAttribute("Position").Split(sep, StringSplitOptions.RemoveEmptyEntries);
                            v.Position = new Vector3(float.Parse(s[0]), float.Parse(s[1]), float.Parse(s[2]));
                            s = reader.GetAttribute("Normal").Split(sep, StringSplitOptions.RemoveEmptyEntries);
                            v.Normal = new Vector3(float.Parse(s[0]), float.Parse(s[1]), float.Parse(s[2]));
                            s = reader.GetAttribute("TexCoord").Split(sep, StringSplitOptions.RemoveEmptyEntries);
                            v.TexCoord = new Vector2(float.Parse(s[0]), float.Parse(s[1]));
                            pl.Vertices.Add(v);
                            lodItem.NumVertices++; // Bump vertex count
                            if (pl.Vertices.Count > 1) lodItem.NumSegments++;
                            break;
                        default:
                            break;
                    }
                }
            }
            if (LODs.Count == 0) throw new InvalidDataException("missing LODs");
        }

        /// <summary>
        /// TrProfile constructor (default - builds from self-contained data)
        /// <param name="viewer">Viewer3D.</param>
        /// <param name="x">Parameter x is a placeholder.</param>
        /// </summary>
        public TrProfile(Viewer viewer, int x)
        {
            // Default TrProfile constructor
            Name = "Default Dynatrack profile";
        }

        /// <summary>
        /// Gets a member of the LODMethods enumeration that corresponds to sLODMethod.
        /// </summary>
        /// <param name="sLODMethod">String that identifies desired LODMethod.</param>
        /// <returns>LODMethod</returns>
        public static LODMethods GetLODMethod(string sLODMethod)
        {
            string s = sLODMethod.ToLower();
            switch (s)
            {
                case "none":
                    return LODMethods.None;

                case "completereplacement":
                    return LODMethods.CompleteReplacement;

                case "componentadditive":
                default:
                    return LODMethods.ComponentAdditive;
            }
        }

        /// <summary>
        /// Gets a member of the PitchControls enumeration that corresponds to sPitchControl.
        /// </summary>
        /// <param name="sPitchControl">String that identifies desired PitchControl.</param>
        /// <returns></returns>
        public static PitchControls GetPitchControl(string sPitchControl)
        {
            string s = sPitchControl.ToLower();
            switch (s)
            {
                case "chordlength":
                    return PitchControls.ChordLength;

                case "chorddisplacement":
                    return PitchControls.ChordDisplacement;

                case "none":
                default:
                    return PitchControls.None; ;

            }
        }
    }

    public class LOD
    {
        public float CutoffRadius; // Distance beyond which LODItem is not seen
        public List<LODItem> LODItems { get; } = new List<LODItem>(); // Array of arrays of LODItems
        public int PrimIndexStart; // Start index of ShapePrimitive block for this LOD
        public int PrimIndexStop;

        /// <summary>
        /// LOD class constructor
        /// </summary>
        /// <param name="cutoffRadius">Distance beyond which LODItem is not seen</param>
        public LOD(float cutoffRadius)
        {
            CutoffRadius = cutoffRadius;
        }

        public LOD(Viewer viewer, STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("cutoffradius", ()=>{ CutoffRadius = stf.ReadFloatBlock(STFReader.Units.Distance, null); }),
                new STFReader.TokenProcessor("loditem", ()=>{
                    LODItem lodItem = new LODItem(viewer, stf);
                    LODItems.Add(lodItem); // Append to Polylines array
                    }),
            });
            if (CutoffRadius == 0) throw new InvalidDataException("missing CutoffRadius");
        }

        public void Mark()
        {
            foreach (LODItem lodItem in LODItems)
                lodItem.Mark();
        }
    }

    public class LODItem
    {
        public List<Polyline> Polylines { get; } = new List<Polyline>();  // Array of arrays of vertices 

        public string Name;                            // e.g., "Rail sides"
        public string ShaderName;
        public string LightModelName;
        public int AlphaTestMode;
        public string TexAddrModeName;
        public int ESD_Alternative_Texture; // Equivalent to that of .sd file
        public float MipMapLevelOfDetailBias;

        public string TexName; // Texture file name

        public Material LODMaterial; // SceneryMaterial reference

        // NumVertices and NumSegments used for sizing vertex and index buffers
        public uint NumVertices;                     // Total independent vertices in LOD
        public uint NumSegments;                     // Total line segment count in LOD

        /// <summary>
        /// LODITem constructor (used for default and XML-style profiles)
        /// </summary>
        public LODItem(string name)
        {
            Name = name;
        }

        /// <summary>
        /// LODITem constructor (used for STF-style profile)
        /// </summary>
        public LODItem(Viewer viewer, STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("name", ()=>{ Name = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("texname", ()=>{ TexName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("shadername", ()=>{ ShaderName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("lightmodelname", ()=>{ LightModelName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("alphatestmode", ()=>{ AlphaTestMode = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("texaddrmodename", ()=>{ TexAddrModeName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("esd_alternative_texture", ()=>{ ESD_Alternative_Texture = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("mipmaplevelofdetailbias", ()=>{ MipMapLevelOfDetailBias = stf.ReadFloatBlock(STFReader.Units.None, null); }),
                new STFReader.TokenProcessor("polyline", ()=>{
                    Polyline pl = new Polyline(stf);
                    Polylines.Add(pl); // Append to Polylines array
                    //parent.Accum(pl.Vertices.Count); }),
                    Accum(pl.Vertices.Count); }),
            });

            // Checks for required member variables:
            // Name not required.
            // MipMapLevelOfDetail bias initializes to 0.
            if (Polylines.Count == 0) throw new InvalidDataException("missing Polylines");

            LoadMaterial(viewer, this);
        }

        public void Accum(int count)
        {
            // Accumulates total independent vertices and total line segments
            // Used for sizing of vertex and index buffers
            NumVertices += (uint)count;
            NumSegments += (uint)count - 1;
        }

        public static void LoadMaterial(Viewer viewer, LODItem lod)
        {
            var options = Helpers.EncodeMaterialOptions(lod);
            lod.LODMaterial = viewer.MaterialManager.Load("Scenery", Helpers.GetRouteTextureFile((Helpers.TextureFlags)lod.ESD_Alternative_Texture, lod.TexName), (int)options, lod.MipMapLevelOfDetailBias);
        }

        public void Mark()
        {
            LODMaterial.Mark();
        }
    }

    public class Polyline
    {
        public List<Vertex> Vertices { get; } = new List<Vertex>();    // Array of vertices 

        public string Name;                             // e.g., "1:1 embankment"
        public Vector2 DeltaTexCoord;                   // Incremental change in (u, v) from one cross section to the next

        /// <summary>
        /// Polyline constructor (DAT)
        /// </summary>
        public Polyline(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("name", ()=>{ Name = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("vertex", ()=>{ Vertices.Add(new Vertex(stf)); }),
                new STFReader.TokenProcessor("deltatexcoord", ()=>{
                    stf.MustMatchBlockStart();
                    DeltaTexCoord.X = stf.ReadFloat(STFReader.Units.None, null);
                    DeltaTexCoord.Y = stf.ReadFloat(STFReader.Units.None, null);
                    stf.SkipRestOfBlock();
                }),
            });
            // Checks for required member variables: 
            // Name not required.
            if (DeltaTexCoord == Vector2.Zero) throw new InvalidDataException("missing DeltaTexCoord");
            if (Vertices.Count == 0) throw new InvalidDataException("missing Vertices");
        }

        /// <summary>
        /// Bare-bones Polyline constructor (used for XML)
        /// </summary>
        public Polyline()
        {
        }

        /// <summary>
        /// Polyline constructor (default)
        /// </summary>
        public Polyline(TrProfile parent, string name, uint num)
        {
            Name = name;
        }
    }

    public struct Vertex
    {
        public Vector3 Position;                           // Position vector (x, y, z)
        public Vector3 Normal;                             // Normal vector (nx, ny, nz)
        public Vector2 TexCoord;                           // Texture coordinate (u, v)

        // Vertex constructor (default)
        public Vertex(float x, float y, float z, float nx, float ny, float nz, float u, float v)
        {
            Position = new Vector3(x, y, z);
            Normal = new Vector3(nx, ny, nz);
            TexCoord = new Vector2(u, v);
        }

        // Vertex constructor (DAT)
        public Vertex(STFReader stf)
        {
            Vertex v = new Vertex(); // Temp variable used to construct the struct in ParseBlock
            v.Position = new Vector3();
            v.Normal = new Vector3();
            v.TexCoord = new Vector2();
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("position", ()=>{
                    stf.MustMatchBlockStart();
                    v.Position.X = stf.ReadFloat(STFReader.Units.None, null);
                    v.Position.Y = stf.ReadFloat(STFReader.Units.None, null);
                    v.Position.Z = 0.0f;
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("normal", ()=>{
                    stf.MustMatchBlockStart();
                    v.Normal.X = stf.ReadFloat(STFReader.Units.None, null);
                    v.Normal.Y = stf.ReadFloat(STFReader.Units.None, null);
                    v.Normal.Z = stf.ReadFloat(STFReader.Units.None, null);
                    stf.SkipRestOfBlock();
                }),
                new STFReader.TokenProcessor("texcoord", ()=>{
                    stf.MustMatchBlockStart();
                    v.TexCoord.X = stf.ReadFloat(STFReader.Units.None, null);
                    v.TexCoord.Y = stf.ReadFloat(STFReader.Units.None, null);
                    stf.SkipRestOfBlock();
                }),
            });
            this = v;
            // Checks for required member variables
            // No way to check for missing Position.
            if (Normal == Vector3.Zero) throw new Exception("improper Normal");
            // No way to check for missing TexCoord
        }
    }

    public class DynamicTrackPrimitive : ShapePrimitive //RenderPrimitive
    {
        public ShapePrimitive[] ShapePrimitives; // Array of ShapePrimitives

        public VertexPositionNormalTexture[] VertexList; // Array of vertices
        public short[] TriangleListIndices;// Array of indices to vertices for triangles
        public uint VertexIndex;           // Index of current position in VertexList
        public uint IndexIndex;            // Index of current position in TriangleListIndices
        public int NumVertices;            // Number of vertices in the track profile
        public short NumIndices;           // Number of triangle indices

        // LOD member variables:
        //public int FirstIndex;       // Marks first LOD that is in-range
        public Vector3 XNAEnd;      // Location of termination-of-section (as opposed to root)
        public float ObjectRadius;  // Radius of bounding sphere
        public Vector3 MSTSLODCenter; // Center of bounding sphere

        // Geometry member variables:
        public int NumSections;            // Number of cross sections needed to make up a track section.
        public float SegmentLength;        // meters if straight; radians if circular arc
        public Vector3 DDY;                // Elevation (y) change from one cross section to next
        public Vector3 OldV;               // Deviation from centerline for previous cross section
        public Vector3 OldRadius;          // Radius vector to centerline for previous cross section

        //TODO: Candidates for re-packaging:
        public Matrix sectionRotation;     // Rotates previous profile into next profile position on curve.
        public Vector3 center;             // Center coordinates of curve radius
        public Vector3 radius;             // Radius vector to cross section on curve centerline

        // This structure holds the basic geometric parameters of a DT section.
        public readonly struct DtrackData
        {
            /// <summary>
            /// Straight (0) or circular arc (1)
            /// </summary>
            public readonly bool IsCurved;
            /// <summary>
            /// Length in meters (straight) or radians (circular arc)
            /// </summary>
            public readonly float Length;
            /// <summary>
            /// Radius for circular arc
            /// </summary>
            public readonly float Radius;

            public DtrackData(bool isCurved, float length, float radius)
            {
                IsCurved = isCurved;
                Length = length;
                Radius = radius;
            }
        }

        public DtrackData DTrackData { get; set; }      // Was: DtrackData[] dtrackData;

        public int UiD { get; } // Used for debugging only

        public TrProfile TrProfile { get; set; }

        /// <summary>
        /// Default constructor
        /// </summary>
        public DynamicTrackPrimitive()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        public DynamicTrackPrimitive(Viewer viewer, DynamicTrackObject track, in WorldPosition worldPosition, in WorldPosition endPosition)
        {
            // DynamicTrackPrimitive is responsible for creating a mesh for a section with a single subsection.
            // It also must update worldPosition to reflect the end of this subsection, subsequently to
            // serve as the beginning of the next subsection.

            UiD = track.TrackSections[0].SectionIndex; // Used for debugging only

            // The track cross section (profile) vertex coordinates are hard coded.
            // The coordinates listed here are those of default MSTS "A1t" track.
            // TODO: Read this stuff from a file. Provide the ability to use alternative profiles.

            // In this implementation dtrack has only 1 DT subsection.
            if (track.TrackSections.Count != 1)
            {
                throw new InvalidDataException($"DynamicTrackPrimitive Constructor detected a multiple-subsection dynamic track section. (SectionIdx = {track.SectionIndex})");
            }
            // Populate member DTrackData (a DtrackData struct)
            DTrackData = new DtrackData(track.TrackSections[0].Curved, track.TrackSections[0].Curved ? track.TrackSections[0].Angle : track.TrackSections[0].Length, track.TrackSections[0].Radius);

            XNAEnd = endPosition.XNAMatrix.Translation;

            TrProfile = viewer.TRP.TrackProfile;
            // Count all of the LODItems in all the LODs
            int count = TrProfile.LODs.Sum(lod => lod.LODItems.Count);

            // Allocate ShapePrimitives array for the LOD count
            ShapePrimitives = new ShapePrimitive[count];

            // Build the meshes for all the LODs, filling the vertex and triangle index buffers.
            int primIndex = 0;
            for (int iLOD = 0; iLOD < TrProfile.LODs.Count; iLOD++)
            {
                LOD lod = TrProfile.LODs[iLOD];
                lod.PrimIndexStart = primIndex; // Store start index for this LOD
                for (int iLODItem = 0; iLODItem < lod.LODItems.Count; iLODItem++)
                {
                    // Build vertexList and triangleListIndices
                    ShapePrimitives[primIndex] = BuildPrimitive(viewer, worldPosition, iLOD, iLODItem);
                    primIndex++;
                }
                lod.PrimIndexStop = primIndex; // 1 above last index for this LOD
            }

            if (!DTrackData.IsCurved) ObjectRadius = 0.5f * DTrackData.Length; // half-length
            else ObjectRadius = DTrackData.Radius * (float)Math.Sin(0.5 * Math.Abs(DTrackData.Length)); // half chord length
        }

        public override void Mark()
        {
            foreach (var prim in ShapePrimitives)
                prim.Mark();
            base.Mark();
        }

        /// <summary>
        /// Builds a Dynatrack LOD to TrProfile specifications as one vertex buffer and one index buffer.
        /// The order in which the buffers are built reflects the nesting in the TrProfile.  The nesting order is:
        /// (Polylines (Vertices)).  All vertices and indices are built contiguously for an LOD.
        /// </summary>
        /// <param name="viewer">Viewer.</param>
        /// <param name="worldPosition">WorldPosition.</param>
        /// <param name="lodIndex">Index of LOD mesh to be generated from profile.</param>
        /// <param name="lodItemIndex">Index of LOD mesh following LODs[iLOD]</param>
        public ShapePrimitive BuildPrimitive(Viewer viewer, in WorldPosition worldPosition, int lodIndex, int lodItemIndex)
        {
            // Call for track section to initialize itself
            if (!DTrackData.IsCurved) LinearGen();
            else CircArcGen();

            // Count vertices and indices
            LOD lod = TrProfile.LODs[lodIndex];
            LODItem lodItem = (LODItem)lod.LODItems[lodItemIndex];
            NumVertices = (int)(lodItem.NumVertices * (NumSections + 1));
            NumIndices = (short)(lodItem.NumSegments * NumSections * 6);
            // (Cells x 2 triangles/cell x 3 indices/triangle)

            // Allocate memory for vertices and indices
            VertexList = new VertexPositionNormalTexture[NumVertices]; // numVertices is now aggregate
            TriangleListIndices = new short[NumIndices]; // as is NumIndices

            // Build the mesh for lod
            VertexIndex = 0;
            IndexIndex = 0;
            // Initial load of baseline cross section polylines for this LOD only:
            foreach (Polyline pl in lodItem.Polylines)
            {
                foreach (Vertex v in pl.Vertices)
                {
                    VertexList[VertexIndex].Position = v.Position;
                    VertexList[VertexIndex].Normal = v.Normal;
                    VertexList[VertexIndex].TextureCoordinate = v.TexCoord;
                    VertexIndex++;
                }
            }
            // Initial load of base cross section complete

            // Now generate and load subsequent cross sections
            OldRadius = -center;
            uint stride = VertexIndex;
            for (uint i = 0; i < NumSections; i++)
            {
                foreach (Polyline pl in lodItem.Polylines)
                {
                    uint plv = 0; // Polyline vertex index
                    foreach (Vertex v in pl.Vertices)
                    {
                        if (!DTrackData.IsCurved) LinearGen(stride, pl); // Generation call
                        else CircArcGen(stride, pl);

                        if (plv > 0)
                        {
                            // Sense for triangles is clockwise
                            // First triangle:
                            TriangleListIndices[IndexIndex++] = (short)VertexIndex;
                            TriangleListIndices[IndexIndex++] = (short)(VertexIndex - 1 - stride);
                            TriangleListIndices[IndexIndex++] = (short)(VertexIndex - 1);
                            // Second triangle:
                            TriangleListIndices[IndexIndex++] = (short)VertexIndex;
                            TriangleListIndices[IndexIndex++] = (short)(VertexIndex - stride);
                            TriangleListIndices[IndexIndex++] = (short)(VertexIndex - 1 - stride);
                        }
                        VertexIndex++;
                        plv++;
                    }
                }
                OldRadius = radius; // Get ready for next segment
            }

            // Create and populate a new ShapePrimitive
            var indexBuffer = new IndexBuffer(viewer.Game.GraphicsDevice, typeof(short), NumIndices, BufferUsage.WriteOnly);
            indexBuffer.SetData(TriangleListIndices);
            return new ShapePrimitive(viewer.Game.GraphicsDevice, lodItem.LODMaterial, new SharedShape.VertexBufferSet(VertexList, viewer.Game.GraphicsDevice), indexBuffer, 0, NumVertices, NumIndices / 3, new[] { -1 }, 0);
        }

        /// <summary>
        /// Initializes member variables for straight track sections.
        /// </summary>
        private void LinearGen()
        {
            // Define the number of track cross sections in addition to the base.
            NumSections = 1;
            //numSections = 10; //TESTING
            // TODO: Generalize count to profile file specification

            SegmentLength = DTrackData.Length / NumSections; // Length of each mesh segment (meters)
            DDY = new Vector3();// new Vector3(0, DTrackData.DeltaElevation / NumSections, 0); // Incremental elevation change
        }

        /// <summary>
        /// Initializes member variables for circular arc track sections.
        /// </summary>
        private void CircArcGen()
        {
            // Define the number of track cross sections in addition to the base.
            NumSections = (int)(Math.Abs(MathHelper.ToDegrees(DTrackData.Length)) / TrProfile.ChordSpan);
            if (NumSections == 0) NumSections++; // Very small radius track - zero avoidance

            // Use pitch control methods
            switch (TrProfile.PitchControl)
            {
                case TrProfile.PitchControls.None:
                    break; // Good enough
                case TrProfile.PitchControls.ChordLength:
                    // Calculate chord length for NumSections
                    float l = 2.0f * DTrackData.Radius * (float)Math.Sin(0.5f * Math.Abs(DTrackData.Length) / NumSections);
                    if (l > TrProfile.PitchControlScalar)
                    {
                        // Number of sections determined by chord length of PitchControlScalar meters
                        float chordAngle = 2.0f * (float)Math.Asin(0.5f * TrProfile.PitchControlScalar / DTrackData.Radius);
                        NumSections = (int)Math.Abs((DTrackData.Length / chordAngle));
                    }
                    break;
                case TrProfile.PitchControls.ChordDisplacement:
                    // Calculate chord displacement for NumSections
                    float d = DTrackData.Radius * (float)(1.0f - Math.Cos(0.5f * Math.Abs(DTrackData.Length) / NumSections));
                    if (d > TrProfile.PitchControlScalar)
                    {
                        // Number of sections determined by chord displacement of PitchControlScalar meters
                        float chordAngle = 2.0f * (float)Math.Acos(1.0f - TrProfile.PitchControlScalar / DTrackData.Radius);
                        NumSections = (int)Math.Abs((DTrackData.Length / chordAngle));
                    }
                    break;
            }

            SegmentLength = DTrackData.Length / NumSections; // Length of each mesh segment (radians)
            DDY = new Vector3();//new Vector3(0, DTrackData.DeltaElevation / NumSections, 0); // Incremental elevation change

            // The approach here is to replicate the previous cross section, 
            // rotated into its position on the curve and vertically displaced if on grade.
            // The local center for the curve lies to the left or right of the local origin and ON THE BASE PLANE
            center = DTrackData.Radius * (DTrackData.Length < 0 ? Vector3.Left : Vector3.Right);
            sectionRotation = Matrix.CreateRotationY(-SegmentLength); // Rotation per iteration (constant)
        }

        /// <summary>
        /// Generates vertices for a succeeding cross section (straight track).
        /// </summary>
        /// <param name="stride">Index increment between section-to-section vertices.</param>
        /// <param name="pl">Polyline.</param>
        public void LinearGen(uint stride, Polyline pl)
        {
            Vector3 displacement = new Vector3(0, 0, -SegmentLength) + DDY;
            float wrapLength = displacement.Length();
            Vector2 uvDisplacement = pl.DeltaTexCoord * wrapLength;

            Vector3 p = VertexList[VertexIndex - stride].Position + displacement;
            Vector3 n = VertexList[VertexIndex - stride].Normal;
            Vector2 uv = VertexList[VertexIndex - stride].TextureCoordinate + uvDisplacement;

            VertexList[VertexIndex].Position = new Vector3(p.X, p.Y, p.Z);
            VertexList[VertexIndex].Normal = new Vector3(n.X, n.Y, n.Z);
            VertexList[VertexIndex].TextureCoordinate = new Vector2(uv.X, uv.Y);
        }

        /// <summary>
        /// /// Generates vertices for a succeeding cross section (circular arc track).
        /// </summary>
        /// <param name="stride">Index increment between section-to-section vertices.</param>
        /// <param name="pl">Polyline.</param>
        public void CircArcGen(uint stride, Polyline pl)
        {
            // Get the previous vertex about the local coordinate system
            OldV = VertexList[VertexIndex - stride].Position - center - OldRadius;
            // Rotate the old radius vector to become the new radius vector
            radius = Vector3.Transform(OldRadius, sectionRotation);
            float wrapLength = (radius - OldRadius).Length(); // Wrap length is centerline chord
            Vector2 uvDisplacement = pl.DeltaTexCoord * wrapLength;

            // Rotate the point about local origin and reposition it (including elevation change)
            Vector3 p = DDY + center + radius + Vector3.Transform(OldV, sectionRotation);
            Vector3 n = VertexList[VertexIndex - stride].Normal;
            Vector2 uv = VertexList[VertexIndex - stride].TextureCoordinate + uvDisplacement;

            VertexList[VertexIndex].Position = new Vector3(p.X, p.Y, p.Z);
            VertexList[VertexIndex].Normal = new Vector3(n.X, n.Y, n.Z);
            VertexList[VertexIndex].TextureCoordinate = new Vector2(uv.X, uv.Y);
        }
    }
}
