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

/* OVERHEAD WIRE
 * 
 * Overhead wire is generated procedurally from data in the track database.
 * 
 */

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation;

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Orts.ActivityRunner.Viewer3D
{
    public static class Wire
    {
        /// <summary>
        /// Decompose and add a wire on top of MSTS track section
        /// </summary>
        /// <param name="viewer">Viewer reference.</param>
        /// <param name="trackList">DynamicTrackViewer list.</param>
        /// <param name="trackObj">Dynamic track section to decompose.</param>
        /// <param name="worldMatrixInput">Position matrix.</param>
        public static int DecomposeStaticWire(Viewer viewer, List<DynamicTrackViewer> trackList, TrackObject trackObj, in WorldPosition worldMatrixInput)
        {
            // The following vectors represent local positioning relative to root of original (5-part) section:
            Vector3 localV = Vector3.Zero; // Local position (in x-z plane)
            Vector3 localProjectedV; // Local next position (in x-z plane)
            Vector3 displacement;  // Local displacement (from y=0 plane)
            Vector3 heading = Vector3.Forward; // Local heading (unit vector)

            WorldPosition nextRoot = worldMatrixInput; // Will become initial root

            WorldPosition wcopy = nextRoot;
            Vector3 sectionOrigin = worldMatrixInput.XNAMatrix.Translation; // Save root position
            WorldPosition worldMatrix = worldMatrixInput.SetTranslation(Vector3.Zero); // worldMatrix now rotation-only
            try
            {
                if (RuntimeData.Instance.TSectionDat.TrackShapes[trackObj.SectionIndex].RoadShape == true)
                    return 1;
            }
            catch (Exception)
            {
                return 0;
            }
            SectionIndex[] SectionIdxs = RuntimeData.Instance.TSectionDat.TrackShapes[trackObj.SectionIndex].SectionIndices;

            foreach (SectionIndex id in SectionIdxs)
            {
                nextRoot = wcopy; // Will become initial root
                sectionOrigin = nextRoot.XNAMatrix.Translation;

                heading = Vector3.Forward; // Local heading (unit vector)
                localV = Vector3.Zero; // Local position (in x-z plane)


                ref readonly Vector3 trackLoc = ref id.Offset;
                Matrix trackRot = Matrix.CreateRotationY(-MathHelper.ToRadians(id.AngularOffset));

                heading = Vector3.Transform(heading, trackRot); // Heading change
                nextRoot = new WorldPosition(nextRoot.Tile, MatrixExtension.Multiply(trackRot, nextRoot.XNAMatrix));
                int[] sections = id.TrackSections;

                for (int i = 0; i < sections.Length; i++)
                {
                    float length, radius;
                    int sid = id.TrackSections[i];
                    TrackSection section = RuntimeData.Instance.TSectionDat.TrackSections[sid];
                    WorldPosition root = nextRoot;
                    nextRoot = nextRoot.SetTranslation(Vector3.Zero);

                    if (!section.Curved)
                    {
                        length = section.Length;
                        radius = -1;
                        localProjectedV = localV + length * heading;
                        displacement = InterpolateHelper.MSTSInterpolateAlongStraight(localV, heading, length,
                                                                worldMatrix.XNAMatrix, out localProjectedV);
                    }
                    else
                    {
                        length = MathHelper.ToRadians(section.Angle);
                        radius = section.Radius; // meters

                        Vector3 left;
                        if (section.Angle > 0)
                            left = radius * Vector3.Cross(Vector3.Down, heading); // Vector from PC to O
                        else
                            left = radius * Vector3.Cross(Vector3.Up, heading); // Vector from PC to O
                        Matrix rot = Matrix.CreateRotationY(-MathHelper.ToRadians(section.Angle)); // Heading change (rotation about O)

                        displacement = InterpolateHelper.MSTSInterpolateAlongCurve(localV, left, rot,
                                                worldMatrix.XNAMatrix, out localProjectedV);

                        heading = Vector3.Transform(heading, rot); // Heading change
                        nextRoot = new WorldPosition(nextRoot.Tile, MatrixExtension.Multiply(rot, nextRoot.XNAMatrix)); // Store heading change

                    }
                    nextRoot = nextRoot.SetTranslation(sectionOrigin + displacement);
                    root = root.SetTranslation(root.XNAMatrix.Translation + Vector3.Transform(trackLoc, worldMatrix.XNAMatrix));
                    //nextRoot.XNAMatrix.Translation += Vector3.Transform(trackLoc, worldMatrix.XNAMatrix);
                    trackList.Add(new WireViewer(viewer, root, nextRoot, radius, length));
                    localV = localProjectedV; // Next subsection
                }
            }
            return 1;
        }

        /// <summary>
        /// Decompose and add a wire on top of MSTS track section converted from dynamic tracks
        /// </summary>
        /// <param name="viewer">Viewer reference.</param>
        /// <param name="trackList">DynamicTrackViewer list.</param>
        /// <param name="trackObj">Dynamic track section to decompose.</param>
        /// <param name="worldMatrixInput">Position matrix.</param>
        public static void DecomposeConvertedDynamicWire(Viewer viewer, List<DynamicTrackViewer> trackList, TrackObject trackObj, in WorldPosition worldMatrixInput)
        {
            // The following vectors represent local positioning relative to root of original (5-part) section:
            Vector3 localV = Vector3.Zero; // Local position (in x-z plane)
            Vector3 localProjectedV; // Local next position (in x-z plane)
            Vector3 displacement;  // Local displacement (from y=0 plane)
            Vector3 heading = Vector3.Forward; // Local heading (unit vector)

            WorldPosition nextRoot = worldMatrixInput; // Will become initial root

            WorldPosition wcopy = nextRoot;
            Vector3 sectionOrigin = worldMatrixInput.XNAMatrix.Translation; // Save root position
            WorldPosition worldMatrix = worldMatrixInput.SetTranslation(Vector3.Zero); // worldMatrix now rotation-only

            TrackPath path;

            try
            {
                path = RuntimeData.Instance.TSectionDat.TrackSectionIndex[trackObj.SectionIndex];
            }
            catch (Exception)
            {
                return; //cannot find the path for the dynamic track
            }

            nextRoot = wcopy; // Will become initial root
            sectionOrigin = nextRoot.XNAMatrix.Translation;

            heading = Vector3.Forward; // Local heading (unit vector)
            localV = Vector3.Zero; // Local position (in x-z plane)


            Vector3 trackLoc = new Vector3(0, 0, 0);// +new Vector3(3, 0, 0);
            Matrix trackRot = Matrix.CreateRotationY(0);

            //heading = Vector3.Transform(heading, trackRot); // Heading change
            nextRoot = new WorldPosition(nextRoot.Tile, MatrixExtension.Multiply(trackRot, nextRoot.XNAMatrix));
            int[] sections = path.TrackSections;

            for (int i = 0; i < sections.Length; i++)
            {
                float length, radius;
                int sid = path.TrackSections[i];
                TrackSection section = RuntimeData.Instance.TSectionDat.TrackSections[sid];
                WorldPosition root = nextRoot;
                nextRoot = nextRoot.SetTranslation(Vector3.Zero);

                if (!section.Curved)
                {
                    length = section.Length;
                    radius = -1;
                    localProjectedV = localV + length * heading;
                    displacement = InterpolateHelper.MSTSInterpolateAlongStraight(localV, heading, length,
                                                            worldMatrix.XNAMatrix, out localProjectedV);
                }
                else
                {
                    length = MathHelper.ToRadians(section.Angle);
                    radius = section.Radius; // meters

                    Vector3 left;
                    if (section.Angle > 0)
                        left = radius * Vector3.Cross(Vector3.Down, heading); // Vector from PC to O
                    else
                        left = radius * Vector3.Cross(Vector3.Up, heading); // Vector from PC to O
                    Matrix rot = Matrix.CreateRotationY(-MathHelper.ToRadians(section.Angle)); // Heading change (rotation about O)

                    displacement = InterpolateHelper.MSTSInterpolateAlongCurve(localV, left, rot,
                                            worldMatrix.XNAMatrix, out localProjectedV);

                    heading = Vector3.Transform(heading, rot); // Heading change
                    nextRoot = new WorldPosition(nextRoot.Tile, MatrixExtension.Multiply(MatrixExtension.Multiply(trackRot, rot), nextRoot.XNAMatrix)); // Store heading change

                }
                nextRoot = nextRoot.SetTranslation(sectionOrigin + displacement);
                root = root.SetTranslation(root.XNAMatrix.Translation + Vector3.Transform(trackLoc, worldMatrix.XNAMatrix));
                nextRoot = nextRoot.SetTranslation(nextRoot.XNAMatrix.Translation + Vector3.Transform(trackLoc, worldMatrix.XNAMatrix));
                trackList.Add(new WireViewer(viewer, root, nextRoot, radius, length));
                localV = localProjectedV; // Next subsection
            }
        }

        /// <summary>
        /// Decompose and add a wire on top of MSTS track section
        /// </summary>
        /// <param name="viewer">Viewer reference.</param>
        /// <param name="trackList">DynamicTrackViewer list.</param>
        /// <param name="trackObj">Dynamic track section to decompose.</param>
        /// <param name="worldMatrixInput">Position matrix.</param>
        public static void DecomposeDynamicWire(Viewer viewer, List<DynamicTrackViewer> trackList, DynamicTrackObject trackObj, in WorldPosition worldMatrixInput)
        {
            // DYNAMIC WIRE
            // ============
            // Objectives:
            // 1-Decompose multi-subsection DT into individual sections.  
            // 2-Create updated transformation objects (instances of WorldPosition) to reflect 
            //   root of next subsection.
            // 3-Distribute elevation change for total section through subsections. (ABANDONED)
            // 4-For each meaningful subsection of dtrack, build a separate WirePrimitive.
            //
            // Method: Iterate through each subsection, updating WorldPosition for the root of
            // each subsection.  The rotation component changes only in heading.  The translation 
            // component steps along the path to reflect the root of each subsection.

            // The following vectors represent local positioning relative to root of original (5-part) section:
            Vector3 localV = Vector3.Zero; // Local position (in x-z plane)
            Vector3 localProjectedV; // Local next position (in x-z plane)
            Vector3 displacement;  // Local displacement (from y=0 plane)
            Vector3 heading = Vector3.Forward; // Local heading (unit vector)

            WorldPosition nextRoot = worldMatrixInput; // Will become initial root
            Vector3 sectionOrigin = worldMatrixInput.XNAMatrix.Translation; // Save root position
            WorldPosition worldMatrix = worldMatrixInput.SetTranslation(Vector3.Zero); // worldMatrix now rotation-only

            // Iterate through all subsections
            for (int iTkSection = 0; iTkSection < trackObj.TrackSections.Count; iTkSection++)
            {
                if ((trackObj.TrackSections[iTkSection].Length == 0f && trackObj.TrackSections[iTkSection].Angle == 0f)
                    || trackObj.TrackSections[iTkSection].SectionIndex == -1)
                    continue; // Consider zero-length subsections vacuous

                // Create new DT object copy; has only one meaningful subsection
                DynamicTrackObject subsection = new DynamicTrackObject(trackObj, iTkSection);

                // Create a new WorldPosition for this subsection, initialized to nextRoot,
                // which is the WorldPosition for the end of the last subsection.
                // In other words, beginning of present subsection is end of previous subsection.
                WorldPosition root = nextRoot;

                // Now we need to compute the position of the end (nextRoot) of this subsection,
                // which will become root for the next subsection.

                // Clear nextRoot's translation vector so that nextRoot matrix contains rotation only
                nextRoot = nextRoot.SetTranslation(Vector3.Zero);

                // Straight or curved subsection?
                if (!subsection.TrackSections[0].Curved) // Straight section
                {   // Heading stays the same; translation changes in the direction oriented
                    // Rotate Vector3.Forward to orient the displacement vector
                    localProjectedV = localV + subsection.TrackSections[0].Length * heading;
                    displacement = InterpolateHelper.MSTSInterpolateAlongStraight(localV, heading, subsection.TrackSections[0].Length,
                                                            worldMatrix.XNAMatrix, out localProjectedV);
                }
                else // Curved section
                {   // Both heading and translation change 
                    // nextRoot is found by moving from Point-of-Curve (PC) to
                    // center (O)to Point-of-Tangent (PT).
                    Vector3 left = subsection.TrackSections[0].Radius * Vector3.Cross(Vector3.Up, heading) * Math.Sign(-subsection.TrackSections[0].Angle); // Vector from PC to O
                    Matrix rot = Matrix.CreateRotationY(-subsection.TrackSections[0].Angle); // Heading change (rotation about O)
                    // Shared method returns displacement from present world position and, by reference,
                    // local position in x-z plane of end of this section
                    displacement = InterpolateHelper.MSTSInterpolateAlongCurve(localV, left, rot,
                                            worldMatrix.XNAMatrix, out localProjectedV);

                    heading = Vector3.Transform(heading, rot); // Heading change
                    nextRoot = new WorldPosition(nextRoot.Tile, MatrixExtension.Multiply(rot, nextRoot.XNAMatrix)); // Store heading change
                }

                // Update nextRoot with new translation component
                nextRoot = nextRoot.SetTranslation(sectionOrigin + displacement);


                // Create a new WireViewer for the subsection
                trackList.Add(new WireViewer(viewer, root, nextRoot, subsection.TrackSections[0].Radius, subsection.TrackSections[0].Angle));
                localV = localProjectedV; // Next subsection
            }
        }
    }

    public class WireViewer : DynamicTrackViewer
    {
        public WireViewer(Viewer viewer, in WorldPosition position, in WorldPosition endPosition, float radius, float angle)
            : base(viewer, position, endPosition)
        {

            // Instantiate classes
            Primitive = new WirePrimitive(viewer, position, endPosition, radius, angle);
        }
    }

    public class LODWire : LOD
    {
        public LODWire(float cutOffRadius)
            : base(cutOffRadius)
        {
        }
    }

    public class LODItemWire : LODItem
    {
        // NumVertices and NumSegments used for sizing vertex and index buffers
        public uint VerticalNumVertices;                     // Total independent vertices in LOD
        public uint VerticalNumSegments;                     // Total line segment count in LOD
        public List<Polyline> VerticalPolylines { get; } = new List<Polyline>();  // Array of arrays of vertices 

        /// <summary>
        /// LODItemWire constructor (default &amp; XML)
        /// </summary>
        public LODItemWire(string name)
            : base(name)
        {
        }

        public void VerticalAccumulate(int count)
        {
            // Accumulates total independent vertices and total line segments
            // Used for sizing of vertex and index buffers
            VerticalNumVertices += (uint)count;
            VerticalNumSegments += (uint)count - 1;
        }
    }

    // Dynamic Wire profile class
    public class WireProfile : TrProfile
    {
        public float expectedSegmentLength;
        private float u1 = 0.25f, v1 = 0.25f;
        private float normalvalue = 0.707f;

        /// <summary>
        /// WireProfile constructor (default - builds from self-contained data)
        /// </summary>
        public WireProfile(Viewer viewer) // Nasty: void return type is not allowed. (See MSDN for compiler error CS0542.)
            : base(viewer, 0)//call the dummy base constructor so that no data is pre-populated
        {
            LODMethod = LODMethods.ComponentAdditive;
            LODWire lod; // Local LOD instance 
            LODItemWire lodItem; // Local LODItem instance
            Polyline pl; // Local polyline instance
            Polyline vertical;

            expectedSegmentLength = 40; //segment of wire is expected to be 40 meters

            lod = new LODWire(800.0f); // Create LOD for railsides with specified CutoffRadius
            lodItem = new LODItemWire("Wire");
            string overheadWire = Path.Combine(viewer.Simulator.RouteFolder.TexturesFolder, "overheadwire.ace");
            if (File.Exists(overheadWire))
            {
                lodItem.TexName = "overheadwire.ace";
            }
            else if (File.Exists(overheadWire = Path.Combine(viewer.Simulator.RouteFolder.ContentFolder.TexturesFolder, "overheadwire.ace")))
            {
                lodItem.TexName = overheadWire;
            }
            else
            {
                Trace.TraceInformation("Ignored missing overheadwire.ace, using default. You can copy the overheadwire.ace from OR\'s AddOns folder to {0}", viewer.Simulator.RouteFolder.TexturesFolder);
                lodItem.TexName = Path.Combine(viewer.Simulator.RouteFolder.ContentFolder.TexturesFolder, "dieselsmoke.ace");
            }
            lodItem.ShaderName = "TexDiff";
            lodItem.LightModelName = "DarkShade";
            lodItem.AlphaTestMode = 0;
            lodItem.TexAddrModeName = "Wrap";
            lodItem.ESD_Alternative_Texture = 0;
            lodItem.MipMapLevelOfDetailBias = 0;
            LODItem.LoadMaterial(viewer, lodItem);

            bool drawTriphaseWire = viewer.Simulator.RouteModel.RouteConditions.TriphaseEnabled;
            bool drawDoubleWire = viewer.Simulator.RouteModel.RouteConditions.DoubleWireEnabled || viewer.UserSettings.OverheadWireType >= FreeTrainSimulator.Common.OverheadWireType.DoubleWire;
            float topHeight = viewer.Simulator.RouteModel.RouteConditions.OverheadWireHeight;
            float topWireOffset = (viewer.Simulator.RouteModel.RouteConditions.DoubleWireHeight > 0 ?
                viewer.Simulator.RouteModel.RouteConditions.DoubleWireHeight : 1.0f);
            float dist = (viewer.Simulator.RouteModel.RouteConditions.TriphaseWidth > 0 ?
                viewer.Simulator.RouteModel.RouteConditions.TriphaseWidth : 1.0f);

            if (drawTriphaseWire)
            {
                pl = SingleWireProfile("TopWireLeft", topHeight, -dist / 2);
                lodItem.Polylines.Add(pl);
                lodItem.Accum(pl.Vertices.Count);

                pl = SingleWireProfile("TopWireRight", topHeight, dist / 2);
                lodItem.Polylines.Add(pl);
                lodItem.Accum(pl.Vertices.Count);
            }
            else
            {
                pl = SingleWireProfile("TopWire", topHeight);
                lodItem.Polylines.Add(pl);
                lodItem.Accum(pl.Vertices.Count);
            }

            if (drawDoubleWire)
            {
                topHeight += topWireOffset;

                if (drawTriphaseWire)
                {
                    pl = SingleWireProfile("TopWire1Left", topHeight, -dist / 2);
                    lodItem.Polylines.Add(pl);
                    lodItem.Accum(pl.Vertices.Count);
                    pl = SingleWireProfile("TopWire1Right", topHeight, dist / 2);
                    lodItem.Polylines.Add(pl);
                    lodItem.Accum(pl.Vertices.Count);

                    vertical = VerticalWireProfile("TopWireVerticalLeft", topHeight, -dist / 2);
                    lodItem.VerticalPolylines.Clear();
                    lodItem.VerticalPolylines.Add(vertical);
                    lodItem.VerticalAccumulate(vertical.Vertices.Count);
                    vertical = VerticalWireProfile("TopWireVerticalRight", topHeight, dist / 2);
                    lodItem.VerticalPolylines.Add(vertical);
                    lodItem.VerticalAccumulate(vertical.Vertices.Count);

                }
                else
                {
                    pl = SingleWireProfile("TopWire1", topHeight);
                    lodItem.Polylines.Add(pl);
                    lodItem.Accum(pl.Vertices.Count);

                    vertical = VerticalWireProfile("TopWireVertical", topHeight);
                    lodItem.VerticalPolylines.Clear();
                    lodItem.VerticalPolylines.Add(vertical);
                    lodItem.VerticalAccumulate(vertical.Vertices.Count);
                }
            }

            lod.LODItems.Add(lodItem); // Append to LODItems array 
            base.LODs.Add(lod); // Append this lod to LODs array
        }

        private Polyline SingleWireProfile(String name, float topHeight, float xOffset = 0)
        {
            Polyline pl;
            pl = new Polyline(this, name, 5);
            pl.DeltaTexCoord = new Vector2(0.00f, 0.00f);

            pl.Vertices.Add(new Vertex(-0.01f + xOffset, topHeight + 0.02f, 0.0f, -normalvalue, normalvalue, 0f, u1, v1));
            pl.Vertices.Add(new Vertex(0.01f + xOffset, topHeight + 0.02f, 0.0f, normalvalue, normalvalue, 0f, u1, v1));
            pl.Vertices.Add(new Vertex(0.01f + xOffset, topHeight, 0.0f, normalvalue, -normalvalue, 0f, u1, v1));
            pl.Vertices.Add(new Vertex(-0.01f + xOffset, topHeight, 0.0f, -normalvalue, -normalvalue, 0f, u1, v1));
            pl.Vertices.Add(new Vertex(-0.01f + xOffset, topHeight + 0.02f, 0.0f, -normalvalue, normalvalue, 0f, u1, v1));

            return pl;
        }

        private Polyline VerticalWireProfile(String name, float topHeight, float xOffset = 0)
        {
            Polyline pl;
            pl = new Polyline(this, name, 5);
            pl.DeltaTexCoord = new Vector2(0.00f, 0.00f);

            pl.Vertices.Add(new Vertex(-0.008f + xOffset, topHeight, 0.008f, -normalvalue, 0f, normalvalue, u1, v1));
            pl.Vertices.Add(new Vertex(-.008f + xOffset, topHeight, -.008f, normalvalue, 0f, normalvalue, u1, v1));
            pl.Vertices.Add(new Vertex(.008f + xOffset, topHeight, -.008f, normalvalue, 0f, -normalvalue, u1, v1));
            pl.Vertices.Add(new Vertex(.008f + xOffset, topHeight, .008f, -normalvalue, 0f, -normalvalue, u1, v1));
            pl.Vertices.Add(new Vertex(-.008f + xOffset, topHeight, .008f, -normalvalue, 0f, normalvalue, u1, v1));

            return pl;
        }

    }

    public class WirePrimitive : DynamicTrackPrimitive
    {
        private static WireProfile WireProfile;
        private float topWireOffset;

        public WirePrimitive(Viewer viewer, in WorldPosition worldPosition, in WorldPosition endPosition, float radius, float angle)
            : base()
        {
            // WirePrimitive is responsible for creating a mesh for a section with a single subsection.
            // It also must update worldPosition to reflect the end of this subsection, subsequently to
            // serve as the beginning of the next subsection.


            // The track cross section (profile) vertex coordinates are hard coded.
            // The coordinates listed here are those of default MSTS "A1t" track.
            // TODO: Read this stuff from a file. Provide the ability to use alternative profiles.

            // Initialize a scalar DtrackData object
            DTrackData = new DtrackData(radius >= 0, angle, Math.Max(0, radius));

            WireProfile ??= new WireProfile(viewer);
            TrProfile = WireProfile;

            topWireOffset = (viewer.Simulator.RouteModel.RouteConditions.DoubleWireHeight > 0 ?
                viewer.Simulator.RouteModel.RouteConditions.DoubleWireHeight : 1.0f);

            XNAEnd = endPosition.XNAMatrix.Translation;

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
                    ShapePrimitives[primIndex] = BuildPrimitive(viewer, iLOD, iLODItem);
                    primIndex++;
                }
                lod.PrimIndexStop = primIndex; // 1 above last index for this LOD
            }


            if (!DTrackData.IsCurved)
                ObjectRadius = 0.5f * DTrackData.Length; // half-length
            else
                ObjectRadius = DTrackData.Radius * (float)Math.Sin(0.5 * Math.Abs(DTrackData.Length)); // half chord length
        }

        /// <summary>
        /// Builds a Wire LOD to WireProfile specifications as one vertex buffer and one index buffer.
        /// The order in which the buffers are built reflects the nesting in the TrProfile.  The nesting order is:
        /// (Polylines (Vertices)).  All vertices and indices are built contiguously for an LOD.
        /// </summary>
        /// <param name="viewer">Viewer.</param>
        /// <param name="lodIndex">Index of LOD mesh to be generated from profile.</param>
        /// <param name="lodItemIndex">Index of LOD mesh to be generated from profile.</param>
        public ShapePrimitive BuildPrimitive(Viewer viewer, int lodIndex, int lodItemIndex)
        {
            // Call for track section to initialize itself
            if (!DTrackData.IsCurved)
                LinearGen();
            else
                CircArcGen();

            // Count vertices and indices
            LODWire lod = (LODWire)TrProfile.LODs[lodIndex];
            LODItemWire lodItem = (LODItemWire)lod.LODItems[lodItemIndex];
            NumVertices = (int)(lodItem.NumVertices * (NumSections + 1) + 2 * lodItem.VerticalNumVertices * NumSections);
            NumIndices = (short)(lodItem.NumSegments * NumSections * 6 + lodItem.VerticalNumSegments * NumSections * 6);
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
                        if (!DTrackData.IsCurved)
                            LinearGen(stride, pl); // Generation call
                        else
                            CircArcGen(stride, pl);

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

            if (lodItem.VerticalPolylines?.Count > 0)
            {

                // Now generate and load subsequent cross sections
                OldRadius = -center;
                float coveredLength = SegmentLength;

                for (uint i = 0; i < NumSections; i++)
                {
                    stride = 0;
                    radius = Vector3.Transform(OldRadius, sectionRotation);
                    Vector3 p;
                    // Initial load of baseline cross section polylines for this LOD only:
                    if (i == 0)
                    {
                        foreach (Polyline pl in lodItem.VerticalPolylines)
                        {
                            foreach (Vertex v in pl.Vertices)
                            {
                                VertexList[VertexIndex].Position = v.Position;
                                VertexList[VertexIndex].Normal = v.Normal;
                                VertexList[VertexIndex].TextureCoordinate = v.TexCoord;
                                VertexIndex++;
                                stride++;
                            }
                        }
                    }
                    else
                    {
                        foreach (Polyline pl in lodItem.VerticalPolylines)
                        {
                            foreach (Vertex v in pl.Vertices)
                            {
                                if (DTrackData.IsCurved)
                                {

                                    OldV = v.Position - center - OldRadius;
                                    // Rotate the point about local origin and reposition it (including elevation change)
                                    p = DDY + center + radius + v.Position;// +Vector3.Transform(OldV, sectionRotation);
                                    VertexList[VertexIndex].Position = new Vector3(p.X, p.Y, p.Z);

                                }
                                else
                                {
                                    VertexList[VertexIndex].Position = v.Position + new Vector3(0, 0, -coveredLength);
                                }

                                VertexList[VertexIndex].Normal = v.Normal;
                                VertexList[VertexIndex].TextureCoordinate = v.TexCoord;
                                VertexIndex++;
                                stride++;
                            }
                        }
                    }

                    foreach (Polyline pl in lodItem.VerticalPolylines)
                    {
                        uint plv = 0; // Polyline vertex index
                        foreach (Vertex v in pl.Vertices)
                        {
                            LinearVerticalGen(stride, pl); // Generation call

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

                    if (i != 0)
                    {
                        OldRadius = radius; // Get ready for next segment
                        coveredLength += SegmentLength;
                    }
                }
            }

            // Create and populate a new ShapePrimitive
            var indexBuffer = new IndexBuffer(viewer.Game.GraphicsDevice, IndexElementSize.SixteenBits, NumIndices, BufferUsage.WriteOnly);
            indexBuffer.SetData(TriangleListIndices);
            return new ShapePrimitive(viewer.Game.GraphicsDevice, lodItem.LODMaterial, new SharedShape.VertexBufferSet(VertexList, viewer.Game.GraphicsDevice), indexBuffer, 0, NumVertices, NumIndices / 3, new[] { -1 }, 0);
        }

        /// <summary>
        /// Initializes member variables for straight track sections.
        /// </summary>
        private void LinearGen()
        {
            NumSections = 1;

            // Cute the lines to have vertical stuff if needed
            if (WireProfile.expectedSegmentLength > 1)
            {
                NumSections = (int)(DTrackData.Length / WireProfile.expectedSegmentLength);
            }

            if (NumSections < 1)
                NumSections = 1;

            SegmentLength = DTrackData.Length / NumSections; // Length of each mesh segment (meters)
            DDY = new Vector3(); // new Vector3(0, DTrackData.DeltaElevation / NumSections, 0); // Incremental elevation change
        }

        /// <summary>
        /// Initializes member variables for circular arc track sections.
        /// </summary>
        private void CircArcGen()
        {
            float arcLength = Math.Abs(DTrackData.Radius * DTrackData.Length);
            // Define the number of track cross sections in addition to the base.
            // Assume one skewed straight section per degree of curvature
            // Define the number of track cross sections in addition to the base.
            if (WireProfile.expectedSegmentLength > 1)
            {
                if (arcLength > 2 * WireProfile.expectedSegmentLength)
                {
                    NumSections = (int)(arcLength / WireProfile.expectedSegmentLength);
                }
                else if (arcLength > WireProfile.expectedSegmentLength)
                {
                    NumSections = (int)(2 * arcLength / WireProfile.expectedSegmentLength);
                }
                else
                    NumSections = (int)Math.Abs(MathHelper.ToDegrees(DTrackData.Length / 4));
            }
            else
                NumSections = (int)Math.Abs(MathHelper.ToDegrees(DTrackData.Length / 3));

            if (NumSections < 1)
                NumSections = 1; // Very small radius track - zero avoidance
            //numSections = 10; //TESTING
            // TODO: Generalize count to profile file specification

            SegmentLength = DTrackData.Length / NumSections; // Length of each mesh segment (radians)
            DDY = new Vector3();// new Vector3(0, DTrackData.DeltaElevation / NumSections, 0); // Incremental elevation change

            // The approach here is to replicate the previous cross section, 
            // rotated into its position on the curve and vertically displaced if on grade.
            // The local center for the curve lies to the left or right of the local origin and ON THE BASE PLANE
            center = DTrackData.Radius * (DTrackData.Length < 0 ? Vector3.Left : Vector3.Right);
            sectionRotation = Matrix.CreateRotationY(-SegmentLength); // Rotation per iteration (constant)
        }

        /// <summary>
        /// Generates vertices for a vertical section (straight track).
        /// </summary>
        /// <param name="stride">Index increment between section-to-section vertices.</param>
        /// <param name="pl"></param>
        private void LinearVerticalGen(uint stride, Polyline pl)
        {
            Vector3 displacement = new Vector3(0, -topWireOffset, 0) + DDY;
            float wrapLength = displacement.Length();
            Vector2 uvDisplacement = pl.DeltaTexCoord * wrapLength;

            Vector3 p = VertexList[VertexIndex - stride].Position + displacement;
            Vector3 n = VertexList[VertexIndex - stride].Normal;
            Vector2 uv = VertexList[VertexIndex - stride].TextureCoordinate + uvDisplacement;

            VertexList[VertexIndex].Position = new Vector3(p.X, p.Y, p.Z);
            VertexList[VertexIndex].Normal = new Vector3(n.X, n.Y, n.Z);
            VertexList[VertexIndex].TextureCoordinate = new Vector2(uv.X, uv.Y);
        }

    }
}
