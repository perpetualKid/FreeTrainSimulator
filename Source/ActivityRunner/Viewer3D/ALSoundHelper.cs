// COPYRIGHT 2011, 2012, 2013 by the Open Rails project.
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using Orts.ActivityRunner.Viewer3D.RollingStock;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;

namespace Orts.ActivityRunner.Viewer3D
{
    public enum PlayMode
    {
        OneShot,            // Start playing the whole sound stream once, then stop
        Loop,               // Start looping the whole stream, release it only at the end
        LoopRelease,        // Start by playing the first part, then start looping the sustain part of the stream
        Release,            // Release the sound by playing the looped sustain part till its end, then play the last part
        ReleaseWithJump     // Release the sound by playing the looped sustain part till the next cue point, then jump to the last part and play that
    };

    public enum PlayState
    {
        NOP,
        New,
        Playing,
    }

    /// <summary>
    /// Represents a piece of sound => an opened wave file, separated by the CUE points
    /// </summary>
    public class SoundPiece : IDisposable
    {
        private readonly bool mstsMonoTreatment;
        private bool disposedValue;
        private readonly int numCuePoints;

        public string Name { get; }
        public int Frequency { get; }
        public int BitsPerSample { get; }
        public int Channels { get; }
        public bool External { get; }
        public bool ReleasedWithJump { get; }
        public bool MstsMonoTreatment => mstsMonoTreatment;

        /// <summary>
        /// How many SoundItems use this. When it falls back to 0, SoundPiece can be disposed
        /// </summary>
        public int RefCount { get; set; } = 1;

        private readonly int[] bufferIDs;
        private readonly int[] bufferLens;

        private const float checkPointS = 0.2f; // In seconds. Should not be set to less than total Thread.Sleep() / 1000
        private readonly float checkFactor; // In bytes, without considering pitch

        private readonly bool valid;
        private readonly bool single;
        private readonly int length;

        /// <summary>
        /// Next buffer to queue when streaming
        /// </summary>
        public int NextBuffer { get; internal set; }
        /// <summary>
        /// Number of CUE points displayed by Sound Debug Form
        /// </summary>
        public int NumCuePoints => numCuePoints;

        public int Length => length;

        /// <summary>
        /// Has no CUE points
        /// </summary>
        public bool SingleCue => single;


        /// <summary>
        /// Constructs a Sound Piece
        /// </summary>
        /// <param name="name">Name of the wave file to open</param>
        /// <param name="external">True if external sound, must be converted to mono</param>
        /// <param name="releasedWithJump">True if sound possibly be released with jump</param>
        public SoundPiece(string name, bool external, bool releasedWithJump)
        {
            Name = name;
            External = external;
            ReleasedWithJump = releasedWithJump;
            if (!WaveFileData.OpenWavFile(Name, ref bufferIDs, ref bufferLens, External, releasedWithJump, ref numCuePoints, ref mstsMonoTreatment))
            {
                bufferIDs = new int[1];
                bufferIDs[0] = 0;
                valid = false;
                Trace.TraceWarning("Skipped unopenable wave file {0}", Name);
            }
            else
            {
                valid = true;
                single = bufferIDs.Length == 1;
                length = bufferLens.Sum();

                int bid = bufferIDs[0];

                foreach (int i in bufferIDs)
                    if (i != 0)
                    {
                        bid = i;
                        break;
                    }

                OpenAL.GetBufferi(bid, OpenAL.AL_FREQUENCY, out int tmp);
                Frequency = tmp;

                OpenAL.GetBufferi(bid, OpenAL.AL_BITS, out tmp);
                BitsPerSample = tmp;

                OpenAL.GetBufferi(bid, OpenAL.AL_CHANNELS, out tmp);
                Channels = tmp;

                checkFactor = (checkPointS * (float)(Frequency * Channels * BitsPerSample / 8));
            }
        }

        /// <summary>
        /// Check if buffer belongs to this sound piece
        /// </summary>
        /// <param name="bufferID">ID of the buffer to check</param>
        /// <returns>True if buffer belongs here</returns>
        public bool IsMine(int bufferID)
        {
            return bufferID != 0 && bufferIDs.Any(value => bufferID == value);
        }

        public bool IsLast(int soundSourceID)
        {
            if (single)
                return true;

            OpenAL.GetSourcei(soundSourceID, OpenAL.AL_BUFFER, out int bufferID);
            return bufferID == 0 || bufferID == bufferIDs.LastOrDefault(value => value > 0);
        }

        public bool IsFirst(int bufferID)
        {
            return bufferID == bufferIDs[0];
        }

        public bool IsSecond(int bufferID)
        {
            return bufferID != bufferIDs.Last() && bufferID == bufferIDs.LastOrDefault(value => value > 0);
        }

        /// <summary>
        /// Queue all buffers as AL_STREAMING
        /// </summary>
        /// <param name="soundSourceID"></param>
        public void QueueAll(int soundSourceID)
        {
            for (int i = 0; i < bufferIDs.Length; i++)
                if (bufferIDs[i] != 0)
                    OpenAL.SourceQueueBuffers(soundSourceID, 1, ref bufferIDs[i]);
        }

        /// <summary>
        /// Queue only the next buffer as AL_STREAMING
        /// </summary>
        /// <param name="soundSourceID"></param>
        public void Queue2(int soundSourceID)
        {
            if (!valid)
                return;
            if (bufferIDs[NextBuffer] != 0)
                OpenAL.SourceQueueBuffers(soundSourceID, 1, ref bufferIDs[NextBuffer]);
            if (bufferIDs.Length > 1)
            {
                NextBuffer++;
                NextBuffer %= bufferIDs.Length - 1;
                if (NextBuffer == 0)
                    NextBuffer++;
            }
        }

        /// <summary>
        /// Queue only the final buffer as AL_STREAMING
        /// </summary>
        /// <param name="soundSourceID"></param>
        public void Queue3(int soundSourceID)
        {
            if (valid && !SingleCue && bufferIDs[^1] != 0)
                OpenAL.SourceQueueBuffers(soundSourceID, 1, ref bufferIDs[^1]);
            NextBuffer = 0;
        }

        /// <summary>
        /// Assign buffer to OpenAL sound source as AL_STATIC type for soft looping
        /// </summary>
        /// <param name="soundSourceID"></param>
        public void SetBuffer(int soundSourceID)
        {
            OpenAL.Sourcei(soundSourceID, OpenAL.AL_BUFFER, bufferIDs[0]);
        }

        /// <summary>
        /// Checkpoint when the buffer near exhausting.
        /// </summary>
        /// <param name="soundSourceID">ID of the AL sound source</param>
        /// <param name="bufferID">ID of the buffer</param>
        /// <param name="pitch">Current playback pitch</param>
        /// <returns>True if near exhausting</returns>
        public bool IsCheckpoint(int soundSourceID, int bufferID, float pitch)
        {
            int bid = -1;
            for (int i = 0; i < bufferIDs.Length; i++)
                if (bufferID == bufferIDs[i])
                {
                    bid = i;
                    break;
                }
            if (bid == -1)
                return false;

            int len = (int)(checkFactor * pitch);
            if (bufferLens[bid] < len)
                return true;

            OpenAL.GetSourcei(soundSourceID, OpenAL.AL_BYTE_OFFSET, out int pos);

            return bufferLens[bid] - len < pos && pos < bufferLens[bid];
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                for (int i = 0; i < bufferIDs.Length; i++)
                {
                    if (bufferIDs[i] != 0) OpenAL.DeleteBuffers(1, ref bufferIDs[i]);
                }
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        ~SoundPiece()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// The SoundItem represents a playable item: the sound to play, the play mode, the pitch. 
    /// A Sound Piece may used by multiple Sound Items
    /// </summary>
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
#pragma warning disable CA1815 // Override equals and operator equals on value types
    public struct SoundItem : IDisposable
#pragma warning restore CA1815 // Override equals and operator equals on value types
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        /// <summary>
        /// Wave data to use. A Sound Piece may used by multiple Sound Items
        /// </summary>
#pragma warning disable CA1051 // Do not declare visible instance fields
        public SoundPiece SoundPiece;
        /// <summary>
        /// Currently executing sound command
        /// </summary>
        public PlayMode PlayMode;
        /// <summary>
        /// Frequency
        /// </summary>
        public float Pitch;
        /// <summary>
        /// Whether can utilize OpenAL Soft Loop Points extension.
        /// Can be publicly set to false in order to disable using the extension for allowing smooth transition.
        /// </summary>
        public bool SoftLoopPoints;
#pragma warning restore CA1051 // Do not declare visible instance fields

        private PlayState playState;

        public PlayState PlayState
        {
            get => playState;
            set
            {
                if (value == PlayState.NOP && playState != PlayState.NOP && SoundPiece != null)
                    SoundPiece.RefCount--;
                playState = value;
            }
        }

        /// <summary>
        /// Cache containing all wave data
        /// </summary>
        public static Dictionary<string, SoundPiece> AllPieces { get; } = new Dictionary<string, SoundPiece>();

        /// <summary>
        /// Sets the Item's piece by its name.
        /// Tries to load the file if not found in cache
        /// </summary>
        /// <param name="Name">Name of the file</param>
        /// <param name="IsExternal">True if external sound</param>
        /// <param name="isReleasedWithJump">True if sound possibly be released with jump</param>
        public void SetPiece(string name, bool IsExternal, bool isReleasedWithJump)
        {
            if (string.IsNullOrEmpty(name))
                return;

            string n = GetKey(name, IsExternal, isReleasedWithJump);

            if (AllPieces.TryGetValue(n, out SoundPiece value))
            {
                SoundPiece = value;
                SoundPiece.RefCount++;
                if (SoundPiece.RefCount < 1)
                    SoundPiece.RefCount = 1;
            }
            else
            {
                SoundPiece = new SoundPiece(name, IsExternal, isReleasedWithJump);
                AllPieces.Add(n, SoundPiece);
            }
            // OpenAL soft loop points extension is disabled until a better way is found for handling smooth transitions with it.
            //SoftLoopPoints = SoundPiece.isSingle;
        }

        /// <summary>
        /// Delete wave data from cache if is no longer in use
        /// </summary>
        /// <param name="name">File name</param>
        /// <param name="isExternal">True if external sound</param>
        /// <param name="isReleasedWithJump">True if sound possibly be released with jump</param>
        public static void Sweep(string name, bool isExternal, bool isReleasedWithJump)
        {
            if (string.IsNullOrEmpty(name))
                return;

            string key = GetKey(name, isExternal, isReleasedWithJump);
            if (AllPieces.TryGetValue(key, out SoundPiece value) && value.RefCount < 1)
            {
                value.Dispose();
                AllPieces.Remove(key);
            }
        }

        /// <summary>
        /// Generate unique key for storing wave data in cache
        /// </summary>
        /// <param name="name"></param>
        /// <param name="external"></param>
        /// <param name="releasedWithJump"></param>
        /// <returns></returns>
        public static string GetKey(string name, bool external, bool releasedWithJump)
        {
            string key = name;
            if (releasedWithJump)
                key += ".j";
            if (external)
                key += ".x";
            return key;
        }

        /// <summary>
        /// Whether is close to exhausting while playing
        /// </summary>
        /// <param name="soundSourceID"></param>
        /// <param name="bufferID"></param>
        /// <returns></returns>
        public bool IsCheckpoint(int soundSourceID, int bufferID)
        {
            return SoundPiece.IsCheckpoint(soundSourceID, bufferID, Pitch);
        }

        public bool IsCheckpoint(int soundSourceID, float pitch)
        {
            Pitch = pitch;
            OpenAL.GetSourcei(soundSourceID, OpenAL.AL_BUFFER, out int bufferID);
            return IsCheckpoint(soundSourceID, bufferID);
        }

        /// <summary>
        /// Updates queue of Sound Piece sustain part for looping or quick releasing
        /// </summary>
        /// <param name="soundSourceID">ID of the AL Sound Source</param>
        /// <param name="pitch">The current pitch of the sound</param>
        /// <returns>False if finished queueing the last chunk in sustain part, True if needs further calling for full Release</returns>
        public bool Update(int soundSourceID, float pitch)
        {
            Pitch = pitch;

            if (PlayMode == PlayMode.Release && SoundPiece.NextBuffer < 2 || PlayMode == PlayMode.ReleaseWithJump)
                return false;

            int buffersQueued;
            OpenAL.GetSourcei(soundSourceID, OpenAL.AL_BUFFER, out int bufferID);
            if (bufferID == 0)
            {
                OpenAL.GetSourcei(soundSourceID, OpenAL.AL_BUFFERS_QUEUED, out buffersQueued);
                if (buffersQueued == 0)
                    SoundPiece.Queue2(soundSourceID);
                OpenAL.SourcePlay(soundSourceID);
            }
            else if (IsCheckpoint(soundSourceID, bufferID))
            {
                OpenAL.GetSourcei(soundSourceID, OpenAL.AL_BUFFERS_QUEUED, out buffersQueued);
                if (buffersQueued < 2)
                    SoundPiece.Queue2(soundSourceID);
            }

            if (PlayMode == PlayMode.Release && SoundPiece.NextBuffer < 2)
                return false;

            return true;
        }

        /// <summary>
        /// Initializes the playing of the item, considering its PlayMode
        /// </summary>
        /// <param name="soundSourceID">ID of the AL sound source</param>
        public bool InitItemPlay(int soundSourceID)
        {
            bool needsFrequentUpdate = false;

            // Get out of AL_LOOP_POINTS_SOFT type playing
            OpenAL.GetSourcei(soundSourceID, OpenAL.AL_SOURCE_TYPE, out int type);
            if (type == OpenAL.AL_STATIC)
            {
                OpenAL.GetSourcei(soundSourceID, OpenAL.AL_SOURCE_STATE, out int state);
                if (state != OpenAL.AL_PLAYING)
                {
                    OpenAL.Sourcei(soundSourceID, OpenAL.AL_BUFFER, OpenAL.AL_NONE);
                    type = OpenAL.AL_UNDETERMINED;
                }
            }

            // Put initial buffers into play
            switch (PlayMode)
            {
                case PlayMode.OneShot:
                case PlayMode.Loop:
                    {
                        if (type != OpenAL.AL_STATIC)
                        {
                            SoundPiece.QueueAll(soundSourceID);
                            PlayState = PlayState.Playing;
                        }
                        else
                            // We need to come back ASAP
                            needsFrequentUpdate = true;
                        break;
                    }
                case PlayMode.LoopRelease:
                    {
                        if (SoftLoopPoints && SoundPiece.SingleCue)
                        {
                            // Utilizing AL_LOOP_POINTS_SOFT. We need to set a static buffer instead of queueing that.
                            OpenAL.GetSourcei(soundSourceID, OpenAL.AL_SOURCE_STATE, out int state);
                            if (state != OpenAL.AL_PLAYING)
                            {
                                OpenAL.SourceStop(soundSourceID);
                                OpenAL.Sourcei(soundSourceID, OpenAL.AL_BUFFER, OpenAL.AL_NONE);
                                SoundPiece.SetBuffer(soundSourceID);
                                OpenAL.SourcePlay(soundSourceID);
                                OpenAL.Sourcei(soundSourceID, OpenAL.AL_LOOPING, OpenAL.AL_TRUE);
                                PlayState = PlayState.Playing;
                            }
                            else
                                // We need to come back ASAP
                                needsFrequentUpdate = true;
                        }
                        else
                        {
                            if (type != OpenAL.AL_STATIC)
                            {
                                SoundPiece.NextBuffer = 0;
                                SoundPiece.Queue2(soundSourceID);
                                PlayState = PlayState.Playing;
                            }
                            else
                                // We need to come back ASAP
                                needsFrequentUpdate = true;
                        }
                        break;
                    }
                default:
                    {
                        PlayState = PlayState.NOP;
                        break;
                    }
            }
            return needsFrequentUpdate;
        }

        /// <summary>
        /// Finishes the playing cycle, in case of the ReleaseLoopReleaseWithJump
        /// </summary>
        /// <param name="soundSourceID">ID of the AL sound source</param>
        internal void LeaveItemPlay(int soundSourceID)
        {
            if (PlayMode == PlayMode.ReleaseWithJump || PlayMode == PlayMode.Release)
            {
                SoundPiece.Queue3(soundSourceID);
                PlayState = PlayState.NOP;
            }
        }

        public void Dispose()
        {
            SoundPiece.Dispose();
        }
    }

    /// <summary>
    /// Represents an OpenAL sound source -- 
    /// One MSTS Sound Stream contains one AL Sound Source
    /// </summary>
    public class ALSoundSource : IDisposable
    {
        private const int QUEUELENGHT = 16;
        private bool looping;
        private readonly float rolloffFactor = 1;
        private int soundSourceID = -1;

        private readonly SoundItem[] soundQueue = new SoundItem[QUEUELENGHT];

        /// <summary>
        /// ID generated automatically by OpenAL, when activated
        /// </summary>
        public int SoundSourceID => soundSourceID;

        /// <summary>
        /// Next command is to be inserted here in queue
        /// </summary>
        private int queueHeader;
        /// <summary>
        /// Currently processing command in queue
        /// </summary>
        private int queueTail;

        /// <summary>
        /// Whether needs active management, or let just OpenAL do the job
        /// </summary>
        private bool needsFrequentUpdate;

        public bool NeedsFrequentUpdate => needsFrequentUpdate;

        /// <summary>
        /// Attached TrainCar
        /// </summary>
        private TrainCarViewer Car;

        /// <summary>
        /// Whether world position should be ignored
        /// </summary>
        private bool ignore3D;

        /// <summary>
        /// Constructs a new AL sound source
        /// </summary>
        /// <param name="isEnv">True if environment sound</param>
        /// <param name="rolloffFactor">The number indicating the fade speed by the distance</param>
        public ALSoundSource(bool isEnv, float rolloffFactor)
        {
            soundSourceID = -1;
            soundQueue[queueTail].PlayState = PlayState.NOP;
            this.rolloffFactor = rolloffFactor;
        }

        private bool mustActivate;
        private static int activeCount;

        public static int ActiveCount => activeCount;

        private static bool MustWarn = true;
        /// <summary>
        /// Tries allocating a new OpenAL SoundSourceID, warns if failed, and sets OpenAL attenuation parameters.
        /// Returns 1 if activation was successful, otherwise 0.
        /// </summary>
        private int TryActivate()
        {
            if (!mustActivate || soundSourceID != -1 || !Active)
                return 0;

            OpenAL.GenSources(1, out soundSourceID);

            if (soundSourceID == -1)
            {
                if (MustWarn)
                {
                    Trace.TraceWarning("Sound stream activation failed at number {0}", activeCount);
                    MustWarn = false;
                }
                return 0;
            }

            activeCount++;
            mustActivate = false;
            MustWarn = true;
            wasPlaying = false;
            stoppedAt = double.MaxValue;

            OpenAL.Sourcef(soundSourceID, OpenAL.AL_MAX_DISTANCE, SoundSource.MaxDistanceM);
            OpenAL.Sourcef(soundSourceID, OpenAL.AL_REFERENCE_DISTANCE, SoundSource.ReferenceDistanceM);
            OpenAL.Sourcef(soundSourceID, OpenAL.AL_MAX_GAIN, 1f);
            OpenAL.Sourcef(soundSourceID, OpenAL.AL_ROLLOFF_FACTOR, rolloffFactor);
            OpenAL.Sourcef(soundSourceID, OpenAL.AL_PITCH, PlaybackSpeed);
            OpenAL.Sourcei(soundSourceID, OpenAL.AL_LOOPING, looping ? OpenAL.AL_TRUE : OpenAL.AL_FALSE);

            InitPosition();
            SetVolume();

            //if (OpenAL.HornEffectSlotID <= 0)
            //    OpenAL.CreateHornEffect();
            //
            //OpenAL.alSource3i(SoundSourceID, OpenAL.AL_AUXILIARY_SEND_FILTER, OpenAL.HornEffectSlotID, 0, OpenAL.AL_FILTER_NULL);

            return 1;
        }

        /// <summary>
        /// Set OpenAL gain
        /// </summary>
        private void SetVolume()
        {
            OpenAL.Sourcef(soundSourceID, OpenAL.AL_GAIN, Active ? Volume : 0);
        }

        /// <summary>
        /// Set whether to ignore 3D position of sound source
        /// </summary>
        public void InitPosition()
        {
            if (ignore3D)
            {
                OpenAL.Sourcei(soundSourceID, OpenAL.AL_SOURCE_RELATIVE, OpenAL.AL_TRUE);
                OpenAL.Sourcef(soundSourceID, OpenAL.AL_DOPPLER_FACTOR, 0);
                OpenAL.Source3f(soundSourceID, OpenAL.AL_POSITION, 0, 0, 0);
                OpenAL.Source3f(soundSourceID, OpenAL.AL_VELOCITY, 0, 0, 0);
            }
            else
            {
                OpenAL.Sourcei(soundSourceID, OpenAL.AL_SOURCE_RELATIVE, OpenAL.AL_FALSE);
                OpenAL.Sourcef(soundSourceID, OpenAL.AL_DOPPLER_FACTOR, 1);

                if (Car != null && !Car.SoundSourceIDs.Contains(soundSourceID))
                    Car.SoundSourceIDs.Add(soundSourceID);
            }
        }

        /// <summary>
        /// Queries a new <see cref="SoundSourceID"/> from OpenAL, if one is not allocated yet.
        /// </summary>
        public void HardActivate(bool ignore3D, TrainCarViewer car)
        {
            this.ignore3D = ignore3D;
            Car = car;
            mustActivate = true;

            if (soundSourceID == -1)
                HardCleanQueue();
        }

        /// <summary>
        /// Frees up the allocated <see cref="SoundSourceID"/>, and cleans the playing queue.
        /// </summary>
        public void HardDeactivate()
        {
            if (soundSourceID != -1)
            {
                if (Car != null)
                    Car.SoundSourceIDs.Remove(soundSourceID);

                Stop();
                OpenAL.DeleteSources(1, ref soundSourceID);
                soundSourceID = -1;
                activeCount--;
            }

            HardCleanQueue();
        }

        private bool active;
        public bool Active
        {
            get => active;
            set { active = value; SetVolume(); }
        }

        private float volume = 1f;
        public float Volume
        {
            get => volume;
            set
            {
                float newval = value < 0 ? 0 : value;

                if (volume != newval)
                {
                    volume = newval;
                    SetVolume();
                    XCheckVolumeAndState();
                }
            }
        }

        private float playbackSpeed = 1;
        public float PlaybackSpeed
        {
            get => playbackSpeed;
            set
            {
                if (playbackSpeed != value)
                {
                    if (!float.IsNaN(value) && value != 0 && !float.IsInfinity(value))
                    {
                        playbackSpeed = value;
                        if (soundSourceID != -1)
                            OpenAL.Sourcef(soundSourceID, OpenAL.AL_PITCH, playbackSpeed);
                    }
                }
            }
        }

        public float SampleRate { get; private set; }
        /// <summary>
        /// Get predicted playing state, not just the copy of OpenAL's
        /// </summary>
        public bool IsPlaying { get; private set; }
        private bool wasPlaying;
        private double stoppedAt = double.MaxValue;

        /// <summary>
        /// Updates Items state and Queue
        /// </summary>
        public void Update()
        {
            lock (soundQueue)
            {
                if (!wasPlaying && IsPlaying)
                    wasPlaying = true;

                SkipProcessed();

                if (queueHeader == queueTail)
                {
                    needsFrequentUpdate = false;
                    XCheckVolumeAndState();
                    return;
                }

                if (soundSourceID != -1)
                {
                    OpenAL.GetSourcei(soundSourceID, OpenAL.AL_BUFFERS_PROCESSED, out int p);
                    while (p > 0)
                    {
                        OpenAL.SourceUnqueueBuffer(soundSourceID);
                        p--;
                    }
                }

                switch (soundQueue[queueTail % QUEUELENGHT].PlayState)
                {
                    case PlayState.Playing:
                        int justActivated = TryActivate();
                        switch (soundQueue[queueTail % QUEUELENGHT].PlayMode)
                        {
                            // Determine next action if available
                            case PlayMode.LoopRelease:
                            case PlayMode.Release:
                            case PlayMode.ReleaseWithJump:
                                if (soundQueue[(queueTail + 1) % QUEUELENGHT].PlayState == PlayState.New
                                    && (soundQueue[(queueTail + 1) % QUEUELENGHT].PlayMode == PlayMode.Release
                                    || soundQueue[(queueTail + 1) % QUEUELENGHT].PlayMode == PlayMode.ReleaseWithJump))
                                {
                                    soundQueue[queueTail % QUEUELENGHT].PlayMode = soundQueue[(queueTail + 1) % QUEUELENGHT].PlayMode;
                                }

                                if (soundQueue[queueTail % QUEUELENGHT].Update(soundSourceID, playbackSpeed))
                                {
                                    Start(); // Restart if buffers had been exhausted because of large update time
                                    needsFrequentUpdate = soundQueue[queueTail % QUEUELENGHT].SoundPiece.ReleasedWithJump;
                                }
                                else
                                {
                                    LeaveLoop();
                                    soundQueue[queueTail % QUEUELENGHT].LeaveItemPlay(soundSourceID);
                                    Start(); // Restart if buffers had been exhausted because of large update time
                                    needsFrequentUpdate = false; // Queued the last chunk, get rest
                                    IsPlaying = false;
                                }

                                break;
                            case PlayMode.Loop:
                            case PlayMode.OneShot:
                                if (soundQueue[(queueTail + 1) % QUEUELENGHT].PlayState == PlayState.New
                                    && (soundQueue[(queueTail + 1) % QUEUELENGHT].PlayMode == PlayMode.Release
                                    || soundQueue[(queueTail + 1) % QUEUELENGHT].PlayMode == PlayMode.ReleaseWithJump))
                                {
                                    soundQueue[queueTail % QUEUELENGHT].PlayMode = soundQueue[(queueTail + 1) % QUEUELENGHT].PlayMode;
                                    soundQueue[(queueTail + 1) % QUEUELENGHT].PlayState = PlayState.NOP;
                                    LeaveLoop();
                                    IsPlaying = false;
                                }
                                else if (soundQueue[queueTail % QUEUELENGHT].PlayMode == PlayMode.Loop)
                                {
                                    // Unlike LoopRelease, which is being updated continuously, 
                                    // unattended Loop must be restarted explicitly after a reactivation.
                                    if (justActivated == 1)
                                        soundQueue[queueTail % QUEUELENGHT].PlayState = PlayState.New;

                                    // The reason of the following is that at Loop type of playing we mustn't EnterLoop() immediately after
                                    // InitItemPlay(), because an other buffer might be playing at that time, and we don't want to loop
                                    // that one. We have to be sure the current loop's buffer is being played already, and all the previous
                                    // ones had been unqueued. This often happens at e.g. Variable2 frequency curves with multiple Loops and
                                    // Releases following each other when increasing throttle.
                                    OpenAL.GetSourcei(soundSourceID, OpenAL.AL_BUFFER, out int bufferID);
                                    if (soundQueue[queueTail % QUEUELENGHT].SoundPiece.IsMine(bufferID))
                                    {
                                        EnterLoop();
                                        IsPlaying = true;
                                        needsFrequentUpdate = false; // Start unattended looping by OpenAL
                                    }
                                    else
                                    {
                                        LeaveLoop(); // Just in case. Wait one more cycle for our buffer,
                                        IsPlaying = false;
                                        needsFrequentUpdate = true; // and watch carefully
                                    }
                                }
                                else if (soundQueue[queueTail % QUEUELENGHT].PlayMode == PlayMode.OneShot)
                                {
                                    needsFrequentUpdate = (soundQueue[(queueTail + 1) % QUEUELENGHT].PlayState != PlayState.NOP);
                                    OpenAL.GetSourcei(soundSourceID, OpenAL.AL_SOURCE_STATE, out int state);
                                    if (state != OpenAL.AL_PLAYING || soundQueue[queueTail % QUEUELENGHT].IsCheckpoint(soundSourceID, playbackSpeed))
                                    {
                                        soundQueue[queueTail % QUEUELENGHT].PlayState = PlayState.NOP;
                                        IsPlaying = false;
                                    }
                                }
                                break;
                        }
                        break;
                    // Found a playable item, play it
                    case PlayState.New:
                        // Only if it is a Play command
                        if (soundQueue[queueTail % QUEUELENGHT].PlayMode != PlayMode.Release
                            && soundQueue[queueTail % QUEUELENGHT].PlayMode != PlayMode.ReleaseWithJump)
                        {
                            justActivated = TryActivate();
                            OpenAL.GetSourcei(soundSourceID, OpenAL.AL_BUFFER, out int bufferID);

                            // If reactivated LoopRelease sound is already playing, then we are at a wrong place, 
                            // no need for reinitialization, just continue after 1st cue point
                            if (IsPlaying && justActivated == 1 && soundQueue[queueTail % QUEUELENGHT].PlayMode == PlayMode.LoopRelease)
                            {
                                soundQueue[queueTail % QUEUELENGHT].PlayState = PlayState.Playing;
                            }
                            // Wait with initialization of a sound piece similar to the previous one, while that is still in queue.
                            // Otherwise we might end up with queueing the same buffers hundreds of times.
                            else if (!IsPlaying || !soundQueue[queueTail % QUEUELENGHT].SoundPiece.IsMine(bufferID))
                            {
                                needsFrequentUpdate = soundQueue[queueTail % QUEUELENGHT].InitItemPlay(soundSourceID);
                                float sampleRate = SampleRate;
                                SampleRate = soundQueue[queueTail % QUEUELENGHT].SoundPiece.Frequency;
                                if (sampleRate != SampleRate && SampleRate != 0)
                                    PlaybackSpeed *= sampleRate / SampleRate;

                                Start();
                                needsFrequentUpdate |= soundQueue[queueTail % QUEUELENGHT].SoundPiece.ReleasedWithJump;
                            }
                        }
                        // Otherwise mark as done
                        else
                        {
                            soundQueue[queueTail % QUEUELENGHT].PlayState = PlayState.NOP;
                            IsPlaying = false;
                            needsFrequentUpdate = false;
                        }
                        break;
                    case PlayState.NOP:
                        needsFrequentUpdate = false;
                        LeaveLoop();
                        IsPlaying = false;

                        break;
                }

                XCheckVolumeAndState();
            }
        }

        /// <summary>
        /// Clear processed commands from queue
        /// </summary>
        private void SkipProcessed()
        {
            while (soundQueue[queueTail % QUEUELENGHT].PlayState == PlayState.NOP && queueHeader != queueTail)
                queueTail++;
        }

        // This is because: different sound items may appear on the same sound source,
        //  with different activation conditions. If the previous sound is not stopped
        //  it would be audible while the new sound must be playing already.
        // So if the volume is set to 0 by the triggers and the sound itself is released
        //  it will be stopped completely.
        private void XCheckVolumeAndState()
        {
            if (volume == 0 && (
                soundQueue[queueTail % QUEUELENGHT].PlayMode == PlayMode.Release ||
                soundQueue[queueTail % QUEUELENGHT].PlayMode == PlayMode.ReleaseWithJump))
            {
                Stop();
                soundQueue[queueTail % QUEUELENGHT].PlayState = PlayState.NOP;
                needsFrequentUpdate = false;
            }

            if (wasPlaying && !IsPlaying || !Active)
            {
                OpenAL.GetSourcei(soundSourceID, OpenAL.AL_SOURCE_STATE, out int state);
                if (state != OpenAL.AL_PLAYING)
                {
                    if (stoppedAt > Simulator.Instance.ClockTime)
                        stoppedAt = Simulator.Instance.GameTime;
                    else if (stoppedAt < Simulator.Instance.GameTime - 0.2)
                    {
                        stoppedAt = double.MaxValue;
                        HardDeactivate();
                        wasPlaying = false;
                        mustActivate = true;
                    }
                }
            }
        }

        /// <summary>
        /// Puts a command with filename into Play Queue. 
        /// Tries to optimize by Name, Mode
        /// </summary>
        /// <param name="Name">Name of the wave to play</param>
        /// <param name="Mode">Mode of the play</param>
        /// <param name="external">Indicator of external sound</param>
        /// <param name="releasedWithJumpOrOneShotRepeated">Indicator if sound may be released with jump (LoopRelease), or is repeated command (OneShot)</param>
        public void Queue(string Name, PlayMode Mode, bool external, bool releasedWithJumpOrOneShotRepeated)
        {
            lock (soundQueue)
            {
                if (soundSourceID == -1)
                {
                    if (Mode == PlayMode.Release || Mode == PlayMode.ReleaseWithJump)
                    {
                        queueHeader = queueTail;
                        soundQueue[queueHeader % QUEUELENGHT].PlayState = PlayState.NOP;
                    }
                    else if (queueHeader != queueTail
                        && soundQueue[(queueHeader - 1) % QUEUELENGHT].SoundPiece.Name == Name
                        && soundQueue[(queueHeader - 1) % QUEUELENGHT].PlayMode == PlayMode.Loop
                        && Mode == PlayMode.Loop)
                    {
                        // Don't put into queue repeatedly
                    }
                    else
                    {
                        // Cannot optimize, put into Queue
                        soundQueue[queueHeader % QUEUELENGHT].SetPiece(Name, external, releasedWithJumpOrOneShotRepeated);
                        soundQueue[queueHeader % QUEUELENGHT].PlayState = PlayState.New;
                        soundQueue[queueHeader % QUEUELENGHT].PlayMode = Mode;
                        if (queueHeader == queueTail && soundQueue[queueTail % QUEUELENGHT].SoundPiece != null)
                            SampleRate = soundQueue[queueTail % QUEUELENGHT].SoundPiece.Frequency;

                        queueHeader++;
                    }

                    return;
                }

                if (queueHeader != queueTail)
                {
                    PlayMode prevMode;
                    SoundItem prev;

                    for (int i = 1; i < 5; i++)
                    {

                        prev = soundQueue[(queueHeader - i) % QUEUELENGHT];

                        prevMode = prev.PlayMode;

                        // Ignore repeated commands
                        // In case we play OneShot, enable repeating same file only by defining it multiple times in sms, otherwise disable.
                        if (prevMode == Mode && (Mode != PlayMode.OneShot || releasedWithJumpOrOneShotRepeated)
                            && prev.SoundPiece != null && prev.SoundPiece.Name == Name)
                            return;
                        if (queueHeader - i == queueTail) break;
                    }

                    prev = soundQueue[(queueHeader - 1) % QUEUELENGHT];
                    prevMode = prev.PlayMode;

                    bool optimized = false;

                    if (prev.PlayState == PlayState.New)
                    {
                        // Optimize play modes
                        switch (prev.PlayMode)
                        {
                            case PlayMode.Loop:
                                if (Mode == PlayMode.Release || Mode == PlayMode.ReleaseWithJump)
                                {
                                    prevMode = Mode;
                                }
                                break;
                            case PlayMode.LoopRelease:
                                if (prev.SoundPiece.Name == Name && Mode == PlayMode.Loop)
                                {
                                    prevMode = Mode;
                                }
                                else if (Mode == PlayMode.Release || Mode == PlayMode.ReleaseWithJump)
                                {
                                    // If interrupted, release it totally. Repeated looping sounds are "parked" with new state,
                                    // so a release command should completely eliminate them
                                    prevMode = Mode;
                                }
                                break;
                            case PlayMode.OneShot:
                                if (prev.SoundPiece.Name == Name && Mode == PlayMode.Loop)
                                {
                                    prevMode = Mode;
                                }
                                break;
                        }

                        if (prevMode != soundQueue[(queueHeader - 1) % QUEUELENGHT].PlayMode)
                        {
                            soundQueue[(queueHeader - 1) % QUEUELENGHT].PlayMode = prevMode;
                            optimized = true;
                        }
                    }

                    // If releasing, then release all older loops as well:
                    if (queueHeader - 1 > queueTail && (Mode == PlayMode.Release || Mode == PlayMode.ReleaseWithJump))
                    {
                        for (int i = queueHeader - 1; i > queueTail; i--)
                            if (soundQueue[(i - 1) % QUEUELENGHT].PlayMode == PlayMode.Loop || soundQueue[(i - 1) % QUEUELENGHT].PlayMode == PlayMode.LoopRelease)
                                soundQueue[(i - 1) % QUEUELENGHT].PlayMode = Mode;
                    }
                    if (optimized)
                        return;
                }

                // Cannot optimize, put into Queue
                soundQueue[queueHeader % QUEUELENGHT].SetPiece(Name, external, Mode == PlayMode.LoopRelease && releasedWithJumpOrOneShotRepeated);
                soundQueue[queueHeader % QUEUELENGHT].PlayState = PlayState.New;
                soundQueue[queueHeader % QUEUELENGHT].PlayMode = Mode;
                // Need an initial sample rate value for frequency curve calculation
                if (queueHeader == queueTail && soundQueue[queueTail % QUEUELENGHT].SoundPiece != null)
                    SampleRate = soundQueue[queueTail % QUEUELENGHT].SoundPiece.Frequency;

                queueHeader++;
            }
        }

        private void HardCleanQueue()
        {
            if (queueHeader == queueTail)
                return;

            int h = queueHeader;
            while (h >= queueTail)
            {
                if (soundQueue[h % QUEUELENGHT].PlayState == PlayState.NOP)
                {
                    h--;
                    continue;
                }

                if ((soundQueue[h % QUEUELENGHT].PlayMode == PlayMode.Loop ||
                    soundQueue[h % QUEUELENGHT].PlayMode == PlayMode.LoopRelease ||
                    (soundQueue[h % QUEUELENGHT].PlayMode == PlayMode.OneShot && soundQueue[h % QUEUELENGHT].SoundPiece.Length > 50000)
                    ) &&
                    (soundQueue[h % QUEUELENGHT].PlayState == PlayState.New ||
                    soundQueue[h % QUEUELENGHT].PlayState == PlayState.Playing))
                    break;

                if (soundQueue[h % QUEUELENGHT].PlayMode == PlayMode.Release ||
                    soundQueue[h % QUEUELENGHT].PlayMode == PlayMode.ReleaseWithJump)
                {
                    h = queueTail - 1;
                }

                h--;
            }

            if (h >= queueTail)
            {
                int i;
                for (i = h - 1; i >= queueTail; i--)
                {
                    soundQueue[i % QUEUELENGHT].PlayState = PlayState.NOP;
                }

                for (i = queueHeader; i > h; i--)
                {
                    soundQueue[i % QUEUELENGHT].PlayState = PlayState.NOP;
                }

                soundQueue[h % QUEUELENGHT].PlayState = PlayState.New;
                soundQueue[(h + 1) % QUEUELENGHT].PlayState = PlayState.NOP;

                queueHeader = h + 1;
                queueTail = h;
            }
            else
            {
                for (int i = queueTail; i <= queueHeader; i++)
                    soundQueue[i % QUEUELENGHT].PlayState = PlayState.NOP;

                queueHeader = queueTail;
            }
        }

        /// <summary>
        /// Start OpenAL playback
        /// </summary>
        private void Start()
        {

            OpenAL.GetSourcei(soundSourceID, OpenAL.AL_SOURCE_STATE, out int state);
            if (state != OpenAL.AL_PLAYING)
                OpenAL.SourcePlay(soundSourceID);
            IsPlaying = true;
        }

        /// <summary>
        /// Stop OpenAL playback and flush buffers
        /// </summary>
        public void Stop()
        {
            OpenAL.SourceStop(soundSourceID);
            OpenAL.Sourcei(soundSourceID, OpenAL.AL_BUFFER, OpenAL.AL_NONE);
            SkipProcessed();
            IsPlaying = false;
        }

        /// <summary>
        /// Instruct OpenAL to enter looping playback mode
        /// </summary>
        private void EnterLoop()
        {
            if (looping)
                return;

            OpenAL.Sourcei(soundSourceID, OpenAL.AL_LOOPING, OpenAL.AL_TRUE);
            looping = true;
        }

        /// <summary>
        /// Instruct OpenAL to leave looping playback
        /// </summary>
        private void LeaveLoop()
        {
            OpenAL.Sourcei(soundSourceID, OpenAL.AL_LOOPING, OpenAL.AL_FALSE);
            looping = false;
        }

        private bool mstsMonoTreatment;
        public bool MstsMonoTreatment
        {
            get
            {
                SoundPiece soundPiece = soundQueue[queueTail % QUEUELENGHT].SoundPiece;
                if (soundPiece != null)
                    mstsMonoTreatment = soundPiece.MstsMonoTreatment;
                return mstsMonoTreatment;
            }
        }

        private static bool muted;
        private bool disposedValue;

        /// <summary>
        /// Sets OpenAL master gain to 100%
        /// </summary>
        public static void UnMuteAll()
        {
            if (muted)
            {
                OpenAL.Listenerf(OpenAL.AL_GAIN, 1);
                muted = false;
            }
        }

        /// <summary>
        /// Sets OpenAL master gain to zero
        /// </summary>
        public static void MuteAll()
        {
            if (!muted)
            {
                OpenAL.Listenerf(OpenAL.AL_GAIN, 0);
                muted = true;
            }
        }

        /// <summary>
        /// Collect data for Sound Debug Form
        /// </summary>
        /// <returns></returns>
        public string[] GetPlayingData()
        {
            string[] retval = new string[4];
            retval[0] = soundSourceID.ToString(CultureInfo.InvariantCulture);

            if (soundQueue[queueTail % QUEUELENGHT].SoundPiece != null)
            {
                retval[1] = soundQueue[queueTail % QUEUELENGHT].SoundPiece.Name.Split('\\').Last();
                retval[2] = soundQueue[queueTail % QUEUELENGHT].SoundPiece.NumCuePoints.ToString(CultureInfo.InvariantCulture);
            }
            else
            {
                retval[1] = "(none)";
                retval[2] = "0";
            }

            if (soundQueue[queueTail % QUEUELENGHT].PlayState != PlayState.NOP)
            {
                retval[3] = $"{soundQueue[queueTail % QUEUELENGHT].PlayState} {soundQueue[queueTail % QUEUELENGHT].PlayMode}{(soundQueue[queueTail % QUEUELENGHT].SoftLoopPoints && soundQueue[queueTail % QUEUELENGHT].PlayMode == PlayMode.LoopRelease ? "Soft" : "")}";
            }
            else
            {
                retval[3] = $"Stopped {soundQueue[queueTail % QUEUELENGHT].PlayMode}";
            }

            retval[3] += $" {queueHeader - queueTail}";

            return retval;
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                if (soundSourceID != -1)
                {
                    OpenAL.DeleteSources(1, ref soundSourceID);
                    activeCount--;
                }
                // TODO: set large fields to null
                disposedValue = true;
            }
        }

        ~ALSoundSource()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: false);
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
