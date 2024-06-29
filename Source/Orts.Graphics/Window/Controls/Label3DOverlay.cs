using System;
using System.Diagnostics;

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Position;
using Orts.Graphics.MapView.Shapes;
using Orts.Graphics.Xna;

namespace Orts.Graphics.Window.Controls
{
    public interface IViewProjection
    {
        ref readonly Matrix Projection { get; }
        ref readonly Matrix View { get; }
        ref readonly WorldLocation Location { get; }
    }

    public enum LabelType
    {
        Car,
        Platform, 
        Sidings,
        TrackDebug,
        RoadTrackDebug,
    }

    public class Label3DOverlay : TextControl
    {
        private static readonly EnumArray<(float VerticalOffset, int MinimumDistance, int MaximumDistance, OutlineRenderOptions OutlineOptions, Color OutlineColor, Color FillColor, System.Drawing.Font TextFont), LabelType> settings =
            new EnumArray<(float, int, int, OutlineRenderOptions, Color, Color, System.Drawing.Font), LabelType>(new[]
            {
                (8.0f, 100, 800, new OutlineRenderOptions(2, ColorExtension.ToSystemDrawingColor(Color.White), ColorExtension.ToSystemDrawingColor(Color.Blue)), Color.White, Color.Blue, FontManager.Scaled(WindowManager.DefaultFontName, System.Drawing.FontStyle.Regular)[(int)(WindowManager.DefaultFontSize * 1.25)]),
                (12.0f, 100, 800, new OutlineRenderOptions(2, ColorExtension.ToSystemDrawingColor(Color.Black), ColorExtension.ToSystemDrawingColor(Color.Yellow)), Color.Black, Color.Yellow, FontManager.Scaled(WindowManager.DefaultFontName, System.Drawing.FontStyle.Regular)[(int)(WindowManager.DefaultFontSize * 1.25)]),
                (18.0f, 100, 500, new OutlineRenderOptions(2, ColorExtension.ToSystemDrawingColor(Color.Black), ColorExtension.ToSystemDrawingColor(Color.Orange)), Color.Black, Color.Orange, FontManager.Scaled(WindowManager.DefaultFontName, System.Drawing.FontStyle.Regular)[(int)(WindowManager.DefaultFontSize * 1.25)]),
                (6.0f, 100, 500, new OutlineRenderOptions(2, ColorExtension.ToSystemDrawingColor(Color.Black), ColorExtension.ToSystemDrawingColor(Color.LightBlue)), Color.Black, Color.LightBlue, FontManager.Scaled(WindowManager.DefaultFontName, System.Drawing.FontStyle.Regular)[(int)(WindowManager.DefaultFontSize * 1.25)]),
                (6.0f, 100, 500, new OutlineRenderOptions(2, ColorExtension.ToSystemDrawingColor(Color.Black), ColorExtension.ToSystemDrawingColor(Color.Salmon)), Color.Black, Color.Salmon, FontManager.Scaled(WindowManager.DefaultFontName, System.Drawing.FontStyle.Regular)[(int)(WindowManager.DefaultFontSize * 1.25)]),
            });

        private readonly IWorldPosition positionSource;
        private readonly IViewProjection viewProjection;
        private readonly float baseline;
        private bool outOfSight;

        private Vector2 labelLocation;
        private Rectangle outlinePointer;
        private Rectangle fillPointer;
        private Color outline;
        private Color fill;
        private Color textAlpha = Color.White;

        private readonly LabelType labelType;

        public Label3DOverlay(FormBase window, string text, LabelType labelType, float baseline,
            IWorldPosition positionSource, IViewProjection viewProjection) :
            base(window, 0, 0, 0, 0)
        {
            Debug.Assert(window is OverlayBase);
            this.labelType = labelType;
            this.viewProjection = viewProjection;
            this.positionSource = positionSource;
            this.baseline = baseline;
            this.text = text;
            this.font = settings[labelType].TextFont;
            this.outlineRenderOptions = settings[labelType].OutlineOptions;
            outline = settings[labelType].OutlineColor;
            fill = settings[labelType].FillColor;
            InitializeText(text);
        }

        internal override void Update(GameTime gameTime, bool shouldUpdate)
        {
            base.Update(gameTime, shouldUpdate);

            ref readonly Viewport viewport = ref Window.Owner.Viewport;
            Vector3 lineLocation3D = positionSource.WorldPosition.XNAMatrix.Translation;
            Vector3 delta = (positionSource.WorldPosition.Tile - viewProjection.Location.Tile).TileVector();
            lineLocation3D.X += delta.X;
            lineLocation3D.Y += baseline + 0.2f;
            lineLocation3D.Z += delta.Z;

            Vector3 lineLocation2DStart = viewport.Project(lineLocation3D, viewProjection.Projection, viewProjection.View, Matrix.Identity);
            if (lineLocation2DStart.Z > 1 || lineLocation2DStart.Z < 0)
            {
                outOfSight = true;
                return; // Out of range or behind the camera
            }

            lineLocation3D.Y += settings[labelType].VerticalOffset;
            float lineLocation2DEndY = viewport.Project(lineLocation3D, viewProjection.Projection, viewProjection.View, Matrix.Identity).Y;

            labelLocation = new Vector2(lineLocation2DStart.X - texture.Width / 2 - 2, lineLocation2DEndY - texture.Height);

            float distance = WorldLocation.GetDistance(positionSource.WorldPosition.WorldLocation, viewProjection.Location).Length();
            float distanceRatio = (MathHelper.Clamp(distance, settings[labelType].MinimumDistance, settings[labelType].MaximumDistance) - settings[labelType].MinimumDistance) / (settings[labelType].MaximumDistance - settings[labelType].MinimumDistance);
            textAlpha.A = fill.A = outline.A = (byte)MathHelper.Lerp(255, 0, distanceRatio);

            outlinePointer = new Rectangle((int)lineLocation2DStart.X - 2, (int)lineLocation2DEndY, 4, (int)(lineLocation2DStart.Y - lineLocation2DEndY));
            fillPointer = new Rectangle((int)lineLocation2DStart.X - 1, (int)lineLocation2DEndY, 2, (int)(lineLocation2DStart.Y - lineLocation2DEndY));

            outOfSight = fill.A == 0; //out of sight
        }

        internal override void Draw(SpriteBatch spriteBatch, Point offset)
        {
            if (outOfSight || texture == null || texture == resourceHolder.EmptyTexture)
                return;
            base.Draw(spriteBatch, offset);

            spriteBatch.Draw(texture, labelLocation, textAlpha);
            Window.Owner.BasicShapes.DrawTexture(BasicTextureType.BlankPixel, outlinePointer, outline, spriteBatch);
            Window.Owner.BasicShapes.DrawTexture(BasicTextureType.BlankPixel, fillPointer, fill, spriteBatch);
        }

        private protected override void RefreshResources(object sender, EventArgs e)
        {
            base.RefreshResources(sender, e);
        }
    }
}
