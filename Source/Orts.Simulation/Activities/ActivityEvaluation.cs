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

        }

        public static void Restore(BinaryReader inputStream)
        {
            if (null == inputStream)
                throw new ArgumentNullException(nameof(inputStream));
            instance = new ActivityEvaluation
            {
                CouplerBreaks = inputStream.ReadInt32(),
                TravellingTooFast = inputStream.ReadInt32(),
                TrainOverTurned = inputStream.ReadInt32(),
                SnappedBrakeHose = inputStream.ReadInt32(),
            };
        }
    }
}
