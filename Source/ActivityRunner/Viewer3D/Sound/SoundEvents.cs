﻿using Orts.Common;

namespace Orts.ActivityRunner.Viewer3D.Sound
{
    public enum SoundEventSource
    {
        None,
        Car,
        Crossing,
        FuelTower,
        InGame,
        Signal,
        Turntable,
        ContainerCrane,
    }

    public static class SoundEvent
    {

        // PLEASE DO NOT EDIT THESE FUNCTIONS without references and testing!
        // These numbers are the MSTS sound triggers and must match
        // MSTS/MSTSBin behaviour whenever possible. NEVER return values for
        // non-MSTS events when passed an MSTS Source.

        public static TrainEvent From(SoundEventSource source, int eventID)
        {
            switch (source)
            {
                case SoundEventSource.Car:
                    switch (eventID)
                    {
                        // Calculated from inspection of existing engine .sms files and extensive testing.
                        // Event 1 is unused in MSTS.
                        case 2: return TrainEvent.DynamicBrakeIncrease;
                        case 3: return TrainEvent.DynamicBrakeOff;
                        case 4: return TrainEvent.SanderOn;
                        case 5: return TrainEvent.SanderOff;
                        case 6: return TrainEvent.WiperOn;
                        case 7: return TrainEvent.WiperOff;
                        case 8: return TrainEvent.HornOn;
                        case 9: return TrainEvent.HornOff;
                        case 10: return TrainEvent.BellOn;
                        case 11: return TrainEvent.BellOff;
                        case 12: return TrainEvent.CompressorOn;
                        case 13: return TrainEvent.CompressorOff;
                        case 14: return TrainEvent.TrainBrakePressureIncrease;
                        case 15: return TrainEvent.ReverserChange;
                        case 16: return TrainEvent.ThrottleChange;
                        case 17: return TrainEvent.TrainBrakeChange; // Event 17 only works first time in MSTS.
                        case 18: return TrainEvent.EngineBrakeChange; // Event 18 only works first time in MSTS; MSTSBin fixes this.
                        // Event 19 is unused in MSTS.
                        case 20: return TrainEvent.DynamicBrakeChange;
                        case 21: return TrainEvent.EngineBrakePressureIncrease; // Event 21 is defined in sound files but never used in MSTS.
                        case 22: return TrainEvent.EngineBrakePressureDecrease; // Event 22 is defined in sound files but never used in MSTS.
                                                                                // Event 23 is unused in MSTS.
                                                                                // Event 24 is unused in MSTS.
                                                                                // MSTSBin codes (documented at http://mstsbin.uktrainsim.com/)
                        case 23: return TrainEvent.EnginePowerOn;
                        case 24: return TrainEvent.EnginePowerOff;
                        // Event 25 is possibly a vigilance reset in MSTS sound files but is never used.
                        // Event 26 is a sander toggle in MSTS sound files but is never used.
                        case 27: return TrainEvent.WaterInjector2On;
                        case 28: return TrainEvent.WaterInjector2Off;
                        // Event 29 is unused in MSTS.
                        case 30: return TrainEvent.WaterInjector1On;
                        case 31: return TrainEvent.WaterInjector1Off;
                        case 32: return TrainEvent.DamperChange;
                        case 33: return TrainEvent.BlowerChange;
                        case 34: return TrainEvent.CylinderCocksToggle;
                        // Event 35 is unused in MSTS.
                        case 36: return TrainEvent.FireboxDoorChange;
                        case 37: return TrainEvent.LightSwitchToggle;
                        case 38: return TrainEvent.WaterScoopDown;
                        case 39: return TrainEvent.WaterScoopUp;
                        case 40: return TrainEvent.FireboxDoorOpen; // Used in default steam locomotives (Scotsman and 380)
                        case 41: return TrainEvent.FireboxDoorClose;
                        case 42: return TrainEvent.SteamSafetyValveOn;
                        case 43: return TrainEvent.SteamSafetyValveOff;
                        case 44: return TrainEvent.SteamHeatChange; // Event 44 only works first time in MSTS.
                        case 45: return TrainEvent.Pantograph1Up;
                        case 46: return TrainEvent.Pantograph1Down;
                        case 47: return TrainEvent.PantographToggle;
                        case 48: return TrainEvent.VigilanceAlarmReset;
                        // Event 49 is unused in MSTS.
                        // Event 50 is unused in MSTS.
                        // Event 51 is an engine brake of some kind in MSTS sound files but is never used.
                        // Event 52 is unused in MSTS.
                        // Event 53 is a train brake normal apply in MSTS sound files but is never used.
                        case 54: return TrainEvent.TrainBrakePressureDecrease; // Event 54 is a train brake emergency apply in MSTS sound files but is actually a train brake pressure decrease.
                        // Event 55 is unused in MSTS.
                        case 56: return TrainEvent.VigilanceAlarmOn;
                        case 57: return TrainEvent.VigilanceAlarmOff; // Event 57 is triggered constantly in MSTS when the vigilance alarm is off.
                        case 58: return TrainEvent.Couple;
                        case 59: return TrainEvent.CoupleB;
                        case 60: return TrainEvent.CoupleC;
                        case 61: return TrainEvent.Uncouple;
                        case 62: return TrainEvent.UncoupleB;
                        case 63: return TrainEvent.UncoupleC;
                        // Event 64 is unused in MSTS.
                        // MSTSBin codes (documented at http://mstsbin.uktrainsim.com/)
                        case 66: return TrainEvent.Pantograph2Up;
                        case 67: return TrainEvent.Pantograph2Down;
                        // ORTS only Events
                        case 101: return TrainEvent.GearUp; // for gearbox based engines
                        case 102: return TrainEvent.GearDown; // for gearbox based engines
                        case 103: return TrainEvent.ReverserToForwardBackward; // reverser moved to forward or backward position
                        case 104: return TrainEvent.ReverserToNeutral; // reversed moved to neutral
                        case 105: return TrainEvent.DoorOpen; // door opened; propagated to all locos and wagons of the consist
                        case 106: return TrainEvent.DoorClose; // door closed; propagated to all locos and wagons of the consist
                        case 107: return TrainEvent.MirrorOpen;
                        case 108: return TrainEvent.MirrorClose;
                        case 109: return TrainEvent.TrainControlSystemInfo1;
                        case 110: return TrainEvent.TrainControlSystemInfo2;
                        case 111: return TrainEvent.TrainControlSystemActivate;
                        case 112: return TrainEvent.TrainControlSystemDeactivate;
                        case 113: return TrainEvent.TrainControlSystemPenalty1;
                        case 114: return TrainEvent.TrainControlSystemPenalty2;
                        case 115: return TrainEvent.TrainControlSystemWarning1;
                        case 116: return TrainEvent.TrainControlSystemWarning2;
                        case 117: return TrainEvent.TrainControlSystemAlert1;
                        case 118: return TrainEvent.TrainControlSystemAlert2;
                        case 119: return TrainEvent.CylinderCompoundToggle; // Locomotive switched to compound

                        case 120: return TrainEvent.BlowdownValveToggle;
                        case 121: return TrainEvent.SteamPulse1;
                        case 122: return TrainEvent.SteamPulse2;
                        case 123: return TrainEvent.SteamPulse3;
                        case 124: return TrainEvent.SteamPulse4;
                        case 125: return TrainEvent.SteamPulse5;
                        case 126: return TrainEvent.SteamPulse6;
                        case 127: return TrainEvent.SteamPulse7;
                        case 128: return TrainEvent.SteamPulse8;
                        case 129: return TrainEvent.SteamPulse9;
                        case 130: return TrainEvent.SteamPulse10;
                        case 131: return TrainEvent.SteamPulse11;
                        case 132: return TrainEvent.SteamPulse12;
                        case 133: return TrainEvent.SteamPulse13;
                        case 134: return TrainEvent.SteamPulse14;
                        case 135: return TrainEvent.SteamPulse15;
                        case 136: return TrainEvent.SteamPulse16;

                        case 137: return TrainEvent.CylinderCocksOpen;
                        case 138: return TrainEvent.CylinderCocksClose;
                        case 139: return TrainEvent.TrainBrakePressureStoppedChanging;
                        case 140: return TrainEvent.EngineBrakePressureStoppedChanging;
                        case 141: return TrainEvent.BrakePipePressureIncrease;
                        case 142: return TrainEvent.BrakePipePressureDecrease;
                        case 143: return TrainEvent.BrakePipePressureStoppedChanging;

                        case 145: return TrainEvent.WaterScoopRaiseLower;
                        case 146: return TrainEvent.WaterScoopBroken;

                        case 147: return TrainEvent.SteamGearLeverToggle;
                        case 148: return TrainEvent.AIFiremanSoundOn;
                        case 149: return TrainEvent.AIFiremanSoundOff;

                        case 150: return TrainEvent.CircuitBreakerOpen;
                        case 151: return TrainEvent.CircuitBreakerClosing;
                        case 152: return TrainEvent.CircuitBreakerClosed;
                        case 153: return TrainEvent.CircuitBreakerClosingOrderOn;
                        case 154: return TrainEvent.CircuitBreakerClosingOrderOff;
                        case 155: return TrainEvent.CircuitBreakerOpeningOrderOn;
                        case 156: return TrainEvent.CircuitBreakerOpeningOrderOff;
                        case 157: return TrainEvent.CircuitBreakerClosingAuthorizationOn;
                        case 158: return TrainEvent.CircuitBreakerClosingAuthorizationOff;

                        case 159: return TrainEvent.LargeEjectorChange;
                        case 160: return TrainEvent.SmallEjectorChange;

                        case 161: return TrainEvent.CabLightSwitchToggle;
                        case 162: return TrainEvent.CabRadioOn;
                        case 163: return TrainEvent.CabRadioOff;

                        case 164: return TrainEvent.BrakesStuck;

                        case 165: return TrainEvent.VacuumExhausterOn;
                        case 166: return TrainEvent.VacuumExhausterOff;
                        case 167: return TrainEvent.SecondEnginePowerOn;
                        case 168: return TrainEvent.SecondEnginePowerOff;

                        case 169: return TrainEvent.Pantograph3Up;
                        case 170: return TrainEvent.Pantograph3Down;
                        case 171: return TrainEvent.Pantograph4Up;
                        case 172: return TrainEvent.Pantograph4Down;

                        case 173: return TrainEvent.HotBoxBearingOn;
                        case 174: return TrainEvent.HotBoxBearingOff;

                        case 175: return TrainEvent.BoilerBlowdownOn;
                        case 176: return TrainEvent.BoilerBlowdownOff;

                        case 181: return TrainEvent.GenericEvent1;
                        case 182: return TrainEvent.GenericEvent2;
                        case 183: return TrainEvent.GenericEvent3;
                        case 184: return TrainEvent.GenericEvent4;
                        case 185: return TrainEvent.GenericEvent5;
                        case 186: return TrainEvent.GenericEvent6;
                        case 187: return TrainEvent.GenericEvent7;
                        case 188: return TrainEvent.GenericEvent8;

                        case 189: return TrainEvent.BatterySwitchOn;
                        case 190: return TrainEvent.BatterySwitchOff;
                        case 191: return TrainEvent.BatterySwitchCommandOn;
                        case 192: return TrainEvent.BatterySwitchCommandOff;

                        case 193: return TrainEvent.MasterKeyOn;
                        case 194: return TrainEvent.MasterKeyOff;

                        case 195: return TrainEvent.ServiceRetentionButtonOn;
                        case 196: return TrainEvent.ServiceRetentionButtonOff;
                        case 197: return TrainEvent.ServiceRetentionCancellationButtonOn;
                        case 198: return TrainEvent.ServiceRetentionCancellationButtonOff;

                        case 200: return TrainEvent.GearPosition0;
                        case 201: return TrainEvent.GearPosition1;
                        case 202: return TrainEvent.GearPosition2;
                        case 203: return TrainEvent.GearPosition3;
                        case 204: return TrainEvent.GearPosition4;
                        case 205: return TrainEvent.GearPosition5;
                        case 206: return TrainEvent.GearPosition6;
                        case 207: return TrainEvent.GearPosition7;
                        case 208: return TrainEvent.GearPosition8;

                        case 210: return TrainEvent.LargeEjectorOn;
                        case 211: return TrainEvent.LargeEjectorOff;
                        case 212: return TrainEvent.SmallEjectorOn;
                        case 213: return TrainEvent.SmallEjectorOff;

                        case 214: return TrainEvent.TractionCutOffRelayOpen;
                        case 215: return TrainEvent.TractionCutOffRelayClosing;
                        case 216: return TrainEvent.TractionCutOffRelayClosed;
                        case 217: return TrainEvent.TractionCutOffRelayClosingOrderOn;
                        case 218: return TrainEvent.TractionCutOffRelayClosingOrderOff;
                        case 219: return TrainEvent.TractionCutOffRelayOpeningOrderOn;
                        case 220: return TrainEvent.TractionCutOffRelayOpeningOrderOff;
                        case 221: return TrainEvent.TractionCutOffRelayClosingAuthorizationOn;
                        case 222: return TrainEvent.TractionCutOffRelayClosingAuthorizationOff;

                        case 223: return TrainEvent.ElectricTrainSupplyOn;
                        case 224: return TrainEvent.ElectricTrainSupplyOff;
                        case 225: return TrainEvent.ElectricTrainSupplyCommandOn;
                        case 226: return TrainEvent.ElectricTrainSupplyCommandOff;

                        case 227: return TrainEvent.PowerConverterOn;
                        case 228: return TrainEvent.PowerConverterOff;
                        case 229: return TrainEvent.VentilationHigh;
                        case 230: return TrainEvent.VentilationLow;
                        case 231: return TrainEvent.VentilationOff;
                        case 232: return TrainEvent.HeatingOn;
                        case 233: return TrainEvent.HeatingOff;
                        case 234: return TrainEvent.AirConditioningOn;
                        case 235: return TrainEvent.AirConditioningOff;

                        case 240: return TrainEvent.GenericItem1On;
                        case 241: return TrainEvent.GenericItem1Off;
                        case 242: return TrainEvent.GenericItem2On;
                        case 243: return TrainEvent.GenericItem2Off;

                        case 250: return TrainEvent.OverchargeBrakingOn;
                        case 251: return TrainEvent.OverchargeBrakingOff;

                        //
                        default: return TrainEvent.None;
                    }
                case SoundEventSource.Crossing:
                    switch (eventID)
                    {
                        // Calculated from inspection of existing crossing.sms files.
                        case 3: return TrainEvent.CrossingClosing;
                        case 4: return TrainEvent.CrossingOpening;
                        default: return TrainEvent.None;
                    }
                case SoundEventSource.FuelTower:
                    switch (eventID)
                    {
                        // Calculated from inspection of existing *tower.sms files.
                        case 6: return TrainEvent.FuelTowerDown;
                        case 7: return TrainEvent.FuelTowerUp;
                        case 9: return TrainEvent.FuelTowerTransferStart;
                        case 10: return TrainEvent.FuelTowerTransferEnd;
                        default: return TrainEvent.None;
                    }
                case SoundEventSource.InGame:
                    switch (eventID)
                    {
                        // Calculated from inspection of existing ingame.sms files.
                        case 10: return TrainEvent.ControlError;
                        case 20: return TrainEvent.Derail1;
                        case 21: return TrainEvent.Derail2;
                        case 22: return TrainEvent.Derail3;
                        case 25: return 0; // TODO: What is this event?
                        case 60: return TrainEvent.PermissionToDepart;
                        case 61: return TrainEvent.PermissionGranted;
                        case 62: return TrainEvent.PermissionDenied;
                        default: return TrainEvent.None;
                    }
                case SoundEventSource.Signal:
                    switch (eventID)
                    {
                        // Calculated from inspection of existing signal.sms files.
                        case 1: return TrainEvent.SemaphoreArm;
                        default: return TrainEvent.None;
                    }
                case SoundEventSource.Turntable:
                    switch (eventID)
                    {
                        // related file is turntable.sms
                        case 1: return TrainEvent.MovingTableMovingEmpty;
                        case 2: return TrainEvent.MovingTableMovingLoaded;
                        case 3: return TrainEvent.MovingTableStopped;
                        default: return TrainEvent.None;
                    }
                case SoundEventSource.ContainerCrane:
                    switch (eventID)
                    {
                        // Can be different from crane to crane
                        case 1:
                            return TrainEvent.CraneXAxisMove;
                        case 2:
                            return TrainEvent.CraneXAxisSlowDown;
                        case 3:
                            return TrainEvent.CraneYAxisMove;
                        case 4:
                            return TrainEvent.CraneYAxisSlowDown;
                        case 5:
                            return TrainEvent.CraneZAxisMove;
                        case 6:
                            return TrainEvent.CraneZAxisSlowDown;
                        case 7:
                            return TrainEvent.CraneYAxisDown;
                        default:
                            return 0;
                    }
                default: return TrainEvent.None;
            }
        }
    }
}
