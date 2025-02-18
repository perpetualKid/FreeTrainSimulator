namespace FreeTrainSimulator.Common.Position
{
    /// <summary>
    /// An item which has a location on a (2D) map.
    /// Location can be given <see cref="PointD"/> or <see cref="WorldLocation"/>
    /// Implements <see cref="ITileCoordinate{T}"/> to allow 2D-indexing with <see cref="TileIndexedList{TTileCoordinate, T}"/>
    /// </summary>
    public abstract record CoordinatePrimitiveBase
    {
        /// <summary>
        /// Diameter for Point Primitives
        /// Line Width for Vector Primitives
        /// 1.0 equals 1 metre
        /// </summary>
        public float Size { get; protected set; }
    }

    public abstract record PointPrimitive : CoordinatePrimitiveBase, ITileCoordinate
    {
        protected const double ProximityTolerance = 1.0; //allow for a 1m proximity error (rounding, placement) when trying to locate points/locations along a track segment

        private PointD location;

        private Tile tile;

        public ref readonly Tile Tile => ref tile;
        public ref readonly PointD Location => ref location;

        public virtual double DistanceSquared(in PointD point) => location.DistanceSquared(point);

        protected PointPrimitive()
        { }

        protected PointPrimitive(in PointD location)
        {
            SetLocation(location);
        }

        protected PointPrimitive(in WorldLocation location)
        {
            SetLocation(location);
        }

        protected void SetLocation(in PointD location)
        {
            tile = PointD.ToTile(location);
            this.location = location;
        }

        protected void SetLocation(in WorldLocation location)
        {
            tile = location.Tile;
            this.location = PointD.FromWorldLocation(location);
        }
    }

    public abstract record VectorPrimitive : PointPrimitive, ITileCoordinateVector
    {
        private PointD vectorEnd;

        private Tile otherTile;

        public ref readonly PointD Vector => ref vectorEnd;

        public ref readonly Tile OtherTile => ref otherTile;

        /// <summary>
        /// Squared distance of the given point on the straight line vector or on the arc (curved line)
        /// Points which are not betwween the start and end point, are considered to return NaN.
        /// For this, implementations will mostly allow for a small rounding tolerance (up to 1m) <seealso cref="ProximityTolerance"/>
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public abstract override double DistanceSquared(in PointD point);

        protected VectorPrimitive()
        { }

        protected VectorPrimitive(in PointD start, in PointD end) : base(start)
        {
            SetVector(start, end);
        }

        protected VectorPrimitive(in WorldLocation start, in WorldLocation end) : base(start)
        {
            SetVector(end);
        }

        protected void SetVector(in PointD end)
        {
            otherTile = PointD.ToTile(end);
            vectorEnd = end;
        }

        protected void SetVector(in PointD start, in PointD end)
        {
            SetLocation(start);
            SetVector(end);
        }

        protected void SetVector(in WorldLocation end)
        {
            otherTile = end.Tile;
            vectorEnd = PointD.FromWorldLocation(end);
        }

        protected void SetVector(in WorldLocation start, in WorldLocation end)
        {
            SetLocation(start);
            SetVector(end);
        }
    }
}
