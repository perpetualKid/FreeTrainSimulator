using System;
using System.Collections.Generic;

using FreeTrainSimulator.Graphics.Xna;

namespace FreeTrainSimulator.Graphics.MapView
{
    public sealed class XnaMapTextureHelperHost : IMapTextureHelperHost
    {
        private readonly IReadOnlyCollection<TextureContentComponent> components;

        public XnaMapTextureHelperHost(IEnumerable<TextureContentComponent> components)
        {
            this.components = components is IReadOnlyCollection<TextureContentComponent> collection
                ? collection
                : new List<TextureContentComponent>(components ?? Array.Empty<TextureContentComponent>());
        }

        public void Enable(ContentArea contentArea)
        {
            foreach (TextureContentComponent component in components)
                component.Enable(contentArea);
        }

        public void Disable()
        {
            foreach (TextureContentComponent component in components)
                component.Disable();
        }
    }
}
