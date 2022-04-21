using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Xna.Framework;

using Orts.Common.Position;
using Orts.Graphics.MapView.Shapes;

namespace Orts.Graphics.MapView.Widgets
{
    public enum TrainPathNodeType
    {
        /// <summary>Node is the start node </summary>
        Start,
        /// <summary>Node is the end node (and not just the last node) </summary>
        End,
        /// <summary>Node is a regular node </summary>
        Other,
        /// <summary>Node is a wait/stop node</summary>
        Stop,
        /// <summary>Node is a junction node at the start of a siding </summary>
        SidingStart,
        /// <summary>Node is a junction node at the end of a siding</summary>
        SidingEnd,
        /// <summary>Node is a reversal node</summary>
        Reverse,
        /// <summary>Temporary node for editing purposes</summary>
        Temporary,
    };

    internal class TrainPathItem : PointWidget
    {
        private protected readonly BasicTextureType textureType;

        internal TrainPathItem(in PointD location, TrainPathNodeType nodeType)
        {
            base.location = location;
            tile = PointD.ToTile(location);
            textureType = nodeType switch
            {
                TrainPathNodeType.Start => BasicTextureType.PathStart,
                TrainPathNodeType.End => BasicTextureType.PathEnd,
                TrainPathNodeType.Other => BasicTextureType.PathNormal,
                TrainPathNodeType.Stop => BasicTextureType.PathWait,
                TrainPathNodeType.SidingStart => BasicTextureType.PathNormal,
                TrainPathNodeType.SidingEnd => BasicTextureType.PathNormal,
                TrainPathNodeType.Reverse => BasicTextureType.PathReverse,
                TrainPathNodeType.Temporary => BasicTextureType.RingCrossed,
                _ => throw new NotImplementedException(),
            };
        }

        internal override void Draw(ContentArea contentArea, ColorVariation colorVariation = ColorVariation.None, double scaleFactor = 1)
        {
            Size = contentArea.Scale switch
            {
                double i when i < 0.3 => 30,
                double i when i < 0.5 => 20,
                double i when i < 0.75 => 15,
                double i when i < 1 => 10,
                double i when i < 3 => 7,
                double i when i < 5 => 5,
                double i when i < 8 => 4,
                _ => 3,
            };
            BasicShapes.DrawTexture(textureType, contentArea.WorldToScreenCoordinates(in Location), 0, contentArea.WorldToScreenSize(Size * scaleFactor), Color.White, contentArea.SpriteBatch);
        }
    }
}
