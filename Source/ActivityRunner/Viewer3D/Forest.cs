// COPYRIGHT 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
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

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Common;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Common;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;

namespace Orts.ActivityRunner.Viewer3D
{
    public class ForestViewer
    {
        private readonly Viewer viewer;
        private readonly WorldPosition position;
        private readonly Material material;
        private readonly ForestPrimitive primitive;

        private readonly float maximumCenterlineOffset;
        private readonly bool checkRoads;

        public ForestViewer(Viewer viewer, ForestObject forest, in WorldPosition position)
        {
            this.viewer = viewer;
            this.position = position;
            maximumCenterlineOffset = this.viewer.Simulator.Route.ForestClearDistance;
            checkRoads = this.viewer.Simulator.Route.RemoveForestTreesFromRoads;

            material = viewer.MaterialManager.Load("Forest", Helpers.GetForestTextureFile(forest.TreeTexture));
            primitive = new ForestPrimitive(this.viewer, forest, position, maximumCenterlineOffset, checkRoads);
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (primitive.primitiveCount > 0)
            {
                Tile delta = position.Tile - viewer.Camera.Tile;
                Vector3 mstsLocation = position.Location + delta.TileVector();
                Matrix xnaMatrix = Matrix.CreateTranslation(mstsLocation.X, mstsLocation.Y, -mstsLocation.Z);
                frame.AddAutoPrimitive(mstsLocation, primitive.ObjectRadius, float.MaxValue, material, primitive, RenderPrimitiveGroup.World, ref xnaMatrix, viewer.Settings.ShadowAllShapes ? ShapeFlags.ShadowCaster : ShapeFlags.None);
            }
        }

        internal void Mark()
        {
            material.Mark();
        }
    }

    public class ForestPrimitive : RenderPrimitive
    {
        private readonly Viewer viewer;
        private readonly VertexBuffer vertexBuffer;
        public readonly int primitiveCount;
        private readonly float maximumCenterlineOffset;
        private readonly bool checkRoads;

        public float ObjectRadius { get; }

        public ForestPrimitive(Viewer viewer, ForestObject forest, in WorldPosition position, float maximumCenterlineOffset, bool checkRoadsToo)
        {
            this.viewer = viewer;
            this.maximumCenterlineOffset = maximumCenterlineOffset;
            checkRoads = checkRoadsToo;

            var trees = CalculateTrees(viewer.Tiles, forest, position, out float radius);
            ObjectRadius = radius;

            if (trees.Count > 0)
            {
                vertexBuffer = new VertexBuffer(viewer.Game.GraphicsDevice, typeof(VertexPositionNormalTexture), trees.Count, BufferUsage.WriteOnly);
                vertexBuffer.SetData(trees.ToArray());
            }

            primitiveCount = trees.Count / 3;
        }

        private List<VertexPositionNormalTexture> CalculateTrees(TileManager tiles, ForestObject forest, in WorldPosition position, out float objectRadius)
        {
            // To get consistent tree placement between sessions, derive the seed from the location.
            Random random = new Random((int)(1000.0 * (position.Location.X + position.Location.Z + position.Location.Y)));
            List<TrackVectorSection> sections = new List<TrackVectorSection>();
            objectRadius = (float)Math.Sqrt(forest.ForestArea.Width * forest.ForestArea.Width + forest.ForestArea.Height * forest.ForestArea.Height) / 2;

            if (maximumCenterlineOffset > 0)
            {
                Matrix InvForestXNAMatrix = Matrix.Invert(position.XNAMatrix);
                var addList = FindTracksAndRoadsClose(position.Tile);
                FindTracksAndRoadsMoreClose(sections, addList, forest, position, InvForestXNAMatrix);

                System.Collections.Specialized.BitVector32 includedTiles = new System.Collections.Specialized.BitVector32();

                void UpdateIncludedTiles(in Vector3 vertex)
                {
                    if (vertex.X > 1024)
                        includedTiles[0] = true;
                    if (vertex.X < -1024)
                        includedTiles[1] = true;
                    if (vertex.Z > 1024)
                        includedTiles[3] = true;
                    if (vertex.Z < -1024)
                        includedTiles[2] = true;
                }

                // Check for cross-tile forests
                VectorExtension.Transform(new Vector3(-forest.ForestArea.Width / 2, 0, -forest.ForestArea.Height / 2), position.XNAMatrix, out Vector3 forestVertex);
                UpdateIncludedTiles(forestVertex);
                VectorExtension.Transform(new Vector3(forest.ForestArea.Width / 2, 0, -forest.ForestArea.Height / 2), position.XNAMatrix, out forestVertex);
                UpdateIncludedTiles(forestVertex);
                VectorExtension.Transform(new Vector3(-forest.ForestArea.Width / 2, 0, forest.ForestArea.Height / 2), position.XNAMatrix, out forestVertex);
                UpdateIncludedTiles(forestVertex);
                VectorExtension.Transform(new Vector3(forest.ForestArea.Width / 2, 0, forest.ForestArea.Height / 2), position.XNAMatrix, out forestVertex);
                UpdateIncludedTiles(forestVertex);

                // add sections in nearby tiles for cross-tile forests
                if (includedTiles[0])
                {
                    addList = FindTracksAndRoadsClose(position.Tile.East);
                    FindTracksAndRoadsMoreClose(sections, addList, forest, position, InvForestXNAMatrix);
                }
                if (includedTiles[1])
                {
                    addList = FindTracksAndRoadsClose(position.Tile.West);
                    FindTracksAndRoadsMoreClose(sections, addList, forest, position, InvForestXNAMatrix);
                }
                if (includedTiles[2])
                {
                    addList = FindTracksAndRoadsClose(position.Tile.North);
                    FindTracksAndRoadsMoreClose(sections, addList, forest, position, InvForestXNAMatrix);
                }
                if (includedTiles[3])
                {
                    addList = FindTracksAndRoadsClose(position.Tile.South);
                    FindTracksAndRoadsMoreClose(sections, addList, forest, position, InvForestXNAMatrix);
                }
                if (includedTiles[0] && includedTiles[2])
                {
                    addList = FindTracksAndRoadsClose(position.Tile.NorthEast);
                    FindTracksAndRoadsMoreClose(sections, addList, forest, position, InvForestXNAMatrix);
                }
                if (includedTiles[0] && includedTiles[3])
                {
                    addList = FindTracksAndRoadsClose(position.Tile.SouthEast);
                    FindTracksAndRoadsMoreClose(sections, addList, forest, position, InvForestXNAMatrix);
                }
                if (includedTiles[1] && includedTiles[2])
                {
                    addList = FindTracksAndRoadsClose(position.Tile.NorthWest);
                    FindTracksAndRoadsMoreClose(sections, addList, forest, position, InvForestXNAMatrix);
                }
                if (includedTiles[1] && includedTiles[3])
                {
                    addList = FindTracksAndRoadsClose(position.Tile.SouthWest);
                    FindTracksAndRoadsMoreClose(sections, addList, forest, position, InvForestXNAMatrix);
                }
            }

            var trees = new List<VertexPositionNormalTexture>(forest.Population * 6);
            for (var i = 0; i < forest.Population; i++)
            {
                VectorExtension.Transform(
#pragma warning disable CA5394 // Do not use insecure randomness
                    new Vector3((0.5f - (float)random.NextDouble()) * forest.ForestArea.Width, 0, (0.5f - (float)random.NextDouble()) * forest.ForestArea.Height),
                    position.XNAMatrix, out Vector3 xnaTreePosition);

                bool onTrack = false;
                var scale = MathHelper.Lerp(forest.ScaleRange.LowerLimit, forest.ScaleRange.UpperLimit, (float)random.NextDouble());
#pragma warning restore CA5394 // Do not use insecure randomness
                var treeSize = new Vector3(forest.TreeSize.Width * scale, forest.TreeSize.Height * scale, 1);
                var heightComputed = false;
                if (maximumCenterlineOffset > 0 && sections != null && sections.Count > 0)
                {
                    foreach (var section in sections)
                    {
                        onTrack = InitTrackSection(section, xnaTreePosition, position.Tile, treeSize.X / 2);
                        if (onTrack)
                        {
                            try
                            {
                                var trackShape = RuntimeData.Instance.TSectionDat.TrackShapes[section.ShapeIndex];
                                if (trackShape != null && trackShape.TunnelShape)
                                {
                                    xnaTreePosition.Y = tiles.LoadAndGetElevation(position.Tile, xnaTreePosition.X, -xnaTreePosition.Z, false);
                                    heightComputed = true;
                                    if (xnaTreePosition.Y > section.Location.Location.Y + 10)
                                    {
                                        onTrack = false;
                                        continue;
                                    }
                                }
                            }
                            catch
                            {

                            }
                            break;
                        }
                    }
                }
                if (!onTrack)
                {
                    if (!heightComputed)
                        xnaTreePosition.Y = tiles.LoadAndGetElevation(position.Tile, xnaTreePosition.X, -xnaTreePosition.Z, false);
                    xnaTreePosition -= position.XNAMatrix.Translation;

                    trees.Add(new VertexPositionNormalTexture(xnaTreePosition, treeSize, new Vector2(1, 1)));
                    trees.Add(new VertexPositionNormalTexture(xnaTreePosition, treeSize, new Vector2(0, 0)));
                    trees.Add(new VertexPositionNormalTexture(xnaTreePosition, treeSize, new Vector2(1, 0)));
                    trees.Add(new VertexPositionNormalTexture(xnaTreePosition, treeSize, new Vector2(1, 1)));
                    trees.Add(new VertexPositionNormalTexture(xnaTreePosition, treeSize, new Vector2(0, 1)));
                    trees.Add(new VertexPositionNormalTexture(xnaTreePosition, treeSize, new Vector2(0, 0)));
                }
            }
            return trees;
        }

        //map sections to W tiles
        private static Dictionary<string, List<TrackVectorSection>> SectionMap;

        public List<TrackVectorSection> FindTracksAndRoadsClose(in Tile tile)
        {
            if (SectionMap == null)
            {
                SectionMap = new Dictionary<string, List<TrackVectorSection>>();
                if (maximumCenterlineOffset > 0)
                {
                    foreach (TrackVectorNode trackVectorNode in RuntimeData.Instance.TrackDB.TrackNodes.VectorNodes)
                    {
                        foreach (TrackVectorSection section in trackVectorNode.TrackVectorSections)
                        {
                            string key = section.Location.Tile.ToString();
                            if (!SectionMap.ContainsKey(key))
                                SectionMap.Add(key, new List<TrackVectorSection>());
                            SectionMap[key].Add(section);
                        }
                    }
                }
                if (checkRoads)
                {
                    if (RuntimeData.Instance.RoadTrackDB?.TrackNodes != null)
                    {
                        foreach (var node in RuntimeData.Instance.RoadTrackDB.TrackNodes)
                        {
                            if (!(node is TrackVectorNode trackVectorNode))
                                continue;
                            foreach (var section in trackVectorNode.TrackVectorSections)
                            {
                                string key = section.Location.Tile.ToString();
                                if (!SectionMap.ContainsKey(key))
                                    SectionMap.Add(key, new List<TrackVectorSection>());
                                SectionMap[key].Add(section);
                            }
                        }
                    }
                }
            }

            string targetKey = tile.ToString();
            return SectionMap.TryGetValue(targetKey, out List<TrackVectorSection> value) ? value : null;
        }

        private TrackSection trackSection;

        private bool InitTrackSection(TrackVectorSection section, Vector3 xnaTreePosition, in Tile tile, float treeWidth)
        {
            trackSection = RuntimeData.Instance.TSectionDat.TrackSections.TryGet(section.SectionIndex);
            if (trackSection == null)
                return false;
            if (trackSection.Curved)
            {
                return InitTrackSectionCurved(tile, xnaTreePosition.X, -xnaTreePosition.Z, section, treeWidth);
            }
            return InitTrackSectionStraight(tile, xnaTreePosition.X, -xnaTreePosition.Z, section, treeWidth);
        }

        // don't consider track sections outside the forest boundaries
        public void FindTracksAndRoadsMoreClose(List<TrackVectorSection> sections, List<TrackVectorSection> allSections, ForestObject forest, in WorldPosition position, Matrix invForestXNAMatrix)
        {
            if (allSections != null && allSections.Count > 0)
            {
                var toAddX = maximumCenterlineOffset + forest.ForestArea.Width / 2 + forest.ScaleRange.UpperLimit * forest.TreeSize.Width;
                var toAddZ = maximumCenterlineOffset + forest.ForestArea.Height / 2 + forest.ScaleRange.UpperLimit * forest.TreeSize.Width;
                foreach (TrackVectorSection section in allSections)
                {
                    Vector3 sectPosition = section.Location.Location + (section.Location.Tile - position.Tile).TileVector();
                    sectPosition.Z *= -1;
                    Vector3 sectPosToForest;

                    sectPosToForest = Vector3.Transform(sectPosition, invForestXNAMatrix);
                    sectPosToForest.Z *= -1;
                    trackSection = RuntimeData.Instance.TSectionDat.TrackSections.TryGet(section.SectionIndex);
                    if (trackSection == null)
                        continue;
                    if (Math.Abs(sectPosToForest.X) > trackSection.Length + toAddX)
                        continue;
                    if (Math.Abs(sectPosToForest.Z) > trackSection.Length + toAddZ)
                        continue;
                    sections.Add(section);
                }
            }
            return;
        }

        private const float InitErrorMargin = 0.5f;

        private bool InitTrackSectionCurved(in Tile tile, float x, float z, TrackVectorSection trackVectorSection, float treeWidth)
        {
            Vector3 delta = (tile - trackVectorSection.Location.Tile).TileVector();
            // We're working relative to the track section, so offset as needed.
            x += delta.X;
            z += delta.Z;

            var sx = trackVectorSection.Location.Location.X;
            var sz = trackVectorSection.Location.Location.Z;

            // Do a preliminary cull based on a bounding square around the track section.
            // Bounding distance is (radius * angle + error) by (radius * angle + error) around starting coordinates but no more than 2 for angle.
            var boundingDistance = trackSection.Radius * Math.Min(Math.Abs(MathHelper.ToRadians(trackSection.Angle)), 2) + maximumCenterlineOffset + treeWidth;
            var dx = Math.Abs(x - sx);
            var dz = Math.Abs(z - sz);
            if (dx > boundingDistance || dz > boundingDistance)
                return false;

            // To simplify the math, center around the start of the track section, rotate such that the track section starts out pointing north (+z) and flip so the track curves to the right.
            x -= sx;
            z -= sz;
            var rotated = EarthCoordinates.Rotate2D(trackVectorSection.Direction.Y, x, z);
            if (trackSection.Angle < 0)
                rotated.x *= -1;

            // Compute distance to curve's center at (radius,0) then adjust to get distance from centerline.
            dx = (float)rotated.x - trackSection.Radius;
            var lat = Math.Sqrt(dx * dx + rotated.z * rotated.z) - trackSection.Radius;
            if (Math.Abs(lat) > maximumCenterlineOffset + treeWidth)
                return false;

            // Compute distance along curve (ensure we are in the top right quadrant, otherwise our math goes wrong).
            if (rotated.z < -InitErrorMargin || rotated.x > trackSection.Radius + InitErrorMargin || rotated.z > trackSection.Radius + InitErrorMargin)
                return false;
            var radiansAlongCurve = (float)Math.Asin(rotated.z / trackSection.Radius);
            var lon = radiansAlongCurve * trackSection.Radius;
            if (lon < -InitErrorMargin || lon > trackSection.Length + InitErrorMargin)
                return false;

            return true;
        }

        private bool InitTrackSectionStraight(in Tile tile, float x, float z, TrackVectorSection trackVectorSection, float treeWidth)
        {
            Vector3 delta = (tile - trackVectorSection.Location.Tile).TileVector();
            // We're working relative to the track section, so offset as needed.
            x += delta.X;
            z += delta.Z;

            var sx = trackVectorSection.Location.Location.X;
            var sz = trackVectorSection.Location.Location.Z;

            // Do a preliminary cull based on a bounding square around the track section.
            // Bounding distance is (length + error) by (length + error) around starting coordinates.
            var boundingDistance = trackSection.Length + maximumCenterlineOffset + treeWidth;
            var dx = Math.Abs(x - sx);
            var dz = Math.Abs(z - sz);
            if (dx > boundingDistance || dz > boundingDistance)
                return false;

            // Calculate distance along and away from the track centerline.
            float lat, lon;
            (lat, lon) = EarthCoordinates.Survey(sx, sz, trackVectorSection.Direction.Y, x, z);
            if (Math.Abs(lat) > maximumCenterlineOffset + treeWidth)
                return false;
            if (lon < -InitErrorMargin || lon > trackSection.Length + InitErrorMargin)
                return false;

            return true;
        }

        public override void Draw()
        {
            graphicsDevice.SetVertexBuffer(vertexBuffer);
            graphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, primitiveCount);
        }
    }

    public class ForestMaterial : Material
    {
        private readonly Texture2D treeTexture;
        private readonly SceneryShader shader;
        private readonly int techniqueIndex;

        public ForestMaterial(Viewer viewer, string treeTexturePath)
            : base(viewer, treeTexturePath)
        {
            treeTexture = base.viewer.TextureManager.Get(treeTexturePath, true);
            shader = base.viewer.MaterialManager.SceneryShader;
            for (int i = 0; i < shader.Techniques.Count; i++)
            {
                if (shader.Techniques[i].Name == "Forest")
                {
                    techniqueIndex = i;
                    break;
                }
            }

        }

        public override void SetState(Material previousMaterial)
        {
            shader.CurrentTechnique = shader.Techniques[techniqueIndex]; //["Forest"];

            shader.ImageTexture = treeTexture;
            shader.ReferenceAlpha = 200;

            // Enable alpha blending for everything: this allows distance scenery to appear smoothly.

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
        }

        public override void Render(List<RenderItem> renderItems, ref Matrix view, ref Matrix projection, ref Matrix viewProjection)
        {
            shader.SetViewMatrix(ref view);
            foreach (var pass in shader.CurrentTechnique.Passes)
            {
                for (int i = 0; i < renderItems.Count; i++)
                {
                    RenderItem item = renderItems[i];
                    shader.SetMatrix(in item.XNAMatrix, in viewProjection);
                    shader.ZBias = item.RenderPrimitive.ZBias;
                    pass.Apply();
                    // SamplerStates can only be set after the ShaderPasses.Current.Apply().
                    graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;

                    item.RenderPrimitive.Draw();
                }
            }
        }

        public override void ResetState()
        {
            viewer.MaterialManager.SceneryShader.ReferenceAlpha = 0;

            graphicsDevice.BlendState = BlendState.Opaque;
        }

        public override Texture2D GetShadowTexture()
        {
            return treeTexture;
        }

        public override void Mark()
        {
            viewer.TextureManager.Mark(treeTexture);
            base.Mark();
        }
    }
}
