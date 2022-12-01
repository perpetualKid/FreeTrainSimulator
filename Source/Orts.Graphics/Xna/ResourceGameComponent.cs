using System;
using System.Collections.Concurrent;

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
        private TimeSpan sweepInterval = TimeSpan.FromSeconds(30);
        private TimeSpan nextSweep;
        private protected ConcurrentDictionary<int, T> currentResources = new ConcurrentDictionary<int, T>();
        private protected ConcurrentDictionary<int, T> previousResources = new ConcurrentDictionary<int, T>();
        private ConcurrentDictionary<int, T> sweepResources = new ConcurrentDictionary<int, T>();
        private readonly bool disposableT = typeof(IDisposable).IsAssignableFrom(typeof(T));

        protected ResourceGameComponent(Game game) : base(game)
        {
            game?.Components.Add(this);
        }

        protected int SweepInterval
        {
            get => (int)sweepInterval.TotalSeconds;
            set => sweepInterval = TimeSpan.FromSeconds(value);
        }

        public event EventHandler<EventArgs> Refresh;

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
                Refresh?.Invoke(this, EventArgs.Empty);
            }
            base.Update(gameTime);
        }

        private protected T Get(int identifier, Func<T> create)
        {
            if (!currentResources.TryGetValue(identifier, out T resource))
            {
                if (previousResources.TryRemove(identifier, out resource))
                {
                    if (!currentResources.TryAdd(identifier, resource))
                    {
                        if (disposableT)
                            (resource as IDisposable).Dispose();
                    }
                }
                else
                {
                    if ((resource = create()) != null && !currentResources.TryAdd(identifier, resource))
                    {
                        T holder = resource;
                        if (currentResources.TryGetValue(identifier, out resource))
                        {
                            if (disposableT)
                                (holder as IDisposable).Dispose();
                        }
                        else
                            resource = holder;
                    }
                }
            }
            return resource;
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

#pragma warning disable CA1715 // Identifiers should have correct prefix
    public sealed class ResourceGameComponent<T, U> : ResourceGameComponent<T>
#pragma warning restore CA1715 // Identifiers should have correct prefix
    {
        public ResourceGameComponent(Game game) : base(game)
        {
        }

        public T Get(U source, Func<T> create)
        {
            ArgumentNullException.ThrowIfNull(create);
            return Get(source.GetHashCode(), create);
        }
    }

}
