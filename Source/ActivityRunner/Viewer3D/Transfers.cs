﻿// COPYRIGHT 2012, 2013, 2014 by the Open Rails project.
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

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Common;
using Orts.ActivityRunner.Viewer3D.Shapes;
using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts.Models;

namespace Orts.ActivityRunner.Viewer3D
{
    public class TransferShape : StaticShape
    {
        private readonly Material Material;
        private readonly TransferPrimitive Primitive;
        private readonly float Radius;

        public TransferShape(TransferObject transfer, in WorldPosition position)
            : base(null, RemoveRotation(position), ShapeFlags.AutoZBias)
        {
            Material = viewer.MaterialManager.Load("Transfer", Helpers.GetTransferTextureFile(transfer.FileName));
            Primitive = new TransferPrimitive(viewer, transfer.Width, transfer.Height, position);
            Radius = (float)Math.Sqrt(transfer.Width * transfer.Width + transfer.Height * transfer.Height) / 2;
        }

        private static WorldPosition RemoveRotation(in WorldPosition position)
        {           
            return new WorldPosition(position.TileX, position.TileZ, Matrix.CreateTranslation(position.XNAMatrix.Translation));
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var dTileX = WorldPosition.TileX - viewer.Camera.TileX;
            var dTileZ = WorldPosition.TileZ - viewer.Camera.TileZ;
            var mstsLocation = WorldPosition.Location + new Vector3(dTileX * 2048, 0, dTileZ * 2048);
            var xnaMatrix = Matrix.CreateTranslation(mstsLocation.X, mstsLocation.Y, -mstsLocation.Z);
            frame.AddAutoPrimitive(mstsLocation, Radius, float.MaxValue, Material, Primitive, RenderPrimitiveGroup.World, ref xnaMatrix, Flags);
        }

        internal override void Mark()
        {
            Material.Mark();
            base.Mark();
        }
    }

    public class TransferPrimitive : RenderPrimitive
    {
        private readonly VertexBuffer VertexBuffer;
        private readonly IndexBuffer IndexBuffer;
        private readonly int VertexCount;
        private readonly int PrimitiveCount;

        public TransferPrimitive(Viewer viewer, float width, float height, in WorldPosition position)
        {
            var center = position.Location;
            var radius = (float)Math.Sqrt(width * width + height * height) / 2;
            var minX = (int)Math.Floor((center.X - radius) / 8);
            var maxX = (int)Math.Ceiling((center.X + radius) / 8);
            var minZ = (int)Math.Floor((center.Z - radius) / 8);
            var maxZ = (int)Math.Ceiling((center.Z + radius) / 8);
            var xnaRotation = position.XNAMatrix;
            xnaRotation.Translation = Vector3.Zero;
            Matrix.Invert(ref xnaRotation, out xnaRotation);

            var verticies = new VertexPositionTexture[(maxX - minX + 1) * (maxZ - minZ + 1)];
            for (var x = 0; x <= maxX - minX; x++)
            {
                for (var z = 0; z <= maxZ - minZ; z++)
                {
                    var i = x * (maxZ - minZ + 1) + z;
                    verticies[i].Position.X = (x + minX) * 8 - center.X;
                    verticies[i].Position.Y = viewer.Tiles.LoadAndGetElevation(position.TileX, position.TileZ, (x + minX) * 8, (z + minZ) * 8, false) - center.Y;
                    verticies[i].Position.Z = -(z + minZ) * 8 + center.Z;

                    var tc = new Vector3(verticies[i].Position.X, 0, verticies[i].Position.Z);
                    tc = Vector3.Transform(tc, xnaRotation);
                    verticies[i].TextureCoordinate.X = tc.X / width + 0.5f;
                    verticies[i].TextureCoordinate.Y = tc.Z / height + 0.5f;
                }
            }

            var indicies = new short[(maxX - minX) * (maxZ - minZ) * 6];
            for (var x = 0; x < maxX - minX; x++)
            {
                for (var z = 0; z < maxZ - minZ; z++)
                {
                    // Condition must match TerrainPatch.GetIndexBuffer's condition.
                    if (((x + minX) & 1) == ((z + minZ) & 1))
                    {
                        indicies[(x * (maxZ - minZ) + z) * 6 + 0] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 0));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 1] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 1));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 2] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 0));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 3] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 0));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 4] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 1));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 5] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 1));
                    }
                    else
                    {
                        indicies[(x * (maxZ - minZ) + z) * 6 + 0] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 1));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 1] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 1));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 2] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 0));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 3] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 0));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 4] = (short)((x + 0) * (maxZ - minZ + 1) + (z + 1));
                        indicies[(x * (maxZ - minZ) + z) * 6 + 5] = (short)((x + 1) * (maxZ - minZ + 1) + (z + 0));
                    }
                }
            }

            VertexBuffer = new VertexBuffer(viewer.Game.GraphicsDevice, typeof(VertexPositionTexture), verticies.Length, BufferUsage.WriteOnly);
            VertexBuffer.SetData(verticies);
            VertexCount = verticies.Length;

            IndexBuffer = new IndexBuffer(viewer.Game.GraphicsDevice, typeof(short), indicies.Length, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indicies);
            PrimitiveCount = indicies.Length / 3;
        }

        public override void Draw()
        {
            graphicsDevice.SetVertexBuffer(VertexBuffer);
            graphicsDevice.Indices = IndexBuffer;
            graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, PrimitiveCount);
        }
    }

    public class TransferMaterial : Material
    {
        private readonly Texture2D texture;
        private readonly SamplerState transferSamplerState;
        private readonly SceneryShader shader;
        private readonly int techniqueIndex;

        public TransferMaterial(Viewer viewer, string textureName)
            : base(viewer, textureName)
        {
            texture = Viewer.TextureManager.Get(textureName, true);
            transferSamplerState = new SamplerState
            {
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Clamp,
                Filter = TextureFilter.Anisotropic,
                MaxAnisotropy = 16,
            };
            shader = Viewer.MaterialManager.SceneryShader;
            for (int i = 0; i < shader.Techniques.Count; i++)
            {
                if (shader.Techniques[i].Name == "TransferPS")
                {
                    techniqueIndex = i;
                    break;
                }
            }
        }

        public override void SetState(Material previousMaterial)
        {
            shader.CurrentTechnique = shader.Techniques[techniqueIndex];
            shader.ImageTexture = texture;
            shader.ReferenceAlpha = 10;

            graphicsDevice.BlendState = BlendState.NonPremultiplied;
            graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
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
                    graphicsDevice.SamplerStates[0] = transferSamplerState;
                    item.RenderPrimitive.Draw();

                }
            }
        }

        public override void ResetState()
        {
            var shader = Viewer.MaterialManager.SceneryShader;
            shader.ReferenceAlpha = 0;

            graphicsDevice.BlendState = BlendState.Opaque;
            graphicsDevice.DepthStencilState = DepthStencilState.Default;
        }

        public override bool GetBlending()
        {
            return true;
        }

        public override void Mark()
        {
            Viewer.TextureManager.Mark(texture);
            base.Mark();
        }
    }
}
