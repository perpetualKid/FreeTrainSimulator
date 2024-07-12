// COPYRIGHT 2009, 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
using FreeTrainSimulator.Common.DebugInfo;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Common.Diagnostics
{
    public sealed class GraphicMetrics : DetailInfoBase
    {
        public GraphicsMetrics CurrentMetrics { get; set; }

        public GraphicMetrics() : base(true)
        {
            this["GPU Metrics"] = null;
            this[".0"] = null;
            this["Clear Calls"] = null;
            this["Draw Calls"] = null;
            this["Primitives"] = null;
            this["Textures"] = null;
            this["Sprites"] = null;
            this["Targets"] = null;
            this["PixelShaders"] = null;
            this["VertexShaders"] = null;
        }

        public override void Update(GameTime gameTime)
        {
            if (UpdateNeeded)
            {
                this["Clear Calls"] = $"{CurrentMetrics.ClearCount:N0}";
                this["Draw Calls"] = $"{CurrentMetrics.DrawCount:N0}";
                this["Primitives"] = $"{CurrentMetrics.PrimitiveCount:N0}";
                this["Textures"] = $"{CurrentMetrics.TextureCount:N0}";
                this["Sprites"] = $"{CurrentMetrics.SpriteCount:N0}";
                this["Targets"] = $"{CurrentMetrics.TargetCount:N0}";
                this["PixelShaders"] = $"{CurrentMetrics.PixelShaderCount:N0}";
                this["VertexShaders"] = $"{CurrentMetrics.VertexShaderCount:N0}";
                base.Update(gameTime);
            }
        }
    }
}
