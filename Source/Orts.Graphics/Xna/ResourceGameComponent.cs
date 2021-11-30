using System;
using System.Collections.Generic;

using Microsoft.Xna.Framework;

namespace Orts.Graphics.Xna
{
    /// <summary>
    /// Abstract base class to host (updateable) resources which are expensive to create and 
    /// want to be cached for a certain amount of time, but also get released if no longer used
    /// </summary>
    /// <typeparam name="T">The type of resource to be managed</typeparam>
    public abstract class ResourceGameComponent<T> : GameComponent
    {
        private TimeSpan sweepInterval = TimeSpan.FromSeconds(10);
        private TimeSpan nextSweep;
        private protected Dictionary<int, T> currentResources = new Dictionary<int, T>();
        private protected Dictionary<int, T> previousResources = new Dictionary<int, T>();
        private Dictionary<int, T> sweepResources = new Dictionary<int, T>();
        private readonly bool disposableT = typeof(IDisposable).IsAssignableFrom(typeof(T));

        protected ResourceGameComponent(Game game) : base(game)
        {
            game?.Components.Add(this);
        }

        public override void Initialize()
        {
            base.Initialize();
        }

        protected int SweepInterval
        {
            get => (int)sweepInterval.TotalSeconds;
            set => sweepInterval = TimeSpan.FromSeconds(value);
        }

        public override void Update(GameTime gameTime)
        {
            if (gameTime?.TotalGameTime > nextSweep)
            {

                (currentResources, previousResources, sweepResources) = (sweepResources, currentResources, previousResources);
                if (disposableT)
                {
                    foreach (T value in sweepResources.Values)
                        (value as IDisposable).Dispose();
                }
                sweepResources.Clear();
                nextSweep = gameTime.TotalGameTime.Add(sweepInterval);
            }
            base.Update(gameTime);
        }

        protected override void Dispose(bool disposing)
        {
            Enabled = false;
            if (disposing && disposableT)
            {
                foreach (T value in currentResources.Values)
                    (value as IDisposable).Dispose();
                foreach (T value in previousResources.Values)
                    (value as IDisposable).Dispose();
                foreach (T value in sweepResources.Values)
                    (value as IDisposable).Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
