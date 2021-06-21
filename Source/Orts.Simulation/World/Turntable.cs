// COPYRIGHT 2010, 2011, 2012, 2013, 2014, 2015, 2016 by the Open Rails project.
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
using System.IO;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;

namespace Orts.Simulation.World
{
    public class Turntable : MovingTable
    {
        // Fixed data
        public List<float> Angles = new List<float>();
        public float StartingY; // starting yaw angle
        // Dynamic data
        public bool Clockwise; // clockwise motion on
        public bool Counterclockwise; // counterclockwise motion on
        public bool AutoClockwise; // clockwise motion is on - auto control mode
        public bool AutoCounterclockwise; // clockwise motion is on - auto control mode
        public float YAngle; // Y angle of animated part, to be compared with Y angles of endpoints
        public bool ForwardConnected = true; // Platform has its forward part connected to a track
        public bool RearConnected; // Platform has its rear part connected to a track
        public bool SaveForwardConnected = true; // Platform has its forward part connected to a track
        public bool SaveRearConnected; // Platform has its rear part connected to a track
        public int ForwardConnectedTarget = -1; // index of trackend connected
        public int RearConnectedTarget = -1; // index of trackend connected
        public float TargetY; //final target for Viewer;

        public SignalEnvironment signalRef { get; protected set; }

        public Turntable(STFReader stf)
        {
            signalRef = Simulator.Instance.SignalEnvironment;
            string animation;
            Matrix location = Matrix.Identity;
            location.M44 = 100_000_000; //WorlPosition not yet defined, will be loaded when loading related tile;
            stf.MustMatch("(");
            stf.ParseBlock(new[] {
                new STFReader.TokenProcessor("wfile", ()=>{
                    WFile = stf.ReadStringBlock(null);
                    position = new WorldPosition(int.Parse(WFile.Substring(1, 7)), int.Parse(WFile.Substring(8, 7)), location);
                }),
                new STFReader.TokenProcessor("uid", ()=>{ UID = stf.ReadIntBlock(-1); }),
                new STFReader.TokenProcessor("animation", ()=>{ animation = stf.ReadStringBlock(null);
                                                                Animations.Add(animation.ToLower());}),
                new STFReader.TokenProcessor("diameter", ()=>{ Length = stf.ReadFloatBlock(STFReader.Units.None , null);}),
                new STFReader.TokenProcessor("xoffset", ()=>{ offset.X += stf.ReadFloatBlock(STFReader.Units.None , null);}),
                new STFReader.TokenProcessor("zoffset", ()=>{ offset.Z = -stf.ReadFloatBlock(STFReader.Units.None , null);}),
                new STFReader.TokenProcessor("trackshapeindex", ()=>
                {
                    TrackShapeIndex = stf.ReadIntBlock(-1);
                    InitializeAnglesAndTrackNodes();
                }),
             });
        }

        /// <summary>
        /// Saves the general variable parameters
        /// Called from within the Simulator class.
        /// </summary>
        internal override void Save(BinaryWriter outf)
        {
            base.Save(outf);
            outf.Write(Clockwise);
            outf.Write(Counterclockwise);
            outf.Write(AutoClockwise);
            outf.Write(AutoCounterclockwise);
            outf.Write(YAngle);
            outf.Write(ForwardConnected);
            outf.Write(RearConnected);
            outf.Write(SaveForwardConnected);
            outf.Write(SaveRearConnected);
            outf.Write(ForwardConnectedTarget);
            outf.Write(RearConnectedTarget);
            outf.Write(TargetY);
        }


        /// <summary>
        /// Restores the general variable parameters
        /// Called from within the Simulator class.
        /// </summary>
        internal override void Restore(BinaryReader inf, Simulator simulator)
        {
            base.Restore(inf, simulator);
            Clockwise = inf.ReadBoolean();
            Counterclockwise = inf.ReadBoolean();
            AutoClockwise = inf.ReadBoolean();
            AutoCounterclockwise = inf.ReadBoolean();
            YAngle = inf.ReadSingle();
            ForwardConnected = inf.ReadBoolean();
            RearConnected = inf.ReadBoolean();
            SaveForwardConnected = inf.ReadBoolean();
            SaveRearConnected = inf.ReadBoolean();
            ForwardConnectedTarget = inf.ReadInt32();
            RearConnectedTarget = inf.ReadInt32();
            TargetY = inf.ReadSingle();
        }

        protected void InitializeAnglesAndTrackNodes()
        {
            var trackShape = Simulator.Instance.TSectionDat.TrackShapes[(uint)TrackShapeIndex];
            var nSections = Simulator.Instance.TSectionDat.TrackShapes[(uint)TrackShapeIndex].SectionIndices[0].SectionsCount;
            trackNodesIndex = new int[Simulator.Instance.TSectionDat.TrackShapes[(uint)TrackShapeIndex].SectionIndices.Length];
            trackNodesOrientation = new bool[trackNodesIndex.Length];
            trackVectorSectionsIndex = new int[trackNodesIndex.Length];
            var iMyTrackNodes = 0;
            foreach (var sectionIdx in trackShape.SectionIndices)
            {
                Angles.Add(MathHelper.ToRadians((float)sectionIdx.AngularOffset));
                trackNodesIndex[iMyTrackNodes] = -1;
                trackVectorSectionsIndex[iMyTrackNodes] = -1;
                iMyTrackNodes++;
            }
            var trackNodes = Simulator.Instance.TDB.TrackDB.TrackNodes;
            int iTrackNode = 0;
            for (iTrackNode = 1; iTrackNode < trackNodes.Length; iTrackNode++)
                if (trackNodes[iTrackNode] is TrackVectorNode tvn && tvn.TrackVectorSections != null)
                {
                    var iTrVectorSection = Array.FindIndex(tvn.TrackVectorSections, trVectorSection =>
                        trVectorSection.Location.TileX == WorldPosition.TileX && trVectorSection.Location.TileZ == WorldPosition.TileZ && trVectorSection.WorldFileUiD == UID);
                    if (iTrVectorSection >= 0)
                        if (tvn.TrackVectorSections.Length > (int)nSections)
                        {
                            iMyTrackNodes = tvn.TrackVectorSections[iTrVectorSection].Flag1 / 2;
                            trackNodesIndex[iMyTrackNodes] = iTrackNode;
                            trackVectorSectionsIndex[iMyTrackNodes] = iTrVectorSection;
                            trackNodesOrientation[iMyTrackNodes] = tvn.TrackVectorSections[iTrVectorSection].Flag1 % 2 == 0 ? true : false;

                        }
                }
        }

        /// <summary>
        /// Computes the nearest turntable exit in the actual direction
        /// Returns the Y angle to be compared.
        /// </summary>
        public override void ComputeTarget(bool clockwise)
        {
            if (!ContinuousMotion) return;
            ContinuousMotion = false;
            GoToTarget = false;
            Clockwise = clockwise;
            Counterclockwise = !clockwise;
            if (Clockwise)
            {
                var forwardAngleDiff = 3.5f;
                var rearAngleDiff = 3.5f;
                ForwardConnected = false;
                RearConnected = false;
                if (Angles.Count <= 0)
                {
                    Clockwise = false;
                    ForwardConnectedTarget = -1;
                    RearConnectedTarget = -1;
                }
                else
                {
                    for (int iAngle = Angles.Count - 1; iAngle >= 0; iAngle--)
                        if (trackNodesIndex[iAngle] != -1 && trackVectorSectionsIndex[iAngle] != -1)
                        {
                            var thisAngleDiff = MathHelper.WrapAngle(Angles[iAngle] + YAngle);
                            if (thisAngleDiff < forwardAngleDiff && thisAngleDiff >= 0)
                            {
                                ForwardConnectedTarget = iAngle;
                                forwardAngleDiff = thisAngleDiff;
                            }
                            thisAngleDiff = MathHelper.WrapAngle(Angles[iAngle] + YAngle + (float)Math.PI);
                            if (thisAngleDiff < rearAngleDiff && thisAngleDiff >= 0)
                            {
                                RearConnectedTarget = iAngle;
                                rearAngleDiff = thisAngleDiff;
                            }
                        }
                    if (forwardAngleDiff < 0.1 || rearAngleDiff < 0.1)
                        if (forwardAngleDiff < rearAngleDiff && Math.Abs(forwardAngleDiff - rearAngleDiff) > 0.01)
                            RearConnectedTarget = -1;
                        else if (forwardAngleDiff > rearAngleDiff && Math.Abs(forwardAngleDiff - rearAngleDiff) > 0.01)
                            ForwardConnectedTarget = -1;
                        else
                        {
                            Clockwise = false;
                            ForwardConnectedTarget = -1;
                            RearConnectedTarget = -1;
                        }
                }
            }
            else if (Counterclockwise)
            {
                var forwardAngleDiff = -3.5f;
                var rearAngleDiff = -3.5f;
                ForwardConnected = false;
                RearConnected = false;
                if (Angles.Count <= 0)
                {
                    Counterclockwise = false;
                    ForwardConnectedTarget = -1;
                    RearConnectedTarget = -1;
                }
                else
                {
                    for (int iAngle = 0; iAngle <= Angles.Count - 1; iAngle++)
                        if (trackNodesIndex[iAngle] != -1 && trackVectorSectionsIndex[iAngle] != -1)
                        {
                            var thisAngleDiff = MathHelper.WrapAngle(Angles[iAngle] + YAngle);
                            if (thisAngleDiff > forwardAngleDiff && thisAngleDiff <= 0)
                            {
                                ForwardConnectedTarget = iAngle;
                                forwardAngleDiff = thisAngleDiff;
                            }
                            thisAngleDiff = MathHelper.WrapAngle(Angles[iAngle] + YAngle + (float)Math.PI);
                            if (thisAngleDiff > rearAngleDiff && thisAngleDiff <= 0)
                            {
                                RearConnectedTarget = iAngle;
                                rearAngleDiff = thisAngleDiff;
                            }
                        }
                    if (forwardAngleDiff > -0.1 || rearAngleDiff > -0.1)
                        if (forwardAngleDiff > rearAngleDiff && Math.Abs(forwardAngleDiff - rearAngleDiff) > 0.01)
                            RearConnectedTarget = -1;
                        else if (forwardAngleDiff < rearAngleDiff && Math.Abs(forwardAngleDiff - rearAngleDiff) > 0.01)
                            ForwardConnectedTarget = -1;
                        else
                        {
                            Counterclockwise = false;
                            ForwardConnectedTarget = -1;
                            RearConnectedTarget = -1;
                        }
                }

            }
            return;
        }

        /// <summary>
        /// Starts continuous movement
        /// 
        /// </summary>
        /// 
        public override void StartContinuous(bool clockwise)
        {
            if (TrainsOnMovingTable.Count > 1 || TrainsOnMovingTable.Count == 1 && TrainsOnMovingTable[0].FrontOnBoard ^ TrainsOnMovingTable[0].BackOnBoard)
            {
                Clockwise = false;
                Counterclockwise = false;
                ContinuousMotion = false;
                if (SendNotifications) Simulator.Instance.Confirmer.Warning(Simulator.Catalog.GetString("Train partially on turntable, can't rotate"));
                return;
            }
            if (TrainsOnMovingTable.Count == 1 && TrainsOnMovingTable[0].FrontOnBoard && TrainsOnMovingTable[0].BackOnBoard)
            {
                // Preparing for rotation
                var train = TrainsOnMovingTable[0].Train;
                if (Math.Abs(train.SpeedMpS) > 0.1 || train.LeadLocomotiveIndex != -1 && (train.LeadLocomotive.ThrottlePercent >= 1 || !(train.LeadLocomotive.Direction == MidpointDirection.N
                 || Math.Abs(train.MUReverserPercent) <= 1)) || train.ControlMode != TrainControlMode.Manual && train.ControlMode != TrainControlMode.TurnTable &&
                 train.ControlMode != TrainControlMode.Explorer && train.ControlMode != TrainControlMode.Undefined)
                {
                    if (SendNotifications) Simulator.Instance.Confirmer.Warning(Simulator.Catalog.GetString("Rotation can't start: check throttle, speed, direction and control mode"));
                    return;
                }
                if (train.ControlMode == TrainControlMode.Manual || train.ControlMode == TrainControlMode.Explorer || train.ControlMode == TrainControlMode.Undefined)
                {
                    ComputeTrainPosition(train);
                    train.ControlMode = TrainControlMode.TurnTable;
                }
                if (SendNotifications) Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Turntable starting rotation with train"));

            }
            Clockwise = clockwise;
            Counterclockwise = !clockwise;
            ContinuousMotion = true;
        }

        // Computing position of cars relative to center of platform
        public void ComputeTrainPosition(Train train)
        {
            SaveForwardConnected = ForwardConnected ^ !trackNodesOrientation[ConnectedTrackEnd];
            SaveRearConnected = RearConnected;
            var invAnimationXNAMatrix = Matrix.Invert(animationXNAMatrix);
            relativeCarPositions = new List<Matrix>();
            foreach (TrainCar trainCar in train.Cars)
            {
                var relativeCarPosition = Matrix.Identity;
                trainCar.WorldPosition = trainCar.WorldPosition.NormalizeTo(WorldPosition.TileX, WorldPosition.TileZ);
                relativeCarPosition = Matrix.Multiply(trainCar.WorldPosition.XNAMatrix, invAnimationXNAMatrix);
                relativeCarPositions.Add(relativeCarPosition);
            }
            var XNALocation = train.FrontTDBTraveller.Location;
            XNALocation.Z = -XNALocation.Z;
            XNALocation.X = XNALocation.X + 2048 * (train.FrontTDBTraveller.TileX - WorldPosition.TileX);
            XNALocation.Z = XNALocation.Z - 2048 * (train.FrontTDBTraveller.TileZ - WorldPosition.TileZ);
            relativeFrontTravellerXNALocation = Vector3.Transform(XNALocation, invAnimationXNAMatrix);
            XNALocation = train.RearTDBTraveller.Location;
            XNALocation.Z = -XNALocation.Z;
            XNALocation.X = XNALocation.X + 2048 * (train.RearTDBTraveller.TileX - WorldPosition.TileX);
            XNALocation.Z = XNALocation.Z - 2048 * (train.RearTDBTraveller.TileZ - WorldPosition.TileZ);
            relativeRearTravellerXNALocation = Vector3.Transform(XNALocation, invAnimationXNAMatrix);
        }

        public void ComputeCenter(in WorldPosition worldPosition)
        {
            VectorExtension.Transform(offset, worldPosition.XNAMatrix, out Vector3 centerCoordinates);
            position = worldPosition.SetTranslation(centerCoordinates.X, centerCoordinates.Y, centerCoordinates.Z);

        }

        public void RotateTrain(Matrix animationXNAMatrix)
        {
            animationXNAMatrix = animationXNAMatrix;
            if ((Clockwise || Counterclockwise || GoToTarget || GoToAutoTarget) && TrainsOnMovingTable.Count == 1 && TrainsOnMovingTable[0].FrontOnBoard &&
                TrainsOnMovingTable[0].BackOnBoard && TrainsOnMovingTable[0].Train.ControlMode == TrainControlMode.TurnTable)
            {
                // Rotate together also train
                var iRelativeCarPositions = 0;
                foreach (TrainCar traincar in TrainsOnMovingTable[0].Train.Cars)
                {
                    traincar.WorldPosition = new WorldPosition(traincar.WorldPosition.TileX, traincar.WorldPosition.TileZ,
                        Matrix.Multiply(relativeCarPositions[iRelativeCarPositions], animationXNAMatrix));
                    iRelativeCarPositions++;
                }
            }
        }

        public void AutoRotateTable(double elapsedClockSeconds)
        {
            GoToAutoTarget = true;

            double angleStep = (YAngle / Math.PI * 1800.0 + 3600) % 3600.0;
            float usedFrameRate = TurntableFrameRate ?? 30f;

            if (AutoClockwise)
                angleStep -= elapsedClockSeconds * usedFrameRate;
            else if (AutoCounterclockwise)
                angleStep += elapsedClockSeconds * usedFrameRate;

            YAngle = TargetY = (float)MathHelperD.WrapAngle(angleStep / 1800.0 * Math.PI);
        }

        public override void Update()
        {
            foreach (var trainOnTurntable in TrainsOnMovingTable)
                if (trainOnTurntable.FrontOnBoard ^ trainOnTurntable.BackOnBoard)
                {
                    Clockwise = false;
                    Counterclockwise = false;
                    ContinuousMotion = false;
                    return;
                }
            if (ContinuousMotion)
            {
                ForwardConnected = false;
                RearConnected = false;
                ConnectedTrackEnd = -1;
                GoToTarget = false;
            }
            else
                if (Clockwise || AutoClockwise)
            {
                ForwardConnected = false;
                RearConnected = false;
                if (ForwardConnectedTarget != -1)
                    if (Math.Abs(MathHelper.WrapAngle(Angles[ForwardConnectedTarget] + YAngle)) < 0.005)
                    {
                        ForwardConnected = true;
                        GoToTarget = Clockwise;  // only set if not in auto mode
                        Clockwise = false;
                        AutoClockwise = false;
                        ConnectedTrackEnd = ForwardConnectedTarget;
                        if (SendNotifications) Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Turntable forward connected"));
                        TargetY = -Angles[ForwardConnectedTarget];
                    }
                    else if (RearConnectedTarget != -1)
                        if (Math.Abs(MathHelper.WrapAngle(Angles[RearConnectedTarget] + YAngle + (float)Math.PI)) < 0.0055)
                        {
                            RearConnected = true;
                            GoToTarget = Clockwise;  // only set if not in auto mode
                            Clockwise = false;
                            AutoClockwise = false;
                            ConnectedTrackEnd = RearConnectedTarget;
                            if (SendNotifications) Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Turntable backward connected"));
                            TargetY = -MathHelper.WrapAngle(Angles[RearConnectedTarget] + (float)Math.PI);
                        }
            }
            else if (Counterclockwise || AutoCounterclockwise)
            {
                ForwardConnected = false;
                RearConnected = false;
                if (ForwardConnectedTarget != -1)
                    if (Math.Abs(MathHelper.WrapAngle(Angles[ForwardConnectedTarget] + YAngle)) < 0.005)
                    {
                        ForwardConnected = true;
                        GoToTarget = Counterclockwise;  // only set if not in auto mode
                        Counterclockwise = false;
                        AutoCounterclockwise = false;
                        ConnectedTrackEnd = ForwardConnectedTarget;
                        if (SendNotifications) Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Turntable forward connected"));
                        TargetY = -Angles[ForwardConnectedTarget];
                    }
                    else if (RearConnectedTarget != -1)
                        if (Math.Abs(MathHelper.WrapAngle(Angles[RearConnectedTarget] + YAngle + (float)Math.PI)) < 0.0055)
                        {
                            RearConnected = true;
                            GoToTarget = Counterclockwise;  // only set if not in auto mode
                            Counterclockwise = false;
                            AutoCounterclockwise = false;
                            ConnectedTrackEnd = RearConnectedTarget;
                            if (SendNotifications) Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Turntable backward connected"));
                            TargetY = -MathHelper.WrapAngle(Angles[RearConnectedTarget] + (float)Math.PI);
                        }
            }
        }

        /// <summary>
        /// TargetExactlyReached: if train on board, it can exit the turntable
        /// </summary>
        /// 
        public void TargetExactlyReached()
        {
            Traveller.TravellerDirection direction = ForwardConnected ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward;
            direction = SaveForwardConnected ^ !trackNodesOrientation[ConnectedTrackEnd] ? direction : direction == Traveller.TravellerDirection.Forward ? Traveller.TravellerDirection.Backward : Traveller.TravellerDirection.Forward;
            GoToTarget = false;
            if (TrainsOnMovingTable.Count == 1)
            {
                var train = TrainsOnMovingTable[0].Train;
                if (train.ControlMode == TrainControlMode.TurnTable)
                    train.ReenterTrackSections(trackNodesIndex[ConnectedTrackEnd], finalFrontTravellerXNALocation, finalRearTravellerXNALocation, direction);
            }
        }

        /// <summary>
        /// CheckMovingTableAligned: checks if turntable aligned with entering train
        /// </summary>
        /// 

        public override bool CheckMovingTableAligned(Train train, bool forward)
        {
            Traveller.TravellerDirection direction;
            if ((ForwardConnected || RearConnected) && trackVectorSectionsIndex[ConnectedTrackEnd] != -1 && trackNodesIndex[ConnectedTrackEnd] != -1 &&
                (trackNodesIndex[ConnectedTrackEnd] == train.FrontTDBTraveller.TN.Index || trackNodesIndex[ConnectedTrackEnd] == train.RearTDBTraveller.TN.Index))
            {
                direction = ForwardConnected ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward;
                return true;
            }
            direction = Traveller.TravellerDirection.Forward;
            return false;
        }



        /// <summary>
        /// PerformUpdateActions: actions to be performed at every animation step
        /// </summary>
        /// 
        public void PerformUpdateActions(Matrix absAnimationMatrix)
        {
            RotateTrain(absAnimationMatrix);
            if ((GoToTarget || GoToAutoTarget) && TrainsOnMovingTable.Count == 1 && TrainsOnMovingTable[0].Train.ControlMode == TrainControlMode.TurnTable)
                RecalculateTravellerXNALocations(absAnimationMatrix);
            if (GoToTarget) TargetExactlyReached();
        }
    }
}
