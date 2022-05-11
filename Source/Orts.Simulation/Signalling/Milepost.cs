namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    /// <summary>
    ///
    /// Milepost Object
    ///
    /// </summary>
    //================================================================================================//
    internal class Milepost
    {
        public int TrackItemId { get; }
        public int TrackCircuitReference { get; private set; } = -1;
        public float TrackCircuitOffset { get; private set; }
        public float Value { get; }

        public Milepost(int trItemId, float value)
        {
            TrackItemId = trItemId;
            Value = value;
        }

        internal void SetCircuit(int trackCircuitReference, float trackCircuitOffset)
        {
            TrackCircuitReference = trackCircuitReference;
            TrackCircuitOffset = trackCircuitOffset;
        }
    }

}
