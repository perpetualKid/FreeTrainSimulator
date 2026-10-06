using System.Drawing;

using FreeTrainSimulator.Common.Position;

namespace FreeTrainSimulator.Toolbox.Hosting
{
    /// <summary>
    /// A hosted map's native physical-pixel geometry relative to the primary shell window.
    /// </summary>
    internal readonly record struct HostedViewportGeometry
    {
        /// <summary>
        /// Native client size of the map panel, in physical pixels.
        /// </summary>
        public Size ClientSize { get; init; }

        /// <summary>
        /// Native screen position of the map panel's client origin, in physical pixels.
        /// </summary>
        public Point ClientOrigin { get; init; }

        /// <summary>
        /// Center of the primary shell's native window rectangle, in physical screen pixels.
        /// </summary>
        public PointD WindowCenter { get; init; }

        /// <summary>
        /// Creates one complete measurement in a single native screen-coordinate system.
        /// </summary>
        public HostedViewportGeometry(Size clientSize, Point clientOrigin, PointD windowCenter)
        {
            ClientSize = clientSize;
            ClientOrigin = clientOrigin;
            WindowCenter = windowCenter;
        }

        /// <summary>
        /// Calculates the viewport's world center that preserves the world point at the primary window center.
        /// </summary>
        internal PointD PreserveWindowCenter(in HostedViewportGeometry previousGeometry, in PointD previousWorldCenter, double scale)
        {
            double deltaX = (ClientOrigin.X - WindowCenter.X + ClientSize.Width / 2d)
                - (previousGeometry.ClientOrigin.X - previousGeometry.WindowCenter.X + previousGeometry.ClientSize.Width / 2d);
            double deltaY = (ClientOrigin.Y - WindowCenter.Y + ClientSize.Height / 2d)
                - (previousGeometry.ClientOrigin.Y - previousGeometry.WindowCenter.Y + previousGeometry.ClientSize.Height / 2d);

            // Screen Y grows downward, while map world Y grows upward.
            return new PointD(previousWorldCenter.X + deltaX / scale, previousWorldCenter.Y - deltaY / scale);
        }
    }
}
