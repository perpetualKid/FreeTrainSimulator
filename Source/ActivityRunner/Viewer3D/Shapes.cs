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

// This file is the responsibility of the 3D & Environment Team. 

// Experimental code which collapses unnecessarily duplicated primitives when loading shapes.
// WANRING: Slower and not guaranteed to work!
//#define OPTIMIZE_SHAPES_ON_LOAD

// Prints out lots of diagnostic information about the construction of shapes, with regards their sub-objects and hierarchies.
//#define DEBUG_SHAPE_HIERARCHY

// Adds bright green arrows to all normal shapes indicating the direction of their normals.
//#define DEBUG_SHAPE_NORMALS

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Orts.Formats.Msts;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;
using Orts.ActivityRunner.Viewer3D.Common;
using Orts.Common;
using Orts.Common.Xna;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Event = Orts.Common.Event;
using Events = Orts.Common.Events;
using Orts.ActivityRunner.Viewer3D.Shapes;

namespace Orts.ActivityRunner.Viewer3D
{
    public class ShapePrimitive : RenderPrimitive
    {
        public Material Material { get; protected set; }
        public int[] Hierarchy { get; protected set; } // the hierarchy from the sub_object
        public int HierarchyIndex { get; protected set; } // index into the hiearchy array which provides pose for this primitive

        protected internal VertexBuffer VertexBuffer;
        protected internal IndexBuffer IndexBuffer;
        protected internal int MinVertexIndex;
        protected internal int NumVerticies;
        protected internal int PrimitiveCount;
        protected internal VertexBufferBinding[] VertexBufferBindings;

        public ShapePrimitive()
        {
        }

        public ShapePrimitive(GraphicsDevice graphicsDevice, Material material, SharedShape.VertexBufferSet vertexBufferSet, IndexBuffer indexBuffer, int minVertexIndex, int numVerticies, int primitiveCount, int[] hierarchy, int hierarchyIndex)
        {
            Material = material;
            VertexBuffer = vertexBufferSet.Buffer;
            IndexBuffer = indexBuffer;
            MinVertexIndex = minVertexIndex;
            NumVerticies = numVerticies;
            PrimitiveCount = primitiveCount;
            Hierarchy = hierarchy;
            HierarchyIndex = hierarchyIndex;

            DummyVertexBuffer = new VertexBuffer(graphicsDevice, DummyVertexDeclaration, 1, BufferUsage.WriteOnly);
            DummyVertexBuffer.SetData(DummyVertexData);
            VertexBufferBindings = new[] { new VertexBufferBinding(VertexBuffer), new VertexBufferBinding(DummyVertexBuffer) };

        }

        public ShapePrimitive(Material material, SharedShape.VertexBufferSet vertexBufferSet, List<ushort> indexData, GraphicsDevice graphicsDevice, int[] hierarchy, int hierarchyIndex)
            : this(graphicsDevice, material, vertexBufferSet, null, indexData.Min(), indexData.Max() - indexData.Min() + 1, indexData.Count / 3, hierarchy, hierarchyIndex)
        {
            IndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), indexData.Count, BufferUsage.WriteOnly);
            IndexBuffer.SetData(indexData.ToArray());
        }

        public override void Draw()
        {
            if (PrimitiveCount > 0)
            {
                // TODO consider sorting by Vertex set so we can reduce the number of SetSources required.
                graphicsDevice.SetVertexBuffers(VertexBufferBindings);
                graphicsDevice.Indices = IndexBuffer;
//                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, MinVertexIndex, NumVerticies, 0, PrimitiveCount);
                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, PrimitiveCount);
            }
        }

        //[CallOnThread("Loader")]
        public virtual void Mark()
        {
            Material.Mark();
        }
    }

    struct ShapeInstanceData
    {
#pragma warning disable 0649
        public Matrix World;
#pragma warning restore 0649

        public static readonly VertexElement[] VertexElements = {
            new VertexElement(sizeof(float) * 0, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 1),
            new VertexElement(sizeof(float) * 4, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 2),
            new VertexElement(sizeof(float) * 8, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
            new VertexElement(sizeof(float) * 12, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
        };
        public static int SizeInBytes = sizeof(float) * 16;
    }

    public class ShapePrimitiveInstances : RenderPrimitive
    {
        public Material Material { get; protected set; }
        public int[] Hierarchy { get; protected set; } // the hierarchy from the sub_object
        public int HierarchyIndex { get; protected set; } // index into the hiearchy array which provides pose for this primitive
        public int SubObjectIndex { get; protected set; }

        protected VertexBuffer VertexBuffer;
        protected VertexDeclaration VertexDeclaration;
        protected int VertexBufferStride;
        protected IndexBuffer IndexBuffer;
        protected int MinVertexIndex;
        protected int NumVerticies;
        protected int PrimitiveCount;

        protected VertexBuffer InstanceBuffer;
        protected VertexDeclaration InstanceDeclaration;
        protected int InstanceBufferStride;
        protected int InstanceCount;
        protected VertexBufferBinding[] VertexBufferBindings;

        internal ShapePrimitiveInstances(GraphicsDevice graphicsDevice, ShapePrimitive shapePrimitive, Matrix[] positions, int subObjectIndex)
        {
            Material = shapePrimitive.Material;
            Hierarchy = shapePrimitive.Hierarchy;
            HierarchyIndex = shapePrimitive.HierarchyIndex;
            SubObjectIndex = subObjectIndex;
            VertexBuffer = shapePrimitive.VertexBuffer;
            VertexDeclaration = shapePrimitive.VertexBuffer.VertexDeclaration;
            IndexBuffer = shapePrimitive.IndexBuffer;
            MinVertexIndex = shapePrimitive.MinVertexIndex;
            NumVerticies = shapePrimitive.NumVerticies;
            PrimitiveCount = shapePrimitive.PrimitiveCount;

            InstanceDeclaration = new VertexDeclaration(ShapeInstanceData.SizeInBytes, ShapeInstanceData.VertexElements);
            InstanceBuffer = new VertexBuffer(graphicsDevice, InstanceDeclaration, positions.Length, BufferUsage.WriteOnly);
            InstanceBuffer.SetData(positions);
            InstanceCount = positions.Length;
            VertexBufferBindings = new[] { new VertexBufferBinding(VertexBuffer), new VertexBufferBinding(InstanceBuffer, 0, 1) };
        }

        public override void Draw()
        {
            graphicsDevice.Indices = IndexBuffer;
            graphicsDevice.SetVertexBuffers(VertexBufferBindings);
            graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, PrimitiveCount, InstanceCount);
        }
    }

#if DEBUG_SHAPE_NORMALS
    public class ShapeDebugNormalsPrimitive : ShapePrimitive
    {
        public ShapeDebugNormalsPrimitive(Material material, SharedShape.VertexBufferSet vertexBufferSet, List<ushort> indexData, GraphicsDevice graphicsDevice, int[] hierarchy, int hierarchyIndex)
        {
            Material = material;
            VertexBuffer = vertexBufferSet.DebugNormalsBuffer;
            VertexDeclaration = vertexBufferSet.DebugNormalsDeclaration;
            VertexBufferStride = vertexBufferSet.DebugNormalsDeclaration.GetVertexStrideSize(0);
            var debugNormalsIndexBuffer = new List<ushort>(indexData.Count * SharedShape.VertexBufferSet.DebugNormalsVertexPerVertex);
            for (var i = 0; i < indexData.Count; i++)
                for (var j = 0; j < SharedShape.VertexBufferSet.DebugNormalsVertexPerVertex; j++)
                    debugNormalsIndexBuffer.Add((ushort)(indexData[i] * SharedShape.VertexBufferSet.DebugNormalsVertexPerVertex + j));
            IndexBuffer = new IndexBuffer(graphicsDevice, typeof(short), debugNormalsIndexBuffer.Count, BufferUsage.WriteOnly);
            IndexBuffer.SetData(debugNormalsIndexBuffer.ToArray());
            MinVertexIndex = indexData.Min() * SharedShape.VertexBufferSet.DebugNormalsVertexPerVertex;
            NumVerticies = (indexData.Max() - indexData.Min() + 1) * SharedShape.VertexBufferSet.DebugNormalsVertexPerVertex;
            PrimitiveCount = indexData.Count / 3 * SharedShape.VertexBufferSet.DebugNormalsVertexPerVertex;
            Hierarchy = hierarchy;
            HierarchyIndex = hierarchyIndex;
        }

        public override void Draw(GraphicsDevice graphicsDevice)
        {
            if (PrimitiveCount > 0)
            {
                graphicsDevice.VertexDeclaration = VertexDeclaration;
                graphicsDevice.Vertices[0].SetSource(VertexBuffer, 0, VertexBufferStride);
                graphicsDevice.Indices = IndexBuffer;
                graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, MinVertexIndex, NumVerticies, 0, PrimitiveCount);
            }
        }

        //[CallOnThread("Loader")]
        public virtual void Mark()
        {
            Material.Mark();
        }
    }
#endif

    public class TrItemLabel
    {
        public readonly WorldPosition Location;
        public readonly string ItemName;

        /// <summary>
        /// Construct and initialize the class.
        /// This constructor is for the labels of track items in TDB and W Files such as sidings and platforms.
        /// </summary>
        public TrItemLabel(Viewer viewer, in WorldPosition position, TrObject trObj)
        {
            Location = position;
            var i = 0;
            while (true)
            {
                var trID = trObj.getTrItemID(i);
                if (trID < 0)
                    break;
                var trItem = viewer.Simulator.TDB.TrackDB.TrItemTable[trID];
                if (trItem == null)
                    continue;
                ItemName = trItem.ItemName;
                i++;
            }
        }
    }
}
