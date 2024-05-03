// COPYRIGHT 2010, 2011, 2012, 2013, 2014 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

// This file is the responsibility of the 3D & Environment Team. 

// Define this to log each change of the sound sources.
//#define DEBUG_SOURCE_SOURCES

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Processes.Diagnostics;
using Orts.ActivityRunner.Viewer3D;
using Orts.Common;
using Orts.Simulation;

namespace Orts.ActivityRunner.Processes
{
    internal sealed class SoundProcess : ProcessBase
    {
        // THREAD SAFETY:
        //   All accesses must be done in local variables. No modifications to the objects are allowed except by
        //   assignment of a new instance (possibly cloned and then modified).
        public Dictionary<object, Collection<SoundSourceBase>> SoundSources { get; } = new Dictionary<object, Collection<SoundSourceBase>>();

        private int updateCounter = -1;
        private int asyncUpdatePending;
        private const int FULLUPDATECYCLE = 4; // Number of frequent updates needed till a full update

        private readonly ORTSActSoundSources activitySoundSourceList; // Dictionary of activity sound sources

        public SoundProcess(GameHost gameHost) : base(gameHost, "Sound", 50)
        {
            activitySoundSourceList = new ORTSActSoundSources();
            Profiler.ProfilingData[ProcessType.Sound] = profiler;
        }

        internal override void Start()
        {
            if (gameHost.Settings.SoundDetailLevel > 0)
            {
                base.Start();
            }
        }

        protected override void Initialize()
        {
            base.Initialize();
            OpenAL.Initialize();
        }

        protected override void Update(GameTime gameTime)
        {
            var viewer = (gameHost.State as GameStateViewer3D)?.Viewer;
            if (viewer == null)
                return;

            OpenAL.Listenerf(OpenAL.AL_GAIN, Simulator.Instance.GamePaused ? 0 : gameHost.Settings.SoundVolumePercent / 100f);

            // Update activity sounds
            if (viewer.Simulator.SoundNotify != TrainEvent.None)
            {
                if (viewer.World.GameSounds != null)
                    viewer.World.GameSounds.HandleEvent(viewer.Simulator.SoundNotify);
                viewer.Simulator.SoundNotify = TrainEvent.None;
            }

            // Update all sound in our list
            int RetryUpdate = 0;
            int restartIndex = -1;

            while (RetryUpdate >= 0)
            {
                bool updateInterrupted = false;
                lock (SoundSources)
                {
                    updateCounter++;
                    updateCounter %= FULLUPDATECYCLE;
                    var removals = new List<KeyValuePair<object, SoundSourceBase>>();
                    foreach (var sources in SoundSources)
                    {
                        restartIndex++;
                        if (restartIndex >= RetryUpdate)
                        {
                            for (int i = 0; i < sources.Value.Count; i++)
                            {
                                if (!sources.Value[i].NeedsFrequentUpdate && updateCounter > 0)
                                    continue;

                                if (!sources.Value[i].Update())
                                {
                                    removals.Add(new KeyValuePair<object, SoundSourceBase>(sources.Key, sources.Value[i]));
                                }
                            }
                        }
                        // Check if Add or Remove Sound Sources is waiting to get in - allow it if so.
                        // Update can be a (relatively) long process.
                        if (asyncUpdatePending > 0)
                        {
                            updateInterrupted = true;
                            RetryUpdate = restartIndex;
                            //Trace.TraceInformation("Sound Source Updates Interrupted: {0}, Restart Index:{1}", UpdateInterrupts, restartIndex);
                            break;
                        }

                    }
                    if (!updateInterrupted)
                        RetryUpdate = -1;
                    // Remove Sound Sources for train no longer active.  This doesn't seem to be necessary -
                    // cleanup when a train is removed seems to do it anyway with hardly any delay.
                    foreach (var removal in removals)
                    {
                        // If either of the key or value no longer exist, we can't remove them - so skip over them.
                        if (SoundSources.TryGetValue(removal.Key, out Collection<SoundSourceBase> value) && value.Contains(removal.Value))
                        {
                            removal.Value.Uninitialize();
                            value.Remove(removal.Value);
                            if (value.Count == 0)
                            {
                                SoundSources.Remove(removal.Key);
                            }
                        }
                    }
                }

                //Update check for activity sounds
                if (activitySoundSourceList != null)
                    activitySoundSourceList.Update();
            }
            //if (UpdateInterrupts > 1)
            //    Trace.TraceInformation("Sound Source Update Interrupted more than once: {0}", UpdateInterrupts);
        }

        /// <summary>
        /// Adds the collection of <see cref="SoundSourceBase"/> for a particular <paramref name="owner"/> to the playable sounds.
        /// </summary>
        /// <param name="owner">The object to which the sound sources are attached.</param>
        /// <param name="sources">The sound sources to add.</param>
        public void AddSoundSources(object owner, Collection<SoundSourceBase> sources)
        {
            // We use lock to thread-safely update the list.  Interlocked compare-exchange
            // is used to interrupt the update.
            int j;
            while (asyncUpdatePending < 1)
                j = Interlocked.CompareExchange(ref asyncUpdatePending, 1, 0);
            lock (SoundSources)
                SoundSources.Add(owner, sources);
            while (asyncUpdatePending > 0)
                j = Interlocked.CompareExchange(ref asyncUpdatePending, 0, 1);
        }

        /// <summary>
        /// Adds a single <see cref="SoundSourceBase"/> to the playable sounds.
        /// </summary>
        /// <param name="owner">The object to which the sound is attached.</param>
        /// <param name="source">The sound source to add.</param>
        public void AddSoundSource(object owner, SoundSourceBase source)
        {
            // We use lock to thread-safely update the list.  Interlocked compare-exchange
            // is used to interrupt the update.
            int j;
            while (asyncUpdatePending < 1)
                j = Interlocked.CompareExchange(ref asyncUpdatePending, 1, 0);
            lock (SoundSources)
            {
                if (!SoundSources.ContainsKey(owner))
                    SoundSources.Add(owner, new Collection<SoundSourceBase>());
                SoundSources[owner].Add(source);
            }
            while (asyncUpdatePending > 0)
                j = Interlocked.CompareExchange(ref asyncUpdatePending, 0, 1);
        }

        /// <summary>
        /// Returns whether a particular sound source in the playable sounds is owned by a particular <paramref name="owner"/>.
        /// </summary>
        /// <param name="owner">The object to which the sound might be owned.</param>
        /// <param name="source">The sound source to check.</param>
        /// <returns><see cref="true"/> for a match between <paramref name="owner"/> and <paramref name="source"/>, <see cref="false"/> otherwise.</returns>
        public bool IsSoundSourceOwnedBy(object owner, SoundSourceBase source)
        {
            return SoundSources.TryGetValue(owner, out Collection<SoundSourceBase> sources) && sources.Contains(source);
        }

        /// <summary>
        /// Removes the collection of <see cref="SoundSourceBase"/> for a particular <paramref name="owner"/> from the playable sounds.
        /// </summary>
        /// <param name="owner">The object to which the sound sources are attached.</param>
        public void RemoveSoundSources(object owner)
        {
            // We use lock to thread-safely update the list.  Interlocked compare-exchange
            // is used to interrupt the update.
            int j;
            while (asyncUpdatePending < 1)
                j = Interlocked.CompareExchange(ref asyncUpdatePending, 1, 0);
            lock (SoundSources)
            {
                if (SoundSources.TryGetValue(owner, out Collection<SoundSourceBase> value))
                {
                    foreach (var source in value)
                        source.Uninitialize();
                    SoundSources.Remove(owner);
                }
            }
            while (asyncUpdatePending > 0)
                j = Interlocked.CompareExchange(ref asyncUpdatePending, 0, 1);
        }
    }
}
