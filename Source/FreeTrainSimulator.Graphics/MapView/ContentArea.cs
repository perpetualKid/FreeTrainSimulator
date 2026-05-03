using System;
using System.Runtime.CompilerServices;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Shapes;
using FreeTrainSimulator.Graphics.MapView.Widgets;
using FreeTrainSimulator.Graphics.Xna;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Graphics.MapView
{
    public class ContentArea : DrawableGameComponent, IMapRenderer, IMapViewport, IMapHostControl, IMapBaseOverlayContext, IMapInsetOverlayContext, IMapRulerOverlayContext, IMapCoordinateOverlayContext
    {
        private const int zoomAmplifier = 3;

        internal SpriteBatch SpriteBatch { get; }

        internal BasicShapes BasicShapes { get; }

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
        private readonly IMapViewStateAdapter viewStateAdapter;
        private readonly IMapRenderAdapter renderAdapter;
        private readonly IMapInteractionAdapter interactionAdapter;
#pragma warning restore CA1859 // Use concrete types when possible for improved performance

        private readonly MapTextTextureCache ownedTextCache;

        public ContentBase Content { get; }

        public double Scale => viewStateAdapter.Scale;
        public PointD CenterPoint => viewStateAdapter.CenterPoint;
        public bool SuppressDrawing { get; internal set; }
        public Point WindowSize => viewStateAdapter.WindowSize;
        internal PointD TopLeftBound => viewStateAdapter.TopLeftBound;
        internal PointD BottomRightBound => viewStateAdapter.BottomRightBound;
        public PointD WorldPosition => viewStateAdapter.WorldPosition;
        public System.Drawing.Font CurrentFont => viewStateAdapter.CurrentFont;
        public System.Drawing.Font ConstantSizeFont => viewStateAdapter.ConstantSizeFont;

        internal ContentArea(Game game, ContentBase content, MouseInputGameComponent mouseInputGameComponent) :
            base(game)
        {
            ArgumentNullException.ThrowIfNull(game);

            Content = content ?? throw new ArgumentNullException(nameof(content));

            XnaMapAdapterBundle adapterBundle = new XnaMapAdapterBundle(game, content, mouseInputGameComponent);
            SpriteBatch = adapterBundle.SpriteBatch;
            BasicShapes = adapterBundle.BasicShapes;
            ownedTextCache = adapterBundle.OwnedTextCache;
            viewStateAdapter = adapterBundle.AdapterSet.ViewStateAdapter;
            renderAdapter = adapterBundle.AdapterSet.RenderAdapter;
            interactionAdapter = adapterBundle.AdapterSet.InteractionAdapter;
            interactionAdapter.RegisterMouseMove(MouseMove);
            interactionAdapter.AttachClientSizeChanged(Window_ClientSizeChanged);
            Enabled = false;
        }

        private void Window_ClientSizeChanged(object sender, EventArgs e)
        {
            interactionAdapter.SyncViewportForContent(Content);
        }

        private void RefreshViewportBounds()
        {
            interactionAdapter.SyncViewportForContent(Content);
        }

        public static void UpdateTrackWidthSettings(bool limitTrackWidth)
        {
            MapViewAdapterCore.UpdateTrackWidthSettings(limitTrackWidth);
        }

        public void UpdateColor(ColorSetting setting, Color color, bool fontOutlining)
        {
            renderAdapter.UpdateColor(Content, setting, color, fontOutlining);
        }

        public void MouseMove(Point position, Vector2 delta, GameTime gameTime)
        {
            interactionAdapter.MouseMove(Enabled, position, Content);
        }

        protected override void OnEnabledChanged(object sender, EventArgs args)
        {
            interactionAdapter.HandleEnabledChanged(Enabled, this, Content.TextureHelperHost);
            base.OnEnabledChanged(sender, args);
        }

        public void ResetSize(in Point windowSize, int screenDelta)
        {
            System.Drawing.Font currentFont = CurrentFont;
            interactionAdapter.RefreshAfterReset(Content, windowSize, screenDelta, ref currentFont);
        }

        public void PresetPosition(in PointD centerPoint, double scale)
        {
            interactionAdapter.PresetPosition(centerPoint, scale);
            System.Drawing.Font currentFont = CurrentFont;
            bool suppressDrawing = SuppressDrawing;
            interactionAdapter.RefreshAfterPreset(ref currentFont, ref suppressDrawing);
            SuppressDrawing = suppressDrawing;
        }

        public void SetTrackingPosition(in WorldLocation location)
        {
            interactionAdapter.SetTrackingPosition(location);
        }

        public void SetTrackingPosition(in PointD location)
        {
            interactionAdapter.SetTrackingPosition(location);
        }

        public void UpdateScaleToFit(in PointD topLeft, in PointD bottomRight)
        {
            interactionAdapter.UpdateScaleToFit(topLeft, bottomRight);
            System.Drawing.Font currentFont = CurrentFont;
            interactionAdapter.RefreshAfterScaleChange(ref currentFont);
        }

        public void UpdateScaleAt(in Point scaleAt, int steps)
        {
            interactionAdapter.UpdateScaleAt(scaleAt, steps);
            System.Drawing.Font currentFont = CurrentFont;
            interactionAdapter.RefreshAfterScaleChange(ref currentFont);
        }

        public void UpdateScale(int steps)
        {
            interactionAdapter.UpdateScale(steps);
            System.Drawing.Font currentFont = CurrentFont;
            interactionAdapter.RefreshAfterScaleChange(ref currentFont);
        }

        public void UpdateScaleAbsolute(double scale)
        {
            interactionAdapter.UpdateScaleAbsolute(scale);
            System.Drawing.Font currentFont = CurrentFont;
            interactionAdapter.RefreshAfterScaleChange(ref currentFont);
        }

        public void UpdatePosition(in Vector2 delta)
        {
            interactionAdapter.UpdatePosition(delta);
        }

        public override void Update(GameTime gameTime)
        {
            bool suppressDrawing = SuppressDrawing;
            interactionAdapter.UpdateFrameState(ref suppressDrawing);
            SuppressDrawing = suppressDrawing;
            base.Update(gameTime);
        }

        #region public control commands
        public void MouseDragging(UserCommandArgs userCommandArgs)
        {
            interactionAdapter.MouseDragging(userCommandArgs);
        }

        public void MouseWheelAt(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            interactionAdapter.MouseWheelAt(userCommandArgs, modifiers);
            System.Drawing.Font currentFont = CurrentFont;
            interactionAdapter.RefreshAfterScaleChange(ref currentFont);
        }

        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            interactionAdapter.MouseWheel(userCommandArgs, modifiers);
            System.Drawing.Font currentFont = CurrentFont;
            interactionAdapter.RefreshAfterScaleChange(ref currentFont);
        }

        public void MoveByKeyLeft(UserCommandArgs commandArgs)
        {
            interactionAdapter.MoveByKeyLeft(commandArgs);
        }

        public void MoveByKeyRight(UserCommandArgs commandArgs)
        {
            interactionAdapter.MoveByKeyRight(commandArgs);
        }

        public void MoveByKeyUp(UserCommandArgs commandArgs)
        {
            interactionAdapter.MoveByKeyUp(commandArgs);
        }

        public void MoveByKeyDown(UserCommandArgs commandArgs)
        {
            interactionAdapter.MoveByKeyDown(commandArgs);
        }

        public static int ZoomAmplifier(KeyModifiers modifiers)
        {
            int amplifier = zoomAmplifier;
            if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
                amplifier = 1;
            else if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                amplifier = 5;
            return amplifier;
        }

        public static int ZoomAmplifier(UserCommandArgs commandArgs)
        {
            return commandArgs is ModifiableKeyCommandArgs modifiableKeyCommand ? ZoomAmplifier(modifiableKeyCommand.AdditionalModifiers) : zoomAmplifier;
        }

        public void ZoomIn(UserCommandArgs commandArgs)
        {
            interactionAdapter.ZoomIn(commandArgs);
            System.Drawing.Font currentFont = CurrentFont;
            interactionAdapter.RefreshAfterScaleChange(ref currentFont);
        }

        public void ZoomOut(UserCommandArgs commandArgs)
        {
            interactionAdapter.ZoomOut(commandArgs);
            System.Drawing.Font currentFont = CurrentFont;
            interactionAdapter.RefreshAfterScaleChange(ref currentFont);
        }

        public void ResetZoomAndLocation(Point windowSize, int screenDelta)
        {
            ResetSize(windowSize, screenDelta);
        }
        #endregion

        public override void Draw(GameTime gameTime)
        {
            renderAdapter.DrawContent(Content);
            bool suppressDrawing = SuppressDrawing;
            renderAdapter.NotifyFrameRendered(ref suppressDrawing);
            SuppressDrawing = suppressDrawing;
            base.Draw(gameTime);
        }

        public void DrawLine(float width, Color color, Vector2 point, float length, double angle)
        {
            renderAdapter.DrawLine(width, color, point, length, angle);
        }

        public void DrawLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            renderAdapter.DrawLine(width, color, point1, point2);
        }

        public void DrawDashedLine(float width, Color color, Vector2 point1, Vector2 point2)
        {
            renderAdapter.DrawDashedLine(width, color, point1, point2);
        }

        public void DrawArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize)
        {
            renderAdapter.DrawArc(width, color, point, radius, angle, arcSize);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, bool flipHorizontal, bool flipVertical, bool highlight)
        {
            renderAdapter.DrawTexture(texture, point, angle, size, flipHorizontal, flipVertical, highlight);
        }

        public void DrawTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color)
        {
            renderAdapter.DrawTexture(texture, point, angle, size, color);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            return viewStateAdapter.WorldToScreenCoordinates(worldLocation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return viewStateAdapter.ScreenToWorldCoordinates(screenLocation);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return viewStateAdapter.WorldToScreenCoordinates(location);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return viewStateAdapter.WorldToScreenSize(worldSize, minScreenSize);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InsideScreenArea(PointPrimitive pointPrimitive)
        {
            return viewStateAdapter.InsideScreenArea(pointPrimitive);
        }

        bool IMapViewport.InsideScreenArea(PointPrimitive pointPrimitive)
        {
            return InsideScreenArea(pointPrimitive);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool InsideScreenArea(VectorPrimitive vectorPrimitive)
        {
            return viewStateAdapter.InsideScreenArea(vectorPrimitive);
        }

        bool IMapViewport.InsideScreenArea(VectorPrimitive vectorPrimitive)
        {
            return InsideScreenArea(vectorPrimitive);
        }
        
        public void DrawText(in PointD location, Color color, string text, System.Drawing.Font font, in Vector2 scale, float angle,
            HorizontalAlignment horizontalAlignment, VerticalAlignment verticalAlignment, OutlineRenderOptions outlineRenderOptions)
        {
            renderAdapter.DrawText(location, color, text, font, scale, angle, horizontalAlignment, verticalAlignment, outlineRenderOptions);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ownedTextCache?.Dispose();
                SpriteBatch?.Dispose();
                interactionAdapter.UnregisterMouseMove(MouseMove);
            }
            base.Dispose(disposing);
        }

        public bool IsEnabled
        {
            get => Enabled;
            set => Enabled = value;
        }

        Rectangle IMapInsetOverlayContext.ContentBounds => Content.Bounds;

        bool IMapRulerOverlayContext.UseMetricUnits => Content.UseMetricUnits;

        PointD IMapInsetOverlayContext.TopLeftBound => TopLeftBound;

        PointD IMapInsetOverlayContext.BottomRightBound => BottomRightBound;

        void IMapInsetOverlayContext.DrawOverlayLine(float width, Color color, Vector2 point, float length, double angle, SpriteBatch spriteBatch)
        {
            BasicShapes.DrawLine(width, color, point, length, angle, spriteBatch);
        }

        void IMapInsetOverlayContext.DrawOverlayArc(float width, Color color, Vector2 point, float radius, double angle, double arcSize, SpriteBatch spriteBatch)
        {
            BasicShapes.DrawArc(width, color, point, radius, angle, arcSize, spriteBatch);
        }

        void IMapInsetOverlayContext.DrawOverlayTexture(BasicTextureType texture, Vector2 point, double angle, float size, Color color, SpriteBatch spriteBatch)
        {
            BasicShapes.DrawTexture(texture, point, angle, size, color, spriteBatch);
        }
    }
}
