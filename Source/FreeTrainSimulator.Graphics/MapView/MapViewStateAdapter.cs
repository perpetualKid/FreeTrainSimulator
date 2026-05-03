using System;

using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Graphics.MapView.Widgets;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Graphics.MapView
{
    internal sealed class MapViewStateAdapter : IMapViewStateAdapter, IMapViewFontState
    {
        private readonly IMapViewController controller;
        private System.Drawing.Font currentFont;
        private readonly System.Drawing.Font constantSizeFont;

        public MapViewStateAdapter(IMapViewController controller, System.Drawing.Font constantSizeFont)
        {
            this.controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.constantSizeFont = constantSizeFont ?? throw new ArgumentNullException(nameof(constantSizeFont));
        }

        public double Scale => controller.Scale;

        public PointD CenterPoint => controller.CenterPoint;

        public Point WindowSize => controller.WindowSize;

        public PointD TopLeftBound => controller.TopLeftBound;

        public PointD BottomRightBound => controller.BottomRightBound;

        public PointD WorldPosition => controller.WorldPosition;

        public System.Drawing.Font CurrentFont => currentFont;

        public System.Drawing.Font ConstantSizeFont => constantSizeFont;

        public PointD ScreenToWorldCoordinates(in Point screenLocation)
        {
            return controller.ScreenToWorldCoordinates(screenLocation);
        }

        public Vector2 WorldToScreenCoordinates(in WorldLocation worldLocation)
        {
            return controller.WorldToScreenCoordinates(worldLocation);
        }

        public Vector2 WorldToScreenCoordinates(in PointD location)
        {
            return controller.WorldToScreenCoordinates(location);
        }

        public float WorldToScreenSize(double worldSize, int minScreenSize = 1)
        {
            return controller.WorldToScreenSize(worldSize, minScreenSize);
        }

        public bool InsideScreenArea(PointPrimitive pointPrimitive)
        {
            ArgumentNullException.ThrowIfNull(pointPrimitive);
            return controller.InsideScreenArea(pointPrimitive.Location);
        }

        public bool InsideScreenArea(VectorPrimitive vectorPrimitive)
        {
            ArgumentNullException.ThrowIfNull(vectorPrimitive);
            return controller.InsideScreenArea(vectorPrimitive.Location, vectorPrimitive.Vector);
        }

        public void UpdateCurrentFont(System.Drawing.Font font)
        {
            currentFont = font;
        }
    }
}
