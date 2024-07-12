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
using System.Diagnostics;
using System.IO;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Common;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D.RollingStock.CabView
{
    public class CabDigit3D
    {
        private readonly int maxDigits = 6;
        private readonly PoseableShape trainCarShape;
        private readonly VertexPositionNormalTexture[] vertexList;
        private int numVertices;
        private int numIndices;
        private readonly short[] triangleListIndices;// Array of indices to vertices for triangles
        private Matrix xnaMatrix;
        private readonly Viewer viewer;
        private readonly MutableShapePrimitive shapePrimitive;
        public CabViewDigitalRenderer GaugeRenderer { get; }
        private readonly Material material;
        private Material alertMaterial;
        private readonly float size;
        private readonly string aceFile;

        public CabDigit3D(Viewer viewer, int iMatrix, string size, string aceFile, PoseableShape trainCarShape, CabViewControlRenderer c, MSTSLocomotive locomotive)
        {

            this.size = int.Parse(size) * 0.001f;//input size is in mm
            if (!string.IsNullOrEmpty(aceFile))
            {
                if (".ace".Equals(Path.GetExtension(aceFile), StringComparison.OrdinalIgnoreCase))
                    aceFile = Path.ChangeExtension(aceFile, ".ace");
                this.aceFile = aceFile.ToUpperInvariant();
            }
            else
            { this.aceFile = ""; }

            GaugeRenderer = (CabViewDigitalRenderer)c;
            if (GaugeRenderer.control is CabViewDigitalClockControl digital && digital.ControlType.CabViewControlType == CabViewControlType.Clock && digital.Accuracy > 0)
                maxDigits = 8;

            this.viewer = viewer;
            this.trainCarShape = trainCarShape;
            xnaMatrix = this.trainCarShape.SharedShape.Matrices[iMatrix];
            var maxVertex = (maxDigits + 2) * 4;// every face has max 8 digits, each has 2 triangles
                                                //Material = viewer.MaterialManager.Load("Scenery", Helpers.GetRouteTextureFile(viewer.Simulator, Helpers.TextureFlags.None, texture), (int)(SceneryMaterialOptions.None | SceneryMaterialOptions.AlphaBlendingBlend), 0);
            material = FindMaterial(false);//determine normal material
                                           // Create and populate a new ShapePrimitive
            numVertices = numIndices = 0;

            vertexList = new VertexPositionNormalTexture[maxVertex];
            triangleListIndices = new short[maxVertex / 2 * 3]; // as is NumIndices

            //start position is the center of the text
            var start = new Vector3(0, 0, 0);
            var rotation = locomotive.UsingRearCab ? (float)Math.PI : 0;

            //find the left-most of text
            Vector3 offset;

            offset.X = 0;

            offset.Y = -this.size;

            var speed = new string('0', maxDigits);
            foreach (char ch in speed)
            {
                var tX = GetTextureCoordX(ch);
                var tY = GetTextureCoordY(ch);
                var rot = Matrix.CreateRotationY(-rotation);

                //the left-bottom vertex
                Vector3 v = new Vector3(offset.X, offset.Y, 0.01f);
                v = Vector3.Transform(v, rot);
                v += start;
                Vertex v1 = new Vertex(v.X, v.Y, v.Z, 0, 0, -1, tX, tY);

                //the right-bottom vertex
                v.X = offset.X + this.size;
                v.Y = offset.Y;
                v.Z = 0.01f;
                v = Vector3.Transform(v, rot);
                v += start;
                Vertex v2 = new Vertex(v.X, v.Y, v.Z, 0, 0, -1, tX + 0.25f, tY);

                //the right-top vertex
                v.X = offset.X + this.size;
                v.Y = offset.Y + this.size;
                v.Z = 0.01f;
                v = Vector3.Transform(v, rot);
                v += start;
                Vertex v3 = new Vertex(v.X, v.Y, v.Z, 0, 0, -1, tX + 0.25f, tY - 0.25f);

                //the left-top vertex
                v.X = offset.X;
                v.Y = offset.Y + this.size;
                v.Z = 0.01f;
                v = Vector3.Transform(v, rot);
                v += start;
                Vertex v4 = new Vertex(v.X, v.Y, v.Z, 0, 0, -1, tX, tY - 0.25f);

                //create first triangle
                triangleListIndices[numIndices++] = (short)numVertices;
                triangleListIndices[numIndices++] = (short)(numVertices + 2);
                triangleListIndices[numIndices++] = (short)(numVertices + 1);
                // Second triangle:
                triangleListIndices[numIndices++] = (short)numVertices;
                triangleListIndices[numIndices++] = (short)(numVertices + 3);
                triangleListIndices[numIndices++] = (short)(numVertices + 2);

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
                offset.X += this.size * 0.8f;
                offset.Y += 0; //move to next digit
            }

            //create the shape primitive
            shapePrimitive = new MutableShapePrimitive(viewer.Game.GraphicsDevice, material, numVertices, numIndices, new[] { -1 }, 0);
            UpdateShapePrimitive(material);

        }

        private Material FindMaterial(bool Alert)
        {
            string globalText = viewer.Simulator.RouteFolder.ContentFolder.TexturesFolder;
            CabViewControlType controltype = GaugeRenderer.GetControlType();
            Material material = null;

            string imageName;
            if (Alert)
            {
                imageName = "alert.ace";
            }
            else if (!string.IsNullOrEmpty(aceFile))
            {
                imageName = aceFile;
            }
            else
            {
                switch (controltype)
                {
                    case CabViewControlType.Clock:
                        imageName = "clock.ace";
                        break;
                    case CabViewControlType.SpeedLimit:
                    case CabViewControlType.SpeedLim_Display:
                        imageName = "speedlim.ace";
                        break;
                    case CabViewControlType.Speed_Projected:
                    case CabViewControlType.Speedometer:
                    default:
                        imageName = "speed.ace";
                        break;
                }
            }

            SceneryMaterialOptions options = SceneryMaterialOptions.ShaderFullBright | SceneryMaterialOptions.AlphaBlendingAdd | SceneryMaterialOptions.UndergroundTexture;

            if (string.IsNullOrEmpty(trainCarShape.SharedShape.ReferencePath))
            {
                if (!File.Exists(Path.Combine(globalText, imageName)))
                {
                    Trace.TraceInformation($"Ignored missing {imageName} using default. You can copy the {imageName} from OR\'s AddOns folder to {globalText}, or place it under {trainCarShape.SharedShape.ReferencePath}");
                }
                material = viewer.MaterialManager.Load("Scenery", Helpers.GetTextureFile(Helpers.TextureFlags.None, globalText, imageName), (int)options, 0);
            }
            else
            {
                if (!File.Exists(trainCarShape.SharedShape.ReferencePath + @"\" + imageName))
                {
                    Trace.TraceInformation("Ignored missing " + imageName + " using default. You can copy the " + imageName + " from OR\'s AddOns folder to " + globalText +
                        ", or place it under " + trainCarShape.SharedShape.ReferencePath);
                    material = viewer.MaterialManager.Load("Scenery", Helpers.GetTextureFile(Helpers.TextureFlags.None, globalText, imageName), (int)options, 0);
                }
                else
                    material = viewer.MaterialManager.Load("Scenery", Helpers.GetTextureFile(Helpers.TextureFlags.None, trainCarShape.SharedShape.ReferencePath + @"\", imageName), (int)options, 0);
            }

            return material;
            //Material = Viewer.MaterialManager.Load("Scenery", Helpers.GetRouteTextureFile(Viewer.Simulator, Helpers.TextureFlags.None, "Speed"), (int)(SceneryMaterialOptions.None | SceneryMaterialOptions.AlphaBlendingBlend), 0);
        }

        //update the digits with current speed or time
        public void UpdateDigit()
        {

            Material UsedMaterial = material; //use default material

            //update text string
            bool Alert;
            string speed = GaugeRenderer.Get3DDigits(out Alert);

            numVertices = numIndices = 0;

            // add leading blanks to consider alignment
            // for backwards compatibiliy with preceding OR releases all Justification values defined by MSTS are considered as left justified
            var leadingBlankCount = 0;
            switch (GaugeRenderer.Alignment)
            {
                case CabViewDigitalRenderer.DigitalAlignment.Cab3DRight:
                    leadingBlankCount = maxDigits - speed.Length;
                    break;
                case CabViewDigitalRenderer.DigitalAlignment.Cab3DCenter:
                    leadingBlankCount = (maxDigits - speed.Length + 1) / 2;
                    break;
                default:
                    break;
            }
            for (int i = leadingBlankCount; i > 0; i--)
                speed = speed.Insert(0, " ");

            if (Alert)//alert use alert meterial
            {
                if (alertMaterial == null)
                    alertMaterial = FindMaterial(true);
                UsedMaterial = alertMaterial;
            }
            //update vertex texture coordinate
            foreach (char ch in speed.Substring(0, Math.Min(speed.Length, maxDigits)))
            {
                var tX = GetTextureCoordX(ch);
                var tY = GetTextureCoordY(ch);
                //create first triangle
                triangleListIndices[numIndices++] = (short)numVertices;
                triangleListIndices[numIndices++] = (short)(numVertices + 2);
                triangleListIndices[numIndices++] = (short)(numVertices + 1);
                // Second triangle:
                triangleListIndices[numIndices++] = (short)numVertices;
                triangleListIndices[numIndices++] = (short)(numVertices + 3);
                triangleListIndices[numIndices++] = (short)(numVertices + 2);

                vertexList[numVertices].TextureCoordinate.X = tX;
                vertexList[numVertices].TextureCoordinate.Y = tY;
                vertexList[numVertices + 1].TextureCoordinate.X = tX + 0.25f;
                vertexList[numVertices + 1].TextureCoordinate.Y = tY;
                vertexList[numVertices + 2].TextureCoordinate.X = tX + 0.25f;
                vertexList[numVertices + 2].TextureCoordinate.Y = tY - 0.25f;
                vertexList[numVertices + 3].TextureCoordinate.X = tX;
                vertexList[numVertices + 3].TextureCoordinate.Y = tY - 0.25f;
                numVertices += 4;
            }

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

        //ACE MAP:
        // 0 1 2 3 
        // 4 5 6 7
        // 8 9 : 
        // . - a p
        private static float GetTextureCoordX(char c)
        {
            float x = (c - '0') % 4 * 0.25f;
            if (c == '.')
                x = 0;
            else if (c == ':')
                x = 0.5f;
            else if (c == ' ')
                x = 0.75f;
            else if (c == '-')
                x = 0.25f;
            else if (c == 'a')
                x = 0.5f; //AM
            else if (c == 'p')
                x = 0.75f; //PM
            if (x < 0)
                x = 0;
            if (x > 1)
                x = 1;
            return x;
        }

        private static float GetTextureCoordY(char c)
        {
            if (c == '0' || c == '1' || c == '2' || c == '3')
                return 0.25f;
            if (c == '4' || c == '5' || c == '6' || c == '7')
                return 0.5f;
            if (c == '8' || c == '9' || c == ':' || c == ' ')
                return 0.75f;
            return 1.0f;
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
