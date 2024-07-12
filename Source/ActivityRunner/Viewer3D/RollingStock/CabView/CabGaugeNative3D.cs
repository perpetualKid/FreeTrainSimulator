// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014, 2015 by the Open Rails project.
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

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Formats.Msts.Models;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    public class CabGaugeNative3D
    {
        private readonly PoseableShape trainCarShape;
        private readonly VertexPositionNormalTexture[] vertexList;
        private int numVertices;
        private readonly int numIndices;
        private readonly short[] triangleListIndices;// Array of indices to vertices for triangles
        private Matrix xnaMatrix;
        private readonly Viewer viewer;
        private readonly MutableShapePrimitive shapePrimitive;
        private Material positiveMaterial;
        private Material negativeMaterial;
        private readonly float width;
        private readonly float maxLen;
        private readonly int direction;
        private readonly int orientation;

        public CabViewGaugeRenderer GaugeRenderer { get; }

        public CabGaugeNative3D(Viewer viewer, int iMatrix, string size, string len, PoseableShape trainCarShape, CabViewControlRenderer c)
        {
            if (float.TryParse(size, out width))
                width /= 1000f; //in mm
            if (float.TryParse(len, out maxLen))
                maxLen /= 1000f; //in mm

            GaugeRenderer = (CabViewGaugeRenderer)c;
            direction = GaugeRenderer.GetGauge().Direction;
            orientation = GaugeRenderer.GetGauge().Orientation;

            this.viewer = viewer;
            this.trainCarShape = trainCarShape;
            xnaMatrix = this.trainCarShape.SharedShape.Matrices[iMatrix];
            CabViewGaugeControl gauge = GaugeRenderer.GetGauge();
            var maxVertex = 4;// a rectangle
                              //Material = viewer.MaterialManager.Load("Scenery", Helpers.GetRouteTextureFile(viewer.Simulator, Helpers.TextureFlags.None, texture), (int)(SceneryMaterialOptions.None | SceneryMaterialOptions.AlphaBlendingBlend), 0);

            // Create and populate a new ShapePrimitive
            numVertices = numIndices = 0;
            var Size = gauge.Bounds.Width;

            vertexList = new VertexPositionNormalTexture[maxVertex];
            triangleListIndices = new short[maxVertex / 2 * 3]; // as is NumIndices

            var tX = 1f;
            var tY = 1f;

            //the left-bottom vertex
            Vertex v1 = new Vertex(0f, 0f, 0.002f, 0, 0, -1, tX, tY);

            //the right-bottom vertex
            Vertex v2 = new Vertex(0f, Size, 0.002f, 0, 0, -1, tX, tY);

            Vertex v3 = new Vertex(Size, 0, 0.002f, 0, 0, -1, tX, tY);

            Vertex v4 = new Vertex(Size, Size, 0.002f, 0, 0, -1, tX, tY);

            //create first triangle
            triangleListIndices[numIndices++] = (short)numVertices;
            triangleListIndices[numIndices++] = (short)(numVertices + 1);
            triangleListIndices[numIndices++] = (short)(numVertices + 2);
            // Second triangle:
            triangleListIndices[numIndices++] = (short)numVertices;
            triangleListIndices[numIndices++] = (short)(numVertices + 2);
            triangleListIndices[numIndices++] = (short)(numVertices + 3);

            //create vertex
            vertexList[numVertices].Position = v1.Position;
            vertexList[numVertices].Normal = v1.Normal;
            vertexList[numVertices].TextureCoordinate = v1.TexCoord;
            vertexList[numVertices + 1].Position = v2.Position;
            vertexList[numVertices + 1].Normal = v2.Normal;
            vertexList[numVertices + 1].TextureCoordinate = v2.TexCoord;
            vertexList[numVertices + 2].Position = v3.Position;
            vertexList[numVertices + 2].Normal = v3.Normal;
            vertexList[numVertices + 2].TextureCoordinate = v3.TexCoord;
            vertexList[numVertices + 3].Position = v4.Position;
            vertexList[numVertices + 3].Normal = v4.Normal;
            vertexList[numVertices + 3].TextureCoordinate = v4.TexCoord;
            numVertices += 4;

            //create the shape primitive
            var material = FindMaterial();
            shapePrimitive = new MutableShapePrimitive(viewer.Game.GraphicsDevice, FindMaterial(), numVertices, numIndices, new[] { -1 }, 0);
            UpdateShapePrimitive(material);

        }

        private Material FindMaterial()
        {
            bool Positive;
            Color c = GaugeRenderer.GetColor(out Positive);
            if (Positive)
            {
                if (positiveMaterial == null)
                {
                    positiveMaterial = new SolidColorMaterial(viewer, c.A, c.R, c.G, c.B);
                }
                return positiveMaterial;
            }
            else
            {
                if (negativeMaterial == null)
                    negativeMaterial = new SolidColorMaterial(viewer, c.A, c.R, c.G, c.B);
                return negativeMaterial;
            }
        }

        //update the digits with current speed or time
        public void UpdateDigit()
        {
            numVertices = 0;

            Material UsedMaterial = FindMaterial();

            float length = GaugeRenderer.GetRangeFraction(true);

            CabViewGaugeControl gauge = GaugeRenderer.GetGauge();

            var len = maxLen * length;
            var absLen = Math.Abs(len);
            Vertex v1, v2, v3, v4;

            //the left-bottom vertex if ori=0;dir=0, right-bottom if ori=0,dir=1; left-top if ori=1,dir=0; left-bottom if ori=1,dir=1;
            v1 = new Vertex(0f, 0f, 0.002f, 0, 0, -1, 0f, 0f);

            if (orientation == 0)
            {
                if (direction == 0 ^ len < 0)//moving right
                {
                    //other vertices
                    v2 = new Vertex(0f, width, 0.002f, 0, 0, 1, 0f, 0f);
                    v3 = new Vertex(absLen, width, 0.002f, 0, 0, 1, 0f, 0f);
                    v4 = new Vertex(absLen, 0f, 0.002f, 0, 0, 1, 0f, 0f);
                }
                else //moving left
                {
                    v4 = new Vertex(0f, width, 0.002f, 0, 0, 1, 0f, 0f);
                    v3 = new Vertex(-absLen, width, 0.002f, 0, 0, 1, 0f, 0f);
                    v2 = new Vertex(-absLen, 0f, 0.002f, 0, 0, 1, 0f, 0f);
                }
            }
            else
            {
                if (direction == 1 ^ len < 0)//up
                {
                    //other vertices
                    v2 = new Vertex(0f, absLen, 0.002f, 0, 0, 1, 0f, 0f);
                    v3 = new Vertex(width, absLen, 0.002f, 0, 0, 1, 0f, 0f);
                    v4 = new Vertex(width, 0f, 0.002f, 0, 0, 1, 0f, 0f);
                }
                else //moving down
                {
                    v4 = new Vertex(0f, -absLen, 0.002f, 0, 0, 1, 0f, 0f);
                    v3 = new Vertex(width, -absLen, 0.002f, 0, 0, 1, 0f, 0f);
                    v2 = new Vertex(width, 0, 0.002f, 0, 0, 1, 0f, 0f);
                }
            }

            //create vertex list
            vertexList[numVertices].Position = v1.Position;
            vertexList[numVertices].Normal = v1.Normal;
            vertexList[numVertices].TextureCoordinate = v1.TexCoord;
            vertexList[numVertices + 1].Position = v2.Position;
            vertexList[numVertices + 1].Normal = v2.Normal;
            vertexList[numVertices + 1].TextureCoordinate = v2.TexCoord;
            vertexList[numVertices + 2].Position = v3.Position;
            vertexList[numVertices + 2].Normal = v3.Normal;
            vertexList[numVertices + 2].TextureCoordinate = v3.TexCoord;
            vertexList[numVertices + 3].Position = v4.Position;
            vertexList[numVertices + 3].Normal = v4.Normal;
            vertexList[numVertices + 3].TextureCoordinate = v4.TexCoord;
            numVertices += 4;

            //update the shape primitive
            UpdateShapePrimitive(UsedMaterial);

        }

        private void UpdateShapePrimitive(Material material)
        {
            var indexData = new short[numIndices];
            Array.Copy(triangleListIndices, indexData, numIndices);
            shapePrimitive.SetIndexData(indexData);

            var vertexData = new VertexPositionNormalTexture[numVertices];
            Array.Copy(vertexList, vertexData, numVertices);
            shapePrimitive.SetVertexData(vertexData, 0, numVertices, numIndices / 3);

            shapePrimitive.SetMaterial(material);
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (!GaugeRenderer.IsPowered && GaugeRenderer.control.HideIfDisabled)
                return;

            UpdateDigit();
            Matrix mx = MatrixExtension.ChangeTranslation(trainCarShape.WorldPosition.XNAMatrix, (trainCarShape.WorldPosition.Tile - viewer.Camera.Tile).TileVector().XnaVector());
            MatrixExtension.Multiply(xnaMatrix, mx, out Matrix m);

            // TODO: Make this use AddAutoPrimitive instead.
            frame.AddPrimitive(shapePrimitive.Material, shapePrimitive, RenderPrimitiveGroup.Interior, ref m, ShapeFlags.None);
        }

        internal void Mark()
        {
            shapePrimitive.Mark();
        }
    } // class ThreeDimCabDigit
}
