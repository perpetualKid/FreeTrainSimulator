// COPYRIGHT 2012, 2013 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Models;

namespace Orts.Formats.Msts
{
    /// <summary>
    /// A traveller that represents a specific location and direction on a track node database. 
    /// Think of it like a virtual truck or bogie that can travel along the track or a virtual car that can travel along the road.
    /// </summary>
    public class Traveller: ISaveStateApi<TravellerSaveState>
    {
        private readonly TrackNodes trackNodes;
        private Direction direction = Direction.Forward;
        private float trackVectorSectionOffset; // Offset into track (vector) section; meters for straight sections, radians for curved sections.
        private TrackNode trackNode;
        private TrackVectorSection trackVectorSection;
        private TrackSection trackSection;

        // Location and directionVector are only valid if locationSet == true.
        private bool locationSet;
        private WorldLocation location = WorldLocation.None;
        private Vector3 directionVector;

        // Length and offset only valid if lengthSet = true.
        private bool lengthSet;
        private float trackNodeLength;
        private float trackNodeOffset;

        public ref readonly WorldLocation WorldLocation { get { if (!locationSet) SetLocation(); return ref location; } }
        public ref readonly Tile Tile { get { if (!locationSet) SetLocation(); return ref location.Tile; } }
        public Vector3 Location { get { if (!locationSet) SetLocation(); return location.Location; } }
        public float X { get { if (!locationSet) SetLocation(); return location.Location.X; } }
        public float Y { get { if (!locationSet) SetLocation(); return location.Location.Y; } }
        public float Z { get { if (!locationSet) SetLocation(); return location.Location.Z; } }
        public Direction Direction
        {
            get => direction;
            set
            {
                if (value != direction)
                {
                    direction = value;
                    if (locationSet)
                    {
                        directionVector.X *= -1;
                        directionVector.Y += MathHelper.Pi;
                        directionVector.X = MathHelper.WrapAngle(directionVector.X);
                        directionVector.Y = MathHelper.WrapAngle(directionVector.Y);
                    }
                    if (lengthSet)
                        trackNodeOffset = trackNodeLength - trackNodeOffset;
                }
            }
        }
        public float RotY { get { if (!locationSet) SetLocation(); return directionVector.Y; } }
        public TrackNode TrackNode => trackNode;

        /// <summary>
        /// Returns the index of the current track vector section (individual straight or curved section of track) in the current track node.
        /// </summary>
        public int TrackVectorSectionIndex { get; private set; }
        /// <summary>
        /// Returns the length of the current track node in meters.
        /// </summary>
        public float TrackNodeLength { get { if (!lengthSet) SetLength(); return trackNodeLength; } }
        /// <summary>
        /// Returns the distance down the current track node in meters, based on direction of travel.
        /// </summary>
        public float TrackNodeOffset { get { if (!lengthSet) SetLength(); return trackNodeOffset; } }
        /// <summary>
        /// Returns the distance down the current track vector section in meters, based on direction of travel.
        /// </summary>
        public float TrackSectionOffset { get { if (!lengthSet) SetLength(); return trackVectorSectionOffset; } }
        /// <summary>
        /// Returns whether this traveller is currently on a (section of) track node, junction, or end of line.
        /// </summary>
        public TrackNodeType TrackNodeType => this.TrackNode switch
        {
            TrackJunctionNode _ => TrackNodeType.Junction,
            TrackEndNode _ => TrackNodeType.End,
            TrackVectorNode _ => TrackNodeType.Track,
            _ => TrackNodeType.Track,
        };

        public Traveller(bool roadTrackTraveller)
        {
            if (null == RuntimeData.Instance)
                throw new InvalidOperationException("RuntimeData not initialized!");
            this.trackNodes = roadTrackTraveller ? RuntimeData.Instance.RoadTrackDB.TrackNodes : RuntimeData.Instance.TrackDB.TrackNodes;
        }

        /// <summary>
        /// Creates a traveller on the starting point of a path, in the direction of the path
        /// </summary>
        public Traveller(in WorldLocation firstNodeLocation, in WorldLocation nextMainLocation, bool roadTraveller = false)
            : this(firstNodeLocation, roadTraveller)
        {
            // get distance forward
            float fwdist = DistanceTo(nextMainLocation);

            // reverse train, get distance backward
            ReverseDirection();
            float bwdist = DistanceTo(nextMainLocation);

            // check which way exists or is shorter (in case of loop)
            // remember : train is now facing backward !

            if (bwdist < 0 || (fwdist > 0 && bwdist > fwdist)) // no path backward or backward path is longer
                ReverseDirection();
        }

        /// <summary>
        /// Creates a traveller starting at a specific location, facing with the track node.
        /// </summary>
        public Traveller(WorldLocation location, bool roadTraveller = false)
            : this(roadTraveller)
        {
            List<TrackNodeCandidate> candidates = new List<TrackNodeCandidate>();
            //first find all tracknodes that are close enough
            foreach (TrackNode node in this.trackNodes)
            {
                TrackNodeCandidate candidate = TrackNodeCandidate.TryTrackNode(node, location);
                if (candidate != null)
                {
                    candidates.Add(candidate);
                }
            }
            if (candidates.Count == 0)
            {
                throw new InvalidDataException($"{location} could not be found in the track database.");
            }

            // find the best one.
            TrackNodeCandidate bestCandidate = candidates.OrderBy(cand => cand.DistanceToTrack).First();

            InitFromCandidate(bestCandidate);

        }

        /// <summary>
        /// Creates a traveller starting at a specific location, facing in the specified direction.
        /// </summary>
        public Traveller(in WorldLocation location, Direction direction, bool roadTraveller = false)
            : this(location, roadTraveller)
        {
            Direction = direction;
        }

        /// <summary>
        /// Creates a traveller starting at the beginning of the specified track node, facing with the track node.
        /// </summary>
        public Traveller(TrackVectorNode startTrackNode, bool roadTraveller = false)
            : this(roadTraveller)
        {
            if (startTrackNode == null)
                throw new ArgumentException("Track node is not a vector node.", nameof(startTrackNode));
            if (startTrackNode.TrackVectorSections == null)
                throw new ArgumentException("Track node has no vector section data.", nameof(startTrackNode));
            if (startTrackNode.TrackVectorSections.Length == 0)
                throw new ArgumentException("Track node has no vector sections.", nameof(startTrackNode));
            TrackVectorSection tvs = startTrackNode.TrackVectorSections[0];
            if (!InitTrackNode(startTrackNode, tvs.Location))
            {
                if (TrackSections.MissingTrackSectionWarnings == 0)
                    throw new InvalidDataException($"Track node {startTrackNode.UiD} could not be found in the track database.");
                else
                {
                    throw new MissingTrackNodeException();
                }
            }
        }

        /// <summary>
        /// Creates a traveller starting at a specific location within a specified track node, facing with the track node.
        /// </summary>
        private Traveller(TrackVectorNode startTrackNode, in WorldLocation location, bool roadTraveller = false)
            : this(roadTraveller)
        {
            ArgumentNullException.ThrowIfNull(startTrackNode);
            if (!InitTrackNode(startTrackNode, location))
            {
                if (startTrackNode.TrackVectorSections == null)
                    throw new ArgumentException("Track node has no vector section data.", nameof(startTrackNode));
                if (startTrackNode.TrackVectorSections.Length == 0)
                    throw new ArgumentException("Track node has no vector sections.", nameof(startTrackNode));
                TrackVectorSection tvs = startTrackNode.TrackVectorSections[0];
                if (!InitTrackNode(startTrackNode, tvs.Location))
                {
                    if (TrackSections.MissingTrackSectionWarnings == 0)
                        throw new InvalidDataException($"Track node {startTrackNode.UiD} could not be found in the track database.");
                    else
                    {
                        throw new MissingTrackNodeException($"Missing Track Node at {location.Tile.X} {location.Tile.Z}!");
                    }
                }

                // Figure out which end of the track node is closest and use that.
                float startDistance = WorldLocation.GetDistance2D(WorldLocation, location).Length();
                Direction = Direction.Backward;
                NextTrackVectorSection(startTrackNode.TrackVectorSections.Length - 1);
                float endDistance = WorldLocation.GetDistance2D(WorldLocation, location).Length();
                if (startDistance < endDistance)
                {
                    Direction = Direction.Forward;
                    NextTrackVectorSection(0);
                }
            }
        }

        /// <summary>
        /// Creates a traveller starting at a specific location within a specified track node, facing in the specified direction.
        /// </summary>
        public Traveller(TrackNode startTrackNode, in Tile tile, float x, float z, Direction direction, bool roadTraveller = false)
            : this(startTrackNode as TrackVectorNode, new WorldLocation(tile, x, 0, z), roadTraveller)
        {
            Direction = direction;
        }

        /// <summary>
        /// Creates a traveller starting at a specific location within a specified track node, facing in the specified direction.
        /// </summary>
        public Traveller(TrackVectorNode startTrackNode, in WorldLocation location, Direction direction, bool roadTraveller = false)
            : this(startTrackNode, location, roadTraveller)
        {
            Direction = direction;
        }


        /// <summary>
        /// Creates a copy of another traveller, starting in the same location and with the same direction or reversed direction.
        /// </summary>
        public Traveller(Traveller source, bool reverseDirection = false)
        {
            ArgumentNullException.ThrowIfNull(source);

            trackNodes = source.trackNodes;

            locationSet = source.locationSet;
            location = source.location;
            direction = reverseDirection ? source.direction.Reverse() : source.direction;
            directionVector = source.directionVector;
            trackVectorSectionOffset = source.trackVectorSectionOffset;
            trackNode = source.trackNode;
            TrackVectorSectionIndex = source.TrackVectorSectionIndex;
            trackVectorSection = source.trackVectorSection;
            trackSection = source.trackSection;
            lengthSet = source.lengthSet;
            trackNodeLength = source.trackNodeLength;
            trackNodeOffset = source.trackNodeOffset;
        }

        public ValueTask<TravellerSaveState> Snapshot()
        {
            return ValueTask.FromResult(new TravellerSaveState()
            { 
                Direction = Direction,
                TrackVectorSectionOffset = trackVectorSectionOffset,
                TrackNodeIndex = trackNode.Index,
                TrackVectorSectionIndex = TrackNodeType == TrackNodeType.Track ? TrackVectorSectionIndex : -1,
            });
        }

        public ValueTask Restore(TravellerSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            locationSet = lengthSet = false;
            direction = saveState.Direction;
            trackVectorSectionOffset = saveState.TrackVectorSectionOffset;
            trackNode = trackNodes[saveState.TrackNodeIndex];
            if (TrackNodeType == TrackNodeType.Track)
            {
                TrackVectorSectionIndex = saveState.TrackVectorSectionIndex;
                trackVectorSection = (trackNode as TrackVectorNode).TrackVectorSections[TrackVectorSectionIndex];
                trackSection = RuntimeData.Instance.TSectionDat.TrackSections[trackVectorSection.SectionIndex];
            }
            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Test whether the given location is indeed on (or at least close to) the tracknode given by its index.
        /// If it is, we initialize the (current) traveller such that it is placed on the correct location on the track.
        /// The current traveller will not be changed if initialization is not successfull.
        /// </summary>
        /// <param name="tni">The index of the trackNode for which we test the location</param>
        /// <returns>boolean describing whether the location is indeed on the given tracknode and initialization is done</returns>
        private bool InitTrackNode(TrackNode trackNode, in WorldLocation location)
        {
            TrackNodeCandidate candidate = TrackNodeCandidate.TryTrackNode(trackNode, location);
            if (candidate == null)
                return false;

            InitFromCandidate(candidate);
            return true;
        }

        /// <summary>
        /// Initialize the traveller on the already given tracksection, and return true if this succeeded
        /// </summary>
        /// <param name="traveller">The traveller that needs to be placed</param>
        /// <param name="location">The location where it needs to be placed</param>
        /// <returns>boolean showing whether the traveller can be placed on the section at given location</returns>
        private static bool InitTrackSectionSucceeded(Traveller traveller, in WorldLocation location)
        {
            TrackNodeCandidate candidate = TrackNodeCandidate.TryTrackSection(location, traveller.trackVectorSection, traveller.trackSection);

            if (candidate == null)
                return false;

            traveller.InitFromCandidate(candidate);
            return true;
        }

        /// <summary>
        /// Switched the direction of the traveller.
        /// </summary>
        /// <remarks>
        /// To set a known direction, use <see cref="Direction"/>.
        /// </remarks>
        public void ReverseDirection()
        {
            Direction = Direction.Reverse();
        }

        /// <summary>
        /// Returns the distance from the traveller's current location, in its current direction, to the location specified.
        /// </summary>
        /// <param name="location">Target location</param>
        /// <param name="maxDistance">Maximum distance to search for specified location.</param>
        /// <returns>If the target is found, the distance from the traveller's current location, along the track nodes, to the specified location. If the target is not found, <c>-1</c>.</returns>
        public float DistanceTo(in WorldLocation location, float maxDistance = float.MaxValue)
        {
            return DistanceTo(new Traveller(this), null, location, maxDistance);
        }

        /// <summary>
        /// Returns the distance from the traveller's current location, in its current direction, to the location specified.
        /// </summary>
        /// <param name="trackNode">Target track node.</param>
        /// <param name="location">Target location</param>
        /// <param name="maxDistance">Maximum distance to search for specified location.</param>
        /// <returns>If the target is found, the distance from the traveller's current location, along the track nodes, to the specified location. If the target is not found, <c>-1</c>.</returns>
        public float DistanceTo(TrackNode trackNode, in WorldLocation location, float maxDistance = float.MaxValue)
        {
            return DistanceTo(new Traveller(this), trackNode, location, maxDistance);
        }

        /// <summary>
        /// This is the actual routine that calculates the Distance To a given location along the track.
        /// </summary>
        private static float DistanceTo(Traveller traveller, TrackNode trackNode, in WorldLocation location, float maxDistance)
        {
            float accumulatedDistance = 0f;
            while (accumulatedDistance < maxDistance)
            {
                if (traveller.TrackNodeType == TrackNodeType.Track)
                {
                    float initialOffset = traveller.trackVectorSectionOffset;
                    float radius = traveller.trackSection?.Curved ?? false ? traveller.trackSection.Radius : 1;
                    if (traveller.TrackNode == trackNode || trackNode == null)
                    {
                        int direction = traveller.Direction == Direction.Forward ? 1 : -1;
                        if (InitTrackSectionSucceeded(traveller, location))
                        {
                            // If the new offset is EARLIER, the target is behind us!
                            if (traveller.trackVectorSectionOffset * direction < initialOffset * direction)
                                break;
                            // Otherwise, accumulate distance from offset change and we're done.
                            accumulatedDistance += (traveller.trackVectorSectionOffset - initialOffset) * direction * radius;
                            return accumulatedDistance;
                        }
                    }
                    // No sign of the target location in this track section, accumulate remaining track section length and continue.
                    float length = traveller.trackSection != null ? traveller.trackSection.Curved ? Math.Abs(MathHelper.ToRadians(traveller.trackSection.Angle)) : traveller.trackSection.Length : 0;
                    accumulatedDistance += (traveller.Direction == Direction.Forward ? length - initialOffset : initialOffset) * radius;
                }
                // No sign of the target location yet, let's move on to the next track section.
                if (!traveller.NextSection())
                    break;
                if (traveller.TrackNodeType == TrackNodeType.Junction)
                {
                    // Junctions have no actual location but check the current traveller position against the target.
                    if (WorldLocation.GetDistanceSquared(traveller.WorldLocation, location) < 0.1)
                        return accumulatedDistance;
                    // No match; move past the junction node so we're on track again.
                    traveller.NextSection();
                }
                // If we've found the end of the track, the target isn't here.
                if (traveller.TrackNodeType == TrackNodeType.End)
                    break;
            }
            return -1;
        }

        public TrackVectorSection CurrentSection()
        {
            return trackNodes.VectorNodes[TrackNode.Index]?.TrackVectorSections[TrackVectorSectionIndex];
        }

        /// <summary>
        /// Moves the traveller on to the next section of track, whether that is another section within the current track node or a new track node.
        /// </summary>
        /// <returns><c>true</c> if the next section exists, <c>false</c> if it does not.</returns>
        public bool NextSection()
        {
            if (TrackNodeType == TrackNodeType.Track && NextVectorSection())
                return true;
            return NextTrackNode();
        }

        public bool NextTrackNode()
        {
            switch (TrackNodeType)
            {
                case TrackNodeType.End:
                    Debug.Assert(trackNode.InPins == 1 && trackNode.OutPins == 0);
                    break;
                case TrackNodeType.Junction:
                    Debug.Assert(trackNode.InPins == 1 && trackNode.OutPins > 1);
                    break;
                default:
                    Debug.Assert(trackNode.InPins == 1 && trackNode.OutPins == 1);
                    break;
            }

            int pin = direction == Direction.Forward ? trackNode.InPins : 0;
            if (TrackNodeType == TrackNodeType.Junction && direction == Direction.Forward)
                pin += (trackNode as TrackJunctionNode).SelectedRoute;
            if (pin < 0 || pin >= trackNode.TrackPins.Length)
                return false;
            TrackPin trPin = trackNode.TrackPins[pin];
            if (trPin.Link <= 0 || trPin.Link >= trackNodes.Count)
                return false;

            direction = trPin.Direction > 0 ? Direction.Forward : Direction.Backward;
            trackVectorSectionOffset = 0;
            trackNode = trackNodes[trPin.Link];
            TrackVectorSectionIndex = -1;
            trackVectorSection = null;
            trackSection = null;
            if (TrackNodeType == TrackNodeType.Track)
            {
                if (direction == Direction.Forward)
                    NextTrackVectorSection(0);
                else
                    NextTrackVectorSection((trackNode as TrackVectorNode).TrackVectorSections.Length - 1);
            }
            return true;
        }

        /// <summary>
        /// Moves the traveller on to the next section of the current track node only, stopping at the end of the track node.
        /// </summary>
        /// <returns><c>true</c> if the next section exists, <c>false</c> if it does not.</returns>
        public bool NextVectorSection()
        {
            TrackVectorNode tvn = trackNode as TrackVectorNode;
            if ((direction == Direction.Forward &&
                trackVectorSection == tvn.TrackVectorSections[^1]) ||
                (direction == Direction.Backward && trackVectorSection == tvn.TrackVectorSections[0]))
                return false;
            return NextTrackVectorSection(TrackVectorSectionIndex + (direction == Direction.Forward ? 1 : -1));
        }

        private bool NextTrackVectorSection(int trackVectorSectionIndex)
        {
            TrackVectorSectionIndex = trackVectorSectionIndex;
            trackVectorSection = (trackNode as TrackVectorNode).TrackVectorSections[TrackVectorSectionIndex];
            trackSection = RuntimeData.Instance.TSectionDat.TrackSections.TryGet(trackVectorSection.SectionIndex);
            if (trackSection == null)
                return false;
            locationSet = lengthSet = false;
            trackVectorSectionOffset = direction == Direction.Forward ? 0 : trackSection.Curved ? Math.Abs(MathHelper.ToRadians(trackSection.Angle)) : trackSection.Length;
            return true;
        }

        private void SetLocation()
        {
            if (locationSet)
                return;

            locationSet = true;

            TrackVectorSection tvs = trackVectorSection;
            TrackSection ts = trackSection;
            float to = trackVectorSectionOffset;
            if (tvs == null)
            {
                // We're on a junction or end node. Use one of the links to get location and direction information.
                TrackPin pin = trackNode.TrackPins[0];
                if (pin.Link <= 0 || pin.Link >= trackNodes.Count)
                    return;
                TrackVectorNode tvn = trackNodes[pin.Link] as TrackVectorNode;
                tvs = tvn.TrackVectorSections[pin.Direction > 0 ? 0 : tvn.TrackVectorSections.Length - 1];
                ts = RuntimeData.Instance.TSectionDat.TrackSections.TryGet(tvs.SectionIndex);
                if (ts == null)
                    return; // This is really bad and we'll have unknown data in the Traveller when the code reads the location and direction!
                to = pin.Direction > 0 ? -trackVectorSectionOffset : ts.Length + trackVectorSectionOffset;
            }

            location = tvs.Location;
            directionVector = tvs.Direction;

            if (ts.Curved)
            {
                // "Handedness" Convention: A right-hand curve (TS.SectionCurve.Angle > 0) curves 
                // to the right when moving forward.
                int sign = -Math.Sign(ts.Angle);
                Vector3 vectorCurveStartToCenter = Vector3.Left * ts.Radius * sign;
                Matrix curveRotation = Matrix.CreateRotationY(to * sign);
                InterpolateHelper.InterpolateAlongCurveLine(Vector3.Zero, vectorCurveStartToCenter, curveRotation, tvs.Direction, out _, out Vector3 displacement);
                displacement.Z *= -1;
                location = new WorldLocation(location.Tile, location.Location + displacement);
                directionVector.Y -= to * sign;
            }
            else
            {
                InterpolateHelper.InterpolateAlongStraightLine(Vector3.Zero, Vector3.UnitZ, to, tvs.Direction, out _, out Vector3 displacement);
                location = new WorldLocation(location.Tile, location.Location + displacement);
            }

            if (direction == Direction.Backward)
            {
                directionVector.X *= -1;
                directionVector.Y += MathHelper.Pi;
            }
            directionVector.X = MathHelper.WrapAngle(directionVector.X);
            directionVector.Y = MathHelper.WrapAngle(directionVector.Y);

            if (trackVectorSection != null)
                location = location.NormalizeTo(trackVectorSection.Location.Tile);
        }

        private void SetLength()
        {
            if (lengthSet)
                return;
            lengthSet = true;
            trackNodeLength = 0;
            trackNodeOffset = 0;
            if (!(trackNode is TrackVectorNode tvn) || tvn.TrackVectorSections == null)
                return;
            TrackVectorSection[] tvs = tvn.TrackVectorSections;
            for (int i = 0; i < tvs.Length; i++)
            {
                TrackSection ts = RuntimeData.Instance.TSectionDat.TrackSections.TryGet(tvs[i].SectionIndex);
                if (ts == null)
                    continue; // This is bad and we'll have potentially bogus data in the Traveller when the code reads the length!
                trackNodeLength += ts.Length;
                if (i < TrackVectorSectionIndex)
                    trackNodeOffset += ts.Length;
                else if (i == TrackVectorSectionIndex)
                    trackNodeOffset += trackVectorSectionOffset * (ts.Curved ? ts.Radius : 1);
            }
            if (Direction == Direction.Backward)
                trackNodeOffset = trackNodeLength - trackNodeOffset;
        }

        /// <summary>
        /// Current Curve Radius value. Zero if not a curve
        /// </summary>
        /// <returns>Current Curve Radius in meters</returns>
        public float CurveRadius()
        {
            if (trackSection == null)
                return 0;

            return trackSection.Curved ? trackSection.Radius : 0;
        }

        public float GetCurvature()
        {
            if (trackSection == null)
                return 0;

            return trackSection.Curved ? Math.Sign(trackSection.Angle) / trackSection.Radius : 0;
        }

        public float GetSuperElevation()
        {
            if (trackSection == null)
                return 0;

            if (!trackSection.Curved)
                return 0;

            if (trackVectorSection == null)
                return 0;

            float trackLength = Math.Abs(MathHelper.ToRadians(trackSection.Angle));
            int sign = Math.Sign(trackSection.Angle) > 0 ^ direction == Direction.Backward ? -1 : 1;
            float trackOffsetReverse = trackLength - trackVectorSectionOffset;

            float startingElevation = trackVectorSection.StartElev;
            float endingElevation = trackVectorSection.EndElev;
            float elevation = trackVectorSection.MaxElev * sign;

            // Check if there is no super-elevation at all.
            if (elevation.AlmostEqual(0f, 0.001f))
                return 0;

            if (trackVectorSectionOffset < trackLength / 2)
            {
                // Start of the curve; if there is starting super-elevation, use max super-elevation.
                if (startingElevation.AlmostEqual(0f, 0.001f))
                    return elevation * trackVectorSectionOffset * 2 / trackLength;

                return elevation;
            }

            // End of the curve; if there is ending super-elevation, use max super-elevation.
            if (endingElevation.AlmostEqual(0f, 0.001f))
                return elevation * trackOffsetReverse * 2 / trackLength;

            return elevation;
        }

        public float GetSuperElevation(float smoothingOffset)
        {
            Traveller offset = new Traveller(this);
            offset.Move(smoothingOffset);
            return (GetSuperElevation() + offset.GetSuperElevation()) / 2;
        }

        public float FindTiltedZ(float speed) //will test 1 second ahead, computed will return desired elev. only
        {
            if (speed < 12)
                return 0;//no tilt if speed too low (<50km/h)
            if (!(trackNode is TrackVectorNode))
                return 0f;
            TrackVectorSection tvs = trackVectorSection;
            TrackSection ts = trackSection;
            float desiredZ;
            if (tvs == null)
            {
                desiredZ = 0f;
            }
            else if (ts.Curved)
            {
                float maxv = 0.14f * speed / 40f;
                //maxv *= speed / 40f;
                //if (maxv.AlmostEqual(0f, 0.001f)) maxv = 0.02f; //short curve, add some effect anyway
                int sign = -Math.Sign(ts.Angle);
                if ((direction == Direction.Forward ? 1 : -1) * sign > 0)
                    desiredZ = 1f;
                else
                    desiredZ = -1f;
                desiredZ *= maxv;//max elevation
            }
            else
                desiredZ = 0f;
            return desiredZ;
        }

        /// <summary>
        /// Move the traveller along the track by the specified distance, or until the end of the track is reached.
        /// </summary>
        /// <param name="distanceToGo">The distance to travel along the track. Positive values travel in the direction of the traveller and negative values in the opposite direction.</param>
        /// <returns>The remaining distance if the traveller reached the end of the track.</returns>
        public float Move(double distanceToGo)
        {
            if (double.IsNaN(distanceToGo))
                distanceToGo = 0f;
            int distanceSign = Math.Sign(distanceToGo);
            distanceToGo = Math.Abs(distanceToGo);
            if (distanceSign < 0)
                ReverseDirection();
            do
            {
                distanceToGo = MoveInTrackSection((float)distanceToGo);
                if (distanceToGo < 0.001)
                    break;
            }
            while (NextSection());
            if (distanceSign < 0)
                ReverseDirection();
            return (float)(distanceSign * distanceToGo);
        }

        /// <summary>
        /// Move the traveller along the track by the specified distance, or until the end of the track is reached, within the current track section only.
        /// </summary>
        /// <param name="distanceToGo">The distance to travel along the track section. Positive values travel in the direction of the traveller and negative values in the opposite direction.</param>
        /// <returns>The remaining distance if the traveller reached the end of the track section.</returns>
        public float MoveInSection(float distanceToGo)
        {
            int sign = Math.Sign(distanceToGo);
            distanceToGo = Math.Abs(distanceToGo);
            if (sign < 0)
                ReverseDirection();
            distanceToGo = MoveInTrackSection(distanceToGo);
            if (sign < 0)
                ReverseDirection();
            return sign * distanceToGo;
        }

        private float MoveInTrackSection(float distanceToGo)
        {
            return TrackNodeType switch
            {
                TrackNodeType.Junction => distanceToGo,
                TrackNodeType.End => distanceToGo,
                TrackNodeType.Track => (trackSection?.Curved ?? false) ? MoveInTrackSectionCurved(distanceToGo) : MoveInTrackSectionStraight(distanceToGo),
                _ => MoveInTrackSectionInfinite(distanceToGo),
            };
        }

        private float MoveInTrackSectionInfinite(float distanceToGo)
        {
            int sign = Direction == Direction.Forward ? 1 : -1;
            float distance = distanceToGo;
            if (Direction == Direction.Backward && distance > trackVectorSectionOffset)
                distance = trackVectorSectionOffset;
            trackVectorSectionOffset += sign * distance;
            trackNodeOffset += distance;
            locationSet = false;
            return distanceToGo - distance;
        }

        private float MoveInTrackSectionCurved(float distanceToGo)
        {
            int sign = Direction == Direction.Forward ? 1 : -1;
            float desiredTurnRadians = distanceToGo / trackSection.Radius;
            float sectionTurnRadians = Math.Abs(MathHelper.ToRadians(trackSection.Angle));
            if (direction == Direction.Forward)
            {
                if (desiredTurnRadians > sectionTurnRadians - trackVectorSectionOffset)
                    desiredTurnRadians = sectionTurnRadians - trackVectorSectionOffset;
            }
            else
            {
                if (desiredTurnRadians > trackVectorSectionOffset)
                    desiredTurnRadians = trackVectorSectionOffset;
            }
            trackVectorSectionOffset += sign * desiredTurnRadians;
            trackNodeOffset += desiredTurnRadians * trackSection.Radius;
            locationSet = false;
            return distanceToGo - desiredTurnRadians * trackSection.Radius;
        }

        private float MoveInTrackSectionStraight(float distanceToGo)
        {
            int sign = Direction == Direction.Forward ? 1 : -1;
            float desiredDistance = distanceToGo;
            if (direction == Direction.Forward)
            {
                if (desiredDistance > trackSection.Length - trackVectorSectionOffset)
                    desiredDistance = trackSection.Length - trackVectorSectionOffset;
            }
            else
            {
                if (desiredDistance > trackVectorSectionOffset)
                    desiredDistance = trackVectorSectionOffset;
            }
            trackVectorSectionOffset += sign * desiredDistance;
            trackNodeOffset += desiredDistance;
            locationSet = false;
            return distanceToGo - desiredDistance;
        }

        // TODO: This is a bit of a strange method that probably should be cleaned up.
        public float OverlapDistanceM(Traveller other, bool rear)
        {
            ArgumentNullException.ThrowIfNull(other);

            Tile delta = Tile - other.Tile;
            float dx = X - other.X + 2048 * delta.X;
            float dz = Z - other.Z + 2048 * delta.Z;
            float dy = Y - other.Y;
            if (dx * dx + dz * dz > 1)
                return 1;
            if (Math.Abs(dy) > 1)
                return 1;
            float dot = dx * (float)Math.Sin(directionVector.Y) + dz * (float)Math.Cos(directionVector.Y);
            return rear ? dot : -dot;
        }

        // Checks if trains are overlapping. Used in multiplayer, where the standard method may lead to train overlapping
        public float RoughOverlapDistanceM(Traveller other, Traveller farMe, Traveller farOther, float lengthMe, float lengthOther, bool rear)
        {
            ArgumentNullException.ThrowIfNull(other);
            ArgumentNullException.ThrowIfNull(farMe);
            ArgumentNullException.ThrowIfNull(farOther);

            float dy = Y - other.Y;
            if (Math.Abs(dy) > 1)
                return 1;
            Tile tileDelta = farMe.Tile - other.Tile;   
            float dx = farMe.X - other.X + 2048 * tileDelta.X;
            float dz = farMe.Z - other.Z + 2048 * tileDelta.Z;
            if (dx * dx + dz * dz > lengthMe * lengthMe)
                return 1;
            tileDelta = Tile - farOther.Tile;
            dx = X - farOther.X + 2048 * tileDelta.X;
            dz = Z - farOther.Z + 2048 * tileDelta.Z;
            if (dx * dx + dz * dz > lengthOther * lengthOther)
                return 1;
            tileDelta = Tile - other.Tile;
            dx = X - other.X + 2048 * tileDelta.X;
            dz = Z - other.Z + 2048 * tileDelta.Z;
            float diagonal = dx * dx + dz * dz;
            if (diagonal < 200 && diagonal < (lengthMe + lengthOther) * (lengthMe + lengthOther))
            {
                float dot = dx * (float)Math.Sin(directionVector.Y) + dz * (float)Math.Cos(directionVector.Y);
                return rear ? dot : -dot;
            }
            return 1;
        }

        public override string ToString()
        {
            return $"{{TrackNodeIndex={TrackNode.Index} TrackVectorSectionIndex={TrackVectorSectionIndex} Offset={trackVectorSectionOffset:F6}}}";
        }

        /// <summary>
        /// During initialization a specific track section (candidate) needs to be found corresponding to the requested worldLocation.
        /// Once the best (or only) candidate has been found, this routine initializes the traveller from the information
        /// stored in the candidate.
        /// </summary>
        /// <param name="candidate">The candidate with all information needed to place the traveller</param>
        private void InitFromCandidate(TrackNodeCandidate candidate)
        {
            // Some things only have to be set when defined. This prevents overwriting existing settings.
            // The order might be important.
            if (candidate.TrackNode != null)
                trackNode = candidate.TrackNode;
            if (candidate.TrackVectorSection != null)
                trackVectorSection = candidate.TrackVectorSection;
            if (candidate.TrackVectorSectionIndex >= 0)
                TrackVectorSectionIndex = candidate.TrackVectorSectionIndex;

            // these are always set:
            direction = Direction.Forward;
            trackVectorSectionOffset = 0;
            locationSet = lengthSet = false;
            trackSection = candidate.TrackSection;
            if (trackSection.Curved)
            {
                MoveInTrackSectionCurved(candidate.Longitude);
            }
            else
            {
                MoveInTrackSectionStraight(candidate.Longitude);
            }
        }
    }
}
