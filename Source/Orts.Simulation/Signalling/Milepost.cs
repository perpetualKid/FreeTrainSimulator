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
        public uint TrackItemId { get; }
        public int TrackCircuitReference { get; private set; } = -1;
        public float TrackCircuitOffset { get; private set; }
        public float Value { get; }

        public Milepost(uint trItemId, float value)
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
