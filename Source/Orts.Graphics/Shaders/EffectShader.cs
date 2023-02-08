using System;
using System.Diagnostics;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common.Info;

namespace Orts.Graphics.Shaders
{
    public abstract class EffectShader : Effect
    {
        private readonly byte worldIndex = byte.MaxValue;
        private readonly byte wvpIndex = byte.MaxValue;

#pragma warning disable CA1044 // Properties should not be write only
        public Matrix World
        {
            set => Parameters[worldIndex].SetValue(value);
        }

        public Matrix WorldViewProjection 
        {
            set => Parameters[wvpIndex].SetValue(value);
        }
#pragma warning restore CA1044 // Properties should not be write only

        protected EffectShader(GraphicsDevice graphicsDevice, string shaderName) :
            base(graphicsDevice, GetEffectCode(shaderName + "Shader"))
        {
            for (byte i = 0; i < Parameters.Count; i++)
            {
                if (Parameters[i].Name.Equals("World", StringComparison.OrdinalIgnoreCase))
                    worldIndex = i;
                if (Parameters[i].Name.Equals("WorldViewProjection", StringComparison.OrdinalIgnoreCase))
                    wvpIndex = i;
            }
        }

        public virtual void SetState() { }

        public virtual void ResetState() { }


        private static byte[] GetEffectCode(string fileName)
        {
            try
            {
                string filePath = Path.Combine(RuntimeInfo.ContentFolder, fileName + ".mgfx");
                return File.ReadAllBytes(filePath);
            }
            catch (Exception exception)
            {
                Trace.TraceError($"Error while loading effect shader '{fileName}': {exception.Message}");
                throw;
            }
        }

    }
}
