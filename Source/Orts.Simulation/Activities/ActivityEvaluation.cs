using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        private static ActivityEvaluation instance;

        public int Version { get; private set; }

        private ActivityEvaluation()
        { }

        public static ActivityEvaluation Instance
        {
            get
            {
                if (null == instance && Simulator.Instance.Settings.ActivityEvalulation)
                    instance = new ActivityEvaluation();
                return instance;
            }
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
            };
        }
    }
}
