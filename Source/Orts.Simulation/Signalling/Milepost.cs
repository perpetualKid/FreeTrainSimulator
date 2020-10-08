namespace Orts.Simulation.Signalling
{
    //================================================================================================//
    /// <summary>
    ///
    /// Milepost Object
    ///
    /// </summary>
    //================================================================================================//
    public class Milepost
    {
        public uint TrackItemId { get; set; }
        public int TrackCircuitReference { get; set; } = -1;
        public float TrackCircuitOffset { get; set; }
        public float MilepostValue { get; set; }

        public Milepost(uint trItemId)
        {
            TrackItemId = trItemId;
        }

        //================================================================================================//
        /// <summary>
        /// Dummy constructor
        /// </summary>

        public Milepost()
        {
        }
    }

}
