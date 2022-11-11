using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Graphics.Shaders;
using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public class GraphControl : WindowControl
    {
        private const int VerticiesPerSample = 6;
        private readonly int VertexCount;
        private const int PrimitivesPerSample = 2;

        private readonly Texture2D labelMin;
        private readonly Texture2D labelMax;
        private readonly Texture2D labelName;
        private Point labelMinPosition;
        private Point labelMaxPosition;
        private Point labelNamePosition;

        private readonly VertexBuffer vertexBufferBorder;
        private readonly DynamicVertexBuffer vertedBufferGraph;
        private readonly VertexPosition[] sample;

        private int currentSample;
        private readonly int vertexStride = VertexPosition.VertexDeclaration.VertexStride;
        private readonly float columnWidth;

        private readonly GraphShader graphShader;
        private Vector2 graphSample;

        public Color GraphColor { get; set; }

        public int SampleCount { get; }

        public GraphControl(FormBase window, int x, int y, int width, int height, string minLabel, string maxLabel, string name, int sampleCount = 1024) : base(window, x, y, width, height)
        {
            graphShader = Window.Owner.GraphShader;
            SampleCount = sampleCount;
            VertexCount = VerticiesPerSample * SampleCount;
            sample = new VertexPosition[VerticiesPerSample];
            columnWidth = 1f / SampleCount;
            TextTextureRenderer textTextureRenderer = TextTextureRenderer.Instance(Window.Owner.Game);
            labelMin = textTextureRenderer.RenderText(minLabel, Window.Owner.TextFontSmall, OutlineRenderOptions.Default);
            labelMax = textTextureRenderer.RenderText(maxLabel, Window.Owner.TextFontSmall, OutlineRenderOptions.Default);
            labelName = textTextureRenderer.RenderText(name, Window.Owner.TextFontSmall, OutlineRenderOptions.Default);

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
            Rectangle bounds = Bounds;
            //bounds.Inflate(-10, -2);
            //bounds.Offset(-10, 1);
            Window.Owner.GraphShader.Bounds = bounds;
            Window.Owner.GraphShader.BorderColor = BorderColor;
            Window.Owner.GraphShader.GraphColor = GraphColor;

            labelNamePosition = bounds.Location + new Point((bounds.Size.X - labelName.Width) / 2, 4);
            labelMaxPosition = bounds.Location + new Point((bounds.Size.X - labelMax.Width), 4);
            labelMinPosition = bounds.Location + new Point((bounds.Size.X - labelMax.Width), bounds.Size.Y - labelMin.Height);
        }

        public void AddSample(float value)
        {
            value = MathHelper.Clamp(value, 0, 1);
            float x = (float)currentSample / SampleCount;

            sample[0] = new VertexPosition(new Vector3(x, value, 0));
            sample[1] = new VertexPosition(new Vector3(columnWidth + x, value, 1));
            sample[2] = new VertexPosition(new Vector3(columnWidth + x, 0, 1));
            sample[3] = new VertexPosition(new Vector3(columnWidth + x, 0, 1));
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

            base.Draw(spriteBatch, offset);

            spriteBatch.Draw(labelName, (labelNamePosition + offset).ToVector2(), Color.White);
            spriteBatch.Draw(labelMin, (labelMinPosition + offset).ToVector2(), Color.White);
            spriteBatch.Draw(labelMax, (labelMaxPosition + offset).ToVector2(), Color.White);
        }

        protected override void Dispose(bool disposing)
        {
            vertedBufferGraph.Dispose();
            vertexBufferBorder.Dispose();
            labelMax.Dispose();
            labelMin.Dispose();
            base.Dispose(disposing);
        }
    }
}
