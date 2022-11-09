using System;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.Shaders;

namespace Orts.Graphics.Window.Controls
{
    public class GraphControl : WindowControl
    {
        private const int VerticiesPerSample = 6;
        private readonly int VertexCount;
        private const int PrimitivesPerSample = 2;

        private readonly VertexBuffer vertexBufferBorder;
        private readonly DynamicVertexBuffer vertedBufferGraph;
        private readonly VertexPosition[] sample;

        private int currentSample;
        private readonly int vertexStride = VertexPosition.VertexDeclaration.VertexStride;

        private readonly GraphShader graphShader;
        private Vector2 graphSample;

        public Color GraphColor { get; set; }

        public int SampleCount { get; } = 1024;

        public GraphControl(FormBase window, int x, int y, int width, int height, int sampleCount = 1024) : base(window, x, y, width, height)
        {
            graphShader = Window.Owner.GraphShader;
            SampleCount = sampleCount;
            VertexCount = VerticiesPerSample * SampleCount;
            sample = new VertexPosition[VerticiesPerSample];

            vertexBufferBorder = new VertexBuffer(Window.Owner.GraphicsDevice, typeof(VertexPosition), 5, BufferUsage.WriteOnly);
            vertedBufferGraph = new DynamicVertexBuffer(Window.Owner.GraphicsDevice, typeof(VertexPosition), VertexCount, BufferUsage.WriteOnly);

            vertexBufferBorder.SetData(new[] {
                new VertexPosition(new Vector3(0, 0, 0)),
                new VertexPosition(new Vector3(0, 1, 0)),
                new VertexPosition(new Vector3(1, 1, 0)),
                new VertexPosition(new Vector3(1, 0, 0)),
                new VertexPosition(new Vector3(0, 0, 0)),
            });
            graphSample = new Vector2(0, SampleCount);
        }

        internal override void Initialize()
        {
            Window.Owner.GraphShader.ScreenSize = Window.Owner.ClientBounds.Size.ToVector2();
            Window.Owner.GraphShader.Bounds = Bounds;
            Window.Owner.GraphShader.BorderColor = BorderColor;
            Window.Owner.GraphShader.GraphColor = GraphColor;
        }

        public void AddSample(float value)
        {
            float columnWidth = 1f / SampleCount - 0.0001f;
            value = MathHelper.Clamp(value, 0, 1);
            float x = (float)currentSample / SampleCount;

            sample[0] = new VertexPosition(new Vector3(x, value, 0));
            sample[1] = new VertexPosition(new Vector3(columnWidth + x, value, 0));
            sample[2] = new VertexPosition(new Vector3(columnWidth + x, 0, 0));
            sample[3] = new VertexPosition(new Vector3(columnWidth + x, 0, 0));
            sample[4] = new VertexPosition(new Vector3(x, value, 0));
            sample[5] = new VertexPosition(new Vector3(x, 0, 0));

            vertedBufferGraph.SetData(currentSample * VerticiesPerSample * vertexStride, sample, 0, VerticiesPerSample, vertexStride, SetDataOptions.NoOverwrite);
            currentSample = (currentSample + 1) % SampleCount;
            graphSample.X = currentSample;
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            graphShader.SetState();

            graphShader.CurrentTechnique = Window.Owner.GraphShader.BorderTechnique;
            graphShader.CurrentTechnique.Passes[0].Apply();
            // Draw border
            Window.Owner.GraphicsDevice.SetVertexBuffer(vertexBufferBorder);
            Window.Owner.GraphicsDevice.DrawPrimitives(PrimitiveType.LineStrip, 0, 4);

            graphShader.GraphSample = graphSample;
            graphShader.CurrentTechnique = Window.Owner.GraphShader.GraphTechnique;
            graphShader.CurrentTechnique.Passes[0].Apply();
            // Draw graph
            Window.Owner.GraphicsDevice.SetVertexBuffer(vertedBufferGraph);

            Window.Owner.GraphicsDevice.DrawPrimitives(PrimitiveType.TriangleList, 0, SampleCount * PrimitivesPerSample);
            graphShader.ResetState();
        }

        protected override void Dispose(bool disposing)
        {
            vertedBufferGraph.Dispose();
            vertexBufferBorder.Dispose();
            base.Dispose(disposing);
        }
    }
}
