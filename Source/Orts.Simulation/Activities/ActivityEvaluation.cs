using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Orts.Simulation.Physics;

using static Orts.Common.Calc.Dynamics;

namespace Orts.Simulation.Activities
{
    public class ActivityEvaluation
    {
        private int couplerBreaks;
        private int travellingTooFast;
        private int trainOverTurned;
        private int snappedBrakeHose;
        private double distanceTravelled;
        private int fullTrainBrakeUnder8kmh;
        private int fullBrakeAbove16kmh;
        private int overSpeedCoupling;
        private int emergencyButtonStopped;
        private int emergencyButtonMoving;
        private double autoPilotInitialTime;
        private double autoPilotTime;
        private bool autoPilotTimerRunning;
        private bool overSpeedRunning;
        private int overSpeed;
        private double overSpeedInitialTime;
        private double overSpeedTime;
        private int departBeforeBoarding;

        private static ActivityEvaluation instance;

        public long Version { get; private set; }

        private ActivityEvaluation()
        { }

        public static ActivityEvaluation Instance
        {
            get
            {
                if (null == instance)
                    instance = new ActivityEvaluation();
                return instance;
            }
        }

        public void Update()
        {
            bool overSpeedNow = Math.Abs(Simulator.Instance.PlayerLocomotive.Train.SpeedMpS) > 
                Math.Min(Simulator.Instance.PlayerLocomotive.Train.AllowedMaxSpeedMpS, Simulator.Instance.PlayerLocomotive.Train.TrainMaxSpeedMpS) + 1.67; //3.8MpH/6.kmh

            if (overSpeedNow && !overSpeedRunning)//Debrief Eval
            {
                overSpeedRunning = true;
                overSpeedInitialTime = Simulator.Instance.ClockTime;
            }
            else if (overSpeedRunning && !overSpeedNow)//Debrief Eval
            {
                overSpeedRunning = false;
                overSpeedTime += Simulator.Instance.ClockTime - overSpeedInitialTime;
                overSpeed++;
            }
            if (overSpeedRunning || autoPilotTimerRunning)
                Version++;

        }

        public int CouplerBreaks
        {
            get => couplerBreaks;
            set
            {
                couplerBreaks++;
                Version++;
                Trace.WriteLine($"Num of coupler breaks: {couplerBreaks}");
            }
        }

        public int TravellingTooFast
        {
            get => travellingTooFast;
            set
            {
                travellingTooFast = value;
                Version++;
            }
        }

        public int TrainOverTurned
        {
            get => trainOverTurned;
            set
            {
                trainOverTurned = value;
                Version++;
            }
        }

        public int SnappedBrakeHose
        {
            get => snappedBrakeHose;
            set
            {
                snappedBrakeHose = value;
                Version++;
            }
        }

        public double DistanceTravelled
        {
            get => distanceTravelled + Simulator.Instance.PlayerLocomotive.DistanceTravelled;
            set
            {
                distanceTravelled = value;
                Version++;
            }
        }

        public int FullTrainBrakeUnder8kmh
        {
            get => fullTrainBrakeUnder8kmh;
            set
            {
                fullTrainBrakeUnder8kmh = value;
                Version++;
            }
        }

        public int FullBrakeAbove16kmh
        {
            get => fullBrakeAbove16kmh;
            set
            {
                fullBrakeAbove16kmh = value;
                Version++;
            }
        }

        public int OverSpeedCoupling
        {
            get => overSpeedCoupling;
            set
            {
                overSpeedCoupling = value;
                Version++;
            }
        }

        public int EmergencyButtonStopped
        {
            get => emergencyButtonStopped;
            set
            {
                emergencyButtonStopped = value;
                Version++;
            }
        }

        public int EmergencyButtonMoving
        {
            get => emergencyButtonMoving;
            set
            {
                emergencyButtonMoving = value;
                Version++;
            }
        }

        public void StartAutoPilotTime()
        {
            if (!autoPilotTimerRunning)
                autoPilotInitialTime = Simulator.Instance.ClockTime;
            autoPilotTimerRunning = true;
        }

        public void StopAutoPilotTime()
        {
            if (autoPilotTimerRunning)
                autoPilotTime += Simulator.Instance.ClockTime - autoPilotInitialTime;
            autoPilotTimerRunning = false;
        }

        public double AutoPilotTime
        {
            get => autoPilotTimerRunning ? Simulator.Instance.ClockTime - autoPilotInitialTime + autoPilotTime : autoPilotTime;
        }

        public int OverSpeed
        {
            get => overSpeed;
        }

        public double OverSpeedTime
        {
            get => overSpeedRunning ? Simulator.Instance.ClockTime - overSpeedInitialTime + overSpeedTime : overSpeedTime;
        }

        public int DepartBeforeBoarding
        {
            get => departBeforeBoarding;
            set 
            {
                departBeforeBoarding = value;
                Version++;
            }
        }
        public static void Save(BinaryWriter outputStream)
        {
            if (null == outputStream)
                throw new ArgumentNullException(nameof(outputStream));
            if (instance == null)
                return;

            outputStream.Write(instance.CouplerBreaks);
            outputStream.Write(instance.TravellingTooFast);
            outputStream.Write(instance.TrainOverTurned);
            outputStream.Write(instance.SnappedBrakeHose);
            outputStream.Write(instance.DistanceTravelled);
            outputStream.Write(instance.FullTrainBrakeUnder8kmh);
            outputStream.Write(instance.FullBrakeAbove16kmh);
            outputStream.Write(instance.OverSpeedCoupling);
            outputStream.Write(instance.EmergencyButtonStopped);
            outputStream.Write(instance.EmergencyButtonMoving);
            outputStream.Write(instance.autoPilotInitialTime);
            outputStream.Write(instance.AutoPilotTime);
            outputStream.Write(instance.OverSpeed);
            outputStream.Write(instance.overSpeedInitialTime);
            outputStream.Write(instance.OverSpeedTime);
            outputStream.Write(instance.DepartBeforeBoarding);
        }

        public static void Restore(BinaryReader inputStream)
        {
            if (null == inputStream)
                throw new ArgumentNullException(nameof(inputStream));
            instance = new ActivityEvaluation
            {
                couplerBreaks = inputStream.ReadInt32(),
                travellingTooFast = inputStream.ReadInt32(),
                trainOverTurned = inputStream.ReadInt32(),
                snappedBrakeHose = inputStream.ReadInt32(),
                distanceTravelled = inputStream.ReadDouble(),
                fullTrainBrakeUnder8kmh = inputStream.ReadInt32(),
                fullBrakeAbove16kmh = inputStream.ReadInt32(),
                overSpeedCoupling = inputStream.ReadInt32(),
                emergencyButtonStopped = inputStream.ReadInt32(),
                emergencyButtonMoving = inputStream.ReadInt32(),
                autoPilotInitialTime = inputStream.ReadDouble(),
                autoPilotTime = inputStream.ReadDouble(),
                overSpeed = inputStream.ReadInt32(),
                overSpeedInitialTime = inputStream.ReadDouble(),
                overSpeedTime = inputStream.ReadDouble(),
                departBeforeBoarding = inputStream.ReadInt32(),
            };
        }
    }
}
