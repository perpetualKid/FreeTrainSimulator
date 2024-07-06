using System;

using FreeTrainSimulator.Graphics.Shaders;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.Window.Controls
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

        private Color graphColor;

        public Color GraphColor
        {
            get => graphColor;
            set
            {
                graphColor = value;
                graphColor.A = (byte)(value.A * Window.Owner.WindowOpacity * 1.25);
            }
        }

        public int SampleCount { get; }

        public GraphControl(FormBase window, int x, int y, int width, int height, string minLabel, string maxLabel, string name, int sampleCount = 1024) :
            base(window ?? throw new ArgumentNullException(nameof(window)), x, y, (int)(width * window.Owner.DpiScaling), (int)(height * window.Owner.DpiScaling))
        {
            BorderColor = Color.White;
            graphShader = Window.Owner.GraphShader;
            SampleCount = sampleCount;
            VertexCount = VerticiesPerSample * SampleCount;
            sample = new VertexPosition[VerticiesPerSample];
            columnWidth = 1f / SampleCount;
#pragma warning disable CA2000 // Dispose objects before losing scope
            TextTextureRenderer textTextureRenderer = TextTextureRenderer.Instance(Window.Owner.Game);
#pragma warning restore CA2000 // Dispose objects before losing scope
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
            Window.Owner.GraphShader.ScreenSize = Window.Owner.Size.ToVector2();

            labelNamePosition = Bounds.Location + new Point((Bounds.Size.X - labelName.Width) / 2, 4);
            labelMaxPosition = Bounds.Location + new Point(Bounds.Size.X - labelMax.Width, 4);
            labelMinPosition = Bounds.Location + new Point(Bounds.Size.X - labelMin.Width, Bounds.Size.Y - labelMin.Height);
        }

        public void AddSample(double value)
        {
            AddSample((float)value);
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
            graphShader.Bounds = Bounds;
            graphShader.BorderColor = BorderColor;
            graphShader.GraphColor = GraphColor;
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
            labelName.Dispose();
            base.Dispose(disposing);
        }
    }
}
