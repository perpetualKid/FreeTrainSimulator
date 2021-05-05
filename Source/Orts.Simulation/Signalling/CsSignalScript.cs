using System;
using System.Collections.Generic;
using System.Linq;

using Orts.Common;
using Orts.Formats.Msts;

namespace Orts.Simulation.Signalling
{
    // The C# signal script is supposed to be used on routes where all signals are using C# scripts.
    // The exchange of information is done through the TextSignalAspect property.
    // The MSTS signal aspect is only used for TCS scripts that do not support TextSignalAspect.
    // The MSTS SIGSCR functions are still usable through the SignalObject and SignalHead objects.
    public abstract class CsSignalScript
    {
        // References
        public SignalHead SignalHead { get; set; }
        public Signal SignalObject => SignalHead.MainSignal;

        // Aliases
        public SignalAspectState MstsSignalAspect { get => SignalHead.SignalIndicationState; protected set => SignalHead.SignalIndicationState = value; }
        public string TextSignalAspect { get => SignalHead.TextSignalAspect; protected set => SignalHead.TextSignalAspect = value; }
        public int DrawState { get => SignalHead.DrawState; protected set => SignalHead.DrawState = value; }
        public bool Enabled => SignalObject.Enabled;
        public float? ApproachControlRequiredPosition => SignalHead.ApproachControlLimitPositionM.Value;
        public float? ApproachControlRequiredSpeed => SignalHead.ApproachControlLimitSpeedMpS.Value;
        public SignalBlockState BlockState => SignalObject.BlockState();
        public bool RouteSet => SignalHead.VerifyRouteSet() > 0;
        public int DefaultDrawState(SignalAspectState signalAspect)
        {
            return SignalHead.DefaultDrawState(signalAspect);
        }

        protected CsSignalScript()
        {
        }

        public abstract void Initialize();

        public abstract void Update();

        public Signal NextSignal(SignalFunction signalFunction)
        {
            return NextSignals(signalFunction, 1).FirstOrDefault();
        }

        public IReadOnlyCollection<Signal> NextSignals(SignalFunction signalFunction, uint number)
        {
            // Sanity check
            if (number > 20)
            {
                number = 20;
            }

            List<Signal> signalObjects = new List<Signal>();
            Signal nextSignalObject = SignalHead.MainSignal;

            while (signalObjects.Count < number)
            {
                int nextSignal = nextSignalObject.NextSignalId((int)signalFunction);

                // signal found : get state
                if (nextSignal >= 0)
                {
                    nextSignalObject = Simulator.Instance.SignalEnvironment.Signals[nextSignal];
                    signalObjects.Add(nextSignalObject);
                }
                else
                {
                    break;
                }
            }

            return signalObjects;
        }

        public IEnumerable<string> GetThisSignalTextAspects(SignalFunction signalFunction)
        {
            return SignalHead.MainSignal.GetAllTextSignalAspects(signalFunction);
        }

        public IEnumerable<string> GetNextSignalTextAspects(SignalFunction signalFunction)
        {
            Signal nextSignal = NextSignal(signalFunction);
            return nextSignal?.GetAllTextSignalAspects(signalFunction) ?? Enumerable.Empty<string>();
        }

        public bool IsSignalFeatureEnabled(string signalFeature)
        {
            if (!EnumExtension.GetValue(signalFeature, out SignalSubType subType))
                subType = SignalSubType.None;
            return SignalHead.VerifySignalFeature((int)subType);
        }
    }
}
