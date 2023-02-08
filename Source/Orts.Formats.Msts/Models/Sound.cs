﻿using System.Collections.Generic;
using System.IO;
using System.Linq;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts.Parsers;

namespace Orts.Formats.Msts.Models
{
    public class TrackItemSound
    {
#pragma warning disable CA1002 // Do not expose generic lists
        public List<WorldSoundSource> SoundSources { get; } = new List<WorldSoundSource>();
        public List<WorldSoundRegion> SoundRegions { get; } = new List<WorldSoundRegion>();
#pragma warning restore CA1002 // Do not expose generic lists

        internal TrackItemSound(STFReader stf, int trackItemsCount)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("soundsource", ()=>{ SoundSources.Add(new WorldSoundSource(stf)); }),
                new STFReader.TokenProcessor("soundregion", ()=>{ SoundRegions.Add(new WorldSoundRegion(stf, trackItemsCount)); }),
            });
        }
    }

    public class WorldSoundSource
    {
        private Vector3 position;

        public ref readonly Vector3 Position => ref position;
        public string FileName { get; private set; }

        internal WorldSoundSource(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("filename", ()=>{ FileName = stf.ReadStringBlock(null); }),
                new STFReader.TokenProcessor("position", ()=>{
                    stf.MustMatchBlockStart();
                    stf.ReadVector3Block(STFReader.Units.None, ref position);
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }

    public class WorldSoundRegion
    {
        public int TrackType { get; private set; } = -1;
        public float RotY { get; private set; }
#pragma warning disable CA1002 // Do not expose generic lists
        public List<int> TrackNodes { get; private set; }
#pragma warning restore CA1002 // Do not expose generic lists

        internal WorldSoundRegion(STFReader stf, int trackItemsCount)
        {
            TrackNodes = new List<int>();
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("soundregiontracktype", ()=>{ TrackType = stf.ReadIntBlock(-1); }),
                new STFReader.TokenProcessor("soundregionroty", ()=>{ RotY = stf.ReadFloatBlock(STFReader.Units.None, float.MaxValue); }),
                new STFReader.TokenProcessor("tritemid", ()=>{
                    stf.MustMatchBlockStart();
                    stf.ReadInt(0);//dummy read
                    int trItemId = stf.ReadInt(-1);
                    if (trItemId != -1) {
                        if (trItemId >= trackItemsCount) {
                            STFException.TraceWarning(stf, $"Ignored invalid TrItemId {trItemId}");
                        } else {
                            TrackNodes.Add(trItemId);
                        }
                    }
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }

    public class ActivitySound
    {
        private WorldLocation location;

        public string SoundFile { get; private set; }
        public OrtsActivitySoundFileType SoundFileType { get; private set; }
        public ref readonly WorldLocation Location => ref location;
        
        internal ActivitySound(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("ortsactsoundfile", ()=>
                {
                    stf.MustMatchBlockStart();
                    string soundFile = stf.ReadString();
                    SoundFile = Path.Combine(FolderStructure.RouteFromActivity(stf.FileName).SoundFile(soundFile));
                    if (!EnumExtension.GetValue(stf.ReadString(), out OrtsActivitySoundFileType soundFileType))
                    {
                        stf.StepBackOneItem();
                        STFException.TraceInformation(stf, "Skipped unknown activity sound file type " + stf.ReadString());
                        SoundFileType = OrtsActivitySoundFileType.None;
                    }
                    else
                        SoundFileType = soundFileType;
                    stf.MustMatchBlockEnd();
                    }),
                new STFReader.TokenProcessor("ortssoundlocation", ()=>{
                    stf.MustMatchBlockStart();
                    location = new WorldLocation(stf.ReadInt(null), stf.ReadInt(null), 
                        stf.ReadFloat(STFReader.Units.None, null), stf.ReadFloat(STFReader.Units.None, null), stf.ReadFloat(STFReader.Units.None, null));
                    stf.MustMatchBlockEnd();
                }),
            });
        }
    }

    public class SoundActivationCondition
    {
        public bool ExternalCam { get; private set; }
        public bool CabCam { get; private set; }
        public bool PassengerCam { get; private set; }
        public float Distance { get; private set; } = 1000;  // by default we are 'in range' to hear this
        public int TrackType { get; private set; } = -1;
        public ActivationType ActivationType { get; private set; }

        internal SoundActivationCondition(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("externalcam", ()=>{ ExternalCam = stf.ReadBoolBlock(true); }),
                new STFReader.TokenProcessor("cabcam", ()=>{ CabCam = stf.ReadBoolBlock(true); }),
                new STFReader.TokenProcessor("passengercam", ()=>{ PassengerCam = stf.ReadBoolBlock(true); }),
                new STFReader.TokenProcessor("distance", ()=>{ Distance = stf.ReadFloatBlock(STFReader.Units.Distance, Distance); }),
                new STFReader.TokenProcessor("tracktype", ()=>{ TrackType = stf.ReadIntBlock(null); }),
            });
        }

        // for precompiled sound sources for activity sound
        public SoundActivationCondition(OrtsActivitySoundFileType soundFileType, ActivationType activationType)
        {
            switch (soundFileType)
            {
                case OrtsActivitySoundFileType.Everywhere:
                    CabCam = activationType == ActivationType.Activate;
                    ExternalCam = activationType == ActivationType.Activate;
                    PassengerCam = activationType == ActivationType.Activate;
                    break;
                default:
                case OrtsActivitySoundFileType.Cab:
                    CabCam = activationType == ActivationType.Activate;
                    ExternalCam = activationType == ActivationType.Deactivate;
                    PassengerCam = activationType == ActivationType.Deactivate; 
                    break;
                case OrtsActivitySoundFileType.Pass:
                    CabCam = activationType == ActivationType.Deactivate;
                    ExternalCam = activationType == ActivationType.Deactivate;
                    PassengerCam = activationType == ActivationType.Activate;
                    break;
                case OrtsActivitySoundFileType.Ground:
                    CabCam = activationType == ActivationType.Activate;
                    ExternalCam = activationType == ActivationType.Activate;
                    PassengerCam = activationType == ActivationType.Activate;
                    break;
                case OrtsActivitySoundFileType.Location:
                    CabCam = activationType == ActivationType.Activate;
                    ExternalCam = activationType == ActivationType.Activate;
                    PassengerCam = activationType == ActivationType.Activate;
                    break;
            }
        }
    }

    public class ScalabilityGroup
    {
        public int DetailLevel { get; private set; }
        public SmsStreams Streams { get; private set; }
        public float Volume { get; private set; } = 1.0f;
        public bool Stereo { get; private set; }
        public bool Ignore3D { get; private set; }
        public SoundActivationCondition Activation { get; private set; }
        public SoundActivationCondition Deactivation { get; private set; }

        internal ScalabilityGroup(STFReader stf)
        {
            stf.MustMatchBlockStart();
            DetailLevel = stf.ReadInt(null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("activation", ()=>{ Activation = new SoundActivationCondition(stf); }),
                new STFReader.TokenProcessor("deactivation", ()=>{ Deactivation = new SoundActivationCondition(stf); }),
                new STFReader.TokenProcessor("streams", ()=>{ Streams = new SmsStreams(stf); }),
                new STFReader.TokenProcessor("volume", ()=>{ Volume = stf.ReadFloatBlock(STFReader.Units.None, null); }),
                new STFReader.TokenProcessor("stereo", ()=>{ Stereo = stf.ReadBoolBlock(true); }),
                new STFReader.TokenProcessor("ignore3d", ()=>{ Ignore3D = stf.ReadBoolBlock(true); }),
            });
        }
    } // class ScalabiltyGroup

    public class SmsStreams : List<SmsStream>
    {
        internal SmsStreams(STFReader stf)
        {
            stf.MustMatchBlockStart();
            int count = stf.ReadInt(null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("stream", ()=>{
                    if (--count < 0)
                        STFException.TraceWarning(stf, "Skipped extra Stream");
                    else
                        Add(new SmsStream(stf));
                }),
            });
            if (count > 0)
                STFException.TraceWarning(stf, count + " missing Stream(s)");
        }
    }

    public class SmsStream
    {
        public int Priority { get; private set; }
        public Triggers Triggers { get; private set; }
        public float Volume { get; private set; } = 1.0f;
#pragma warning disable CA1002 // Do not expose generic lists
        public List<Curve> VolumeCurves { get; } = new List<Curve>();
#pragma warning restore CA1002 // Do not expose generic lists
        public Curve FrequencyCurve { get; private set; }

        internal SmsStream(STFReader stf)
        {
            stf.MustMatchBlockStart();
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("priority", ()=>{ Priority = stf.ReadIntBlock(null); }),
                new STFReader.TokenProcessor("triggers", ()=>{ Triggers = new Triggers(stf); }),
                new STFReader.TokenProcessor("volumecurve", ()=>{ VolumeCurves.Add(new Curve(stf)); }),
                new STFReader.TokenProcessor("frequencycurve", ()=>{ FrequencyCurve = new Curve(stf); }),
                new STFReader.TokenProcessor("volume", ()=>{ Volume = stf.ReadFloatBlock(STFReader.Units.None, Volume); }),
            });
            //if (Volume > 1)  Volume /= 100f;
        }
    }

#pragma warning disable CA1815 // Override equals and operator equals on value types
    public readonly struct CurvePoint
#pragma warning restore CA1815 // Override equals and operator equals on value types
    {
        public readonly float X, Y;

        public CurvePoint(float x, float y)
        {
            X = x;
            Y = y;
        }
    }

    public class Curve
    {
        public enum ControlMode
        {
            None,
            Distance,
            Speed,
            Variable1,
            Variable2,
            Variable3,
            BrakeCylinder,
            CurveForce,
        };

        public ControlMode Mode { get; private set; } = ControlMode.None;

        public float Granularity { get; private set; } = 1.0f;

#pragma warning disable CA1819 // Properties should not return arrays
        public CurvePoint[] CurvePoints { get; private set; }
#pragma warning restore CA1819 // Properties should not return arrays

        internal Curve(STFReader stf)
        {
            stf.MustMatchBlockStart();
            string type = stf.ReadString();
            switch (type.ToUpperInvariant())
            {
                case "DISTANCECONTROLLED": Mode = ControlMode.Distance; break;
                case "SPEEDCONTROLLED": Mode = ControlMode.Speed; break;
                case "VARIABLE1CONTROLLED": Mode = ControlMode.Variable1; break;
                case "VARIABLE2CONTROLLED": Mode = ControlMode.Variable2; break;
                case "VARIABLE3CONTROLLED": Mode = ControlMode.Variable3; break;
                case "BRAKECYLCONTROLLED": Mode = ControlMode.BrakeCylinder; break;
                case "CURVEFORCECONTROLLED": Mode = ControlMode.CurveForce; break;
                default: STFException.TraceWarning(stf, "Crash expected: Skipped unknown VolumeCurve/Frequencycurve type " + type); stf.SkipRestOfBlock(); return;
            }
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("granularity", ()=>{ Granularity = stf.ReadFloatBlock(STFReader.Units.None, null); }),
                new STFReader.TokenProcessor("curvepoints", ()=>{
                    stf.MustMatchBlockStart();
                    int count = stf.ReadInt(null);
                    CurvePoints = new CurvePoint[count];
                    for (int i = 0; i < count; ++i)
                    {
                        float x = stf.ReadFloat(STFReader.Units.None, null);
                        if (Mode == ControlMode.Distance)
                        {
                            if (x >= 0)
                                x *= x;
                            else
                                x *= -x;
                        }
                        float y = stf.ReadFloat(STFReader.Units.None, null);
                        CurvePoints[i] = new CurvePoint(x, y);
                    }
                    stf.SkipRestOfBlock();
                }),
            });
        }
    }

    public class Triggers : List<Trigger>
    {
        internal Triggers(STFReader stf)
        {
            stf.MustMatchBlockStart();
            int count = stf.ReadInt(null);
            stf.ParseBlock(new STFReader.TokenProcessor[] {
                new STFReader.TokenProcessor("dist_travelled_trigger", ()=>{ Add(new DistanceTravelledTrigger(stf)); }),
                new STFReader.TokenProcessor("discrete_trigger", ()=>{ Add(new DiscreteTrigger(stf)); }),
                new STFReader.TokenProcessor("random_trigger", ()=>{ Add(new RandomTrigger(stf)); }),
                new STFReader.TokenProcessor("variable_trigger", ()=>{ Add(new VariableTrigger(stf)); }),
                new STFReader.TokenProcessor("initial_trigger", ()=>{ Add(new InitialTrigger(stf)); }),
            });
            foreach (Trigger trigger in this)
                if (trigger.SoundCommand == null)
                    STFException.TraceWarning(stf, "Trigger lacks a sound command");
        }
    }

    public abstract class Trigger
    {
        public SoundCommand SoundCommand { get; private set; }

        private int playcommandCount;

        private protected void ParsePlayCommand(STFReader stf, string token)
        {
            switch (token = token.ToUpperInvariant())
            {
                case "PLAYONESHOT":
                case "STARTLOOP":
                case "RELEASELOOPRELEASE":
                case "STARTLOOPRELEASE":
                case "RELEASELOOPRELEASEWITHJUMP":
                case "DISABLETRIGGER":
                case "ENABLETRIGGER":
                case "SETSTREAMVOLUME":
                    ++playcommandCount;
                    if (playcommandCount > 1)
                        STFException.TraceWarning(stf, "Replaced play command");
                    break;
                default:
                    break;
            }

            switch (token)
            {
                case "PLAYONESHOT": SoundCommand = new SoundPlayCommand(stf, SoundPlayCommand.SoundCommandType.PlayOneShot); break;
                case "STARTLOOP": SoundCommand = new SoundPlayCommand(stf, SoundPlayCommand.SoundCommandType.StartLoop); break;
                case "RELEASELOOPRELEASE": SoundCommand = new LoopRelease(stf, LoopRelease.ReleaseType.Release); break;
                case "STARTLOOPRELEASE": SoundCommand = new SoundPlayCommand(stf, SoundPlayCommand.SoundCommandType.StartLoopRelease); break;
                case "RELEASELOOPRELEASEWITHJUMP": SoundCommand = new LoopRelease(stf, LoopRelease.ReleaseType.ReleaseWithJump); break;
                case "DISABLETRIGGER": SoundCommand = new TriggerCommand(stf, TriggerCommand.TriggerType.Disable); break;
                case "ENABLETRIGGER": SoundCommand = new TriggerCommand(stf, TriggerCommand.TriggerType.Enable); break;
                case "SETSTREAMVOLUME": SoundCommand = new StreamVolumeCommand(stf); break;
                case "(": stf.SkipRestOfBlock(); break;
            }
        }
    }

    public class InitialTrigger : Trigger
    {

        internal InitialTrigger(STFReader stf)
        {
            stf.MustMatchBlockStart();
            while (!stf.EndOfBlock())
                ParsePlayCommand(stf, stf.ReadString());
        }
    }

    public class DiscreteTrigger : Trigger
    {

        public int TriggerId { get; private set; }

        internal DiscreteTrigger(STFReader stf)
        {
            stf.MustMatchBlockStart();
            TriggerId = stf.ReadInt(null);
            while (!stf.EndOfBlock())
                ParsePlayCommand(stf, stf.ReadString());
        }
    }

    public class VariableTrigger : Trigger
    {
        public enum TriggerEvent
        {
            SpeedIncrease,
            SpeedDecrease,
            DistanceIncrease,
            DistanceDecrease,
            Variable1Increase,
            Variable1Decrease,
            Variable2Increase,
            Variable2Decrease,
            Variable3Increase,
            Variable3Decrease,
            BrakeCylinderIncrease,
            BrakeCylinderDecrease,
            CurveForceIncrease,
            CurveForceDecrease,
        };

        public TriggerEvent Event { get; private set; }
        public float Threshold { get; private set; }

        internal VariableTrigger(STFReader stf)
        {
            stf.MustMatchBlockStart();

            string eventString = stf.ReadString();
            Threshold = stf.ReadFloat(STFReader.Units.None, null);

            switch (eventString.ToUpperInvariant())
            {
                case "SPEED_INC_PAST": Event = TriggerEvent.SpeedIncrease; break;
                case "SPEED_DEC_PAST": Event = TriggerEvent.SpeedDecrease; break;
                case "DISTANCE_INC_PAST":
                    {
                        Event = TriggerEvent.DistanceIncrease;
                        Threshold *= Threshold;
                        break;
                    }
                case "DISTANCE_DEC_PAST":
                    {
                        Event = TriggerEvent.DistanceDecrease;
                        Threshold *= Threshold;
                        break;
                    }
                case "VARIABLE1_INC_PAST": Event = TriggerEvent.Variable1Increase; break;
                case "VARIABLE1_DEC_PAST": Event = TriggerEvent.Variable1Decrease; break;
                case "VARIABLE2_INC_PAST": Event = TriggerEvent.Variable2Increase; break;
                case "VARIABLE2_DEC_PAST": Event = TriggerEvent.Variable2Decrease; break;
                case "VARIABLE3_INC_PAST": Event = TriggerEvent.Variable3Increase; break;
                case "VARIABLE3_DEC_PAST": Event = TriggerEvent.Variable3Decrease; break;
                case "BRAKECYL_INC_PAST": Event = TriggerEvent.BrakeCylinderIncrease; break;
                case "BRAKECYL_DEC_PAST": Event = TriggerEvent.BrakeCylinderDecrease; break;
                case "CURVEFORCE_INC_PAST": Event = TriggerEvent.CurveForceIncrease; break;
                case "CURVEFORCE_DEC_PAST": Event = TriggerEvent.CurveForceDecrease; break;
            }

            while (!stf.EndOfBlock())
                ParsePlayCommand(stf, stf.ReadString());
        }
    }

    public class DistanceTravelledTrigger : Trigger
    {
        public float MinimumDistance { get; private set; } = 80;
        public float MaximumDistance { get; private set; } = 100;
        public float MinimumVolume { get; private set; } = 0.9f;
        public float MaximumVolume { get; private set; } = 1.0f;

        internal DistanceTravelledTrigger(STFReader stf)
        {
            stf.MustMatchBlockStart();
            while (!stf.EndOfBlock())
            {
                string token = stf.ReadString().ToUpperInvariant();
                switch (token)
                {
                    case "DIST_MIN_MAX": stf.MustMatchBlockStart(); MinimumDistance = stf.ReadFloat(STFReader.Units.Distance, null); MaximumDistance = stf.ReadFloat(STFReader.Units.Distance, null); stf.SkipRestOfBlock(); break;
                    case "VOLUME_MIN_MAX": stf.MustMatchBlockStart(); MinimumVolume = stf.ReadFloat(STFReader.Units.None, null); MaximumVolume = stf.ReadFloat(STFReader.Units.None, null); stf.SkipRestOfBlock(); break;
                    default: ParsePlayCommand(stf, token); break;
                }
            }
        }
    }

    public class RandomTrigger : Trigger
    {
        public float MinimumDelay { get; private set; } = 80;
        public float MaximumDelay { get; private set; } = 100;
        public float MinimumVolume { get; private set; } = 0.9f;
        public float MaximumVolume { get; private set; } = 1.0f;

        internal RandomTrigger(STFReader stf)
        {
            stf.MustMatchBlockStart();
            while (!stf.EndOfBlock())
            {
                string token = stf.ReadString().ToUpperInvariant();
                switch (token)
                {
                    case "DELAY_MIN_MAX": stf.MustMatchBlockStart(); MinimumDelay = stf.ReadFloat(STFReader.Units.None, null); MaximumDelay = stf.ReadFloat(STFReader.Units.None, null); stf.SkipRestOfBlock(); break;
                    case "VOLUME_MIN_MAX": stf.MustMatchBlockStart(); MinimumVolume = stf.ReadFloat(STFReader.Units.None, null); MaximumVolume = stf.ReadFloat(STFReader.Units.None, null); stf.SkipRestOfBlock(); break;
                    default: ParsePlayCommand(stf, token); break;
                }
            }
        }
    }

    public abstract class SoundCommand
    {
    }

    public class StreamVolumeCommand : SoundCommand
    {
        public float Volume { get; private set; }

        internal StreamVolumeCommand(STFReader stf)
        {
            stf.MustMatchBlockStart();
            Volume = stf.ReadFloat(STFReader.Units.None, null);
            stf.SkipRestOfBlock();
        }
    }

    public class TriggerCommand : SoundCommand
    {
        public enum TriggerType
        {
            Enable,
            Disable,
        }

        public int TriggerId { get; private set; }

        public TriggerType Trigger { get; private set; }

        internal TriggerCommand(STFReader stf, TriggerType trigger)
        {
            Trigger = trigger;
            stf.MustMatchBlockStart();
            TriggerId = stf.ReadInt(null);
            stf.SkipRestOfBlock();
        }
    }

    public class LoopRelease : SoundCommand
    {
        public ReleaseType ReleaseMode { get; private set; }

        public enum ReleaseType
        {
            Release,
            ReleaseWithJump,
        }

        internal LoopRelease(STFReader stf, ReleaseType mode)
        {
            ReleaseMode = mode;
            stf.MustMatchBlockStart();
            stf.SkipRestOfBlock();
        }
    }

    public class SoundPlayCommand : SoundCommand
    {
        public enum Selection
        {
            Random,
            Sequential,
        };

        public enum SoundCommandType
        {
            PlayOneShot,
            StartLoop,
            StartLoopRelease,
        }

#pragma warning disable CA1002 // Do not expose generic lists
        public List<string> Files { get; }
#pragma warning restore CA1002 // Do not expose generic lists
        public Selection SelectionMethod { get; protected set; } = Selection.Sequential;

        public SoundCommandType CommandType { get; private set; }

        internal SoundPlayCommand(STFReader stf, SoundCommandType commandType)
        {
            CommandType = commandType;
            stf.MustMatchBlockStart();
            int count = stf.ReadInt(null);
            Files = new string[count].ToList();
            int fileIndex = 0;
            while (!stf.EndOfBlock())
                switch (stf.ReadString().ToUpperInvariant())
                {
                    case "FILE":
                        if (fileIndex < count)
                        {
                            stf.MustMatchBlockStart();
                            Files[fileIndex++] = stf.ReadString();
                            stf.ReadInt(null);
                            stf.SkipRestOfBlock();
                        }
                        else  // MSTS skips extra files
                        {
                            STFException.TraceWarning(stf, "Skipped extra File");
                            stf.SkipBlock();
                        }
                        break;
                    case "SELECTIONMETHOD":
                        stf.MustMatchBlockStart();
                        string s = stf.ReadString();
                        switch (s.ToUpperInvariant())
                        {
                            case "RANDOMSELECTION": SelectionMethod = Selection.Random; break;
                            case "SEQUENTIALSELECTION": SelectionMethod = Selection.Sequential; break;
                            default: STFException.TraceWarning(stf, "Skipped unknown selection method " + s); break;
                        }
                        stf.SkipRestOfBlock();
                        break;
                    case "(": stf.SkipRestOfBlock(); break;
                }
        }
    }

}
