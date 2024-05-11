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
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

using FreeTrainSimulator.Common;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Simulation.Multiplayer;
using Orts.Simulation.Multiplayer.Messaging;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.World
{
    public class TurnTable : MovingTable
    {
        // Fixed data
        private readonly List<float> angles = new List<float>();
        // Dynamic data
        public Rotation RotationDirection { get; set; }
        public Rotation AutoRotationDirection { get; set; }
        public float MaxAngle { get; set; } = -1; // max angle extension for partial turntables (in radians)

        public float YAngle { get; set; } // Y angle of animated part, to be compared with Y angles of endpoints
        public bool ForwardConnected { get; private set; } = true; // Platform has its forward part connected to a track
        public bool RearConnected { get; private set; } // Platform has its rear part connected to a track
        public int ForwardConnectedTarget { get; set; } = -1; // index of trackend connected
        public int RearConnectedTarget { get; set; } = -1; // index of trackend connected
        public float TargetY { get; set; } //final target for Viewer;

        private bool saveForwardConnected = true; // Platform has its forward part connected to a track
        private bool saveRearConnected; // Platform has its rear part connected to a track

        internal TurnTable(STFReader stf)
        {
            string animation;
            Matrix location = Matrix.Identity;
            location.M44 = 100_000_000; //WorlPosition not yet defined, will be loaded when loading related tile;
            stf.MustMatch("(");
            stf.ParseBlock(new[] {
                new STFReader.TokenProcessor("wfile", ()=>{
                    WFile = stf.ReadStringBlock(null);
                    position = new WorldPosition(int.Parse(WFile.Substring(1, 7), CultureInfo.InvariantCulture), int.Parse(WFile.Substring(8, 7), CultureInfo.InvariantCulture), location);
                }),
                new STFReader.TokenProcessor("uid", ()=>{ UID = stf.ReadIntBlock(-1); }),
                new STFReader.TokenProcessor("animation", ()=>{ animation = stf.ReadStringBlock(null);
                                                                Animations.Add(animation);}),
                new STFReader.TokenProcessor("diameter", ()=>{ Length = stf.ReadFloatBlock(STFReader.Units.None , null);}),
                new STFReader.TokenProcessor("xoffset", ()=>{ offset.X += stf.ReadFloatBlock(STFReader.Units.None , null);}),
                new STFReader.TokenProcessor("zoffset", ()=>{ offset.Z = -stf.ReadFloatBlock(STFReader.Units.None , null);}),
                new STFReader.TokenProcessor("trackshapeindex", ()=>
                {
                    TrackShapeIndex = stf.ReadIntBlock(-1);
                    InitializeAnglesAndTrackNodes();
                }),
                new STFReader.TokenProcessor("maxangle", ()=>{ MaxAngle = MathHelper.ToRadians(stf.ReadFloatBlock(STFReader.Units.None , null));}),
             });
        }

        public override async ValueTask<MovingTableSaveState> Snapshot()
        {
            MovingTableSaveState result = await base.Snapshot().ConfigureAwait(false);
            result.RotationDirection = RotationDirection;
            result.AutoRotationDirection = AutoRotationDirection;
            result.ForwardConnection = new ConnectionItem(ForwardConnected, saveForwardConnected, ForwardConnectedTarget);
            result.BackwardConnection = new ConnectionItem(RearConnected, saveRearConnected, RearConnectedTarget);
            result.Target = new Vector2(YAngle, TargetY);
            return result;
        }

        public override async ValueTask Restore([NotNull] MovingTableSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            RotationDirection = saveState.RotationDirection;
            AutoRotationDirection = saveState.AutoRotationDirection;
            ForwardConnected = saveState.ForwardConnection.Connected;
            saveForwardConnected = saveState.ForwardConnection.SaveStatus;
            ForwardConnectedTarget = saveState.ForwardConnection.Target;
            RearConnected = saveState.BackwardConnection.Connected;
            saveRearConnected = saveState.BackwardConnection.SaveStatus;
            RearConnectedTarget = saveState.BackwardConnection.Target;
            (YAngle, TargetY) = saveState.Target;
        }

        private void InitializeAnglesAndTrackNodes()
        {
            TrackShape trackShape = RuntimeData.Instance.TSectionDat.TrackShapes[TrackShapeIndex];
            uint nSections = RuntimeData.Instance.TSectionDat.TrackShapes[TrackShapeIndex].SectionIndices[0].SectionsCount;
            trackNodesIndex = new int[RuntimeData.Instance.TSectionDat.TrackShapes[TrackShapeIndex].SectionIndices.Length];
            trackNodesOrientation = new bool[trackNodesIndex.Length];
            trackVectorSectionsIndex = new int[trackNodesIndex.Length];
            int i = 0;
            foreach (SectionIndex sectionIdx in trackShape.SectionIndices)
            {
                angles.Add(MathHelper.ToRadians((float)sectionIdx.AngularOffset));
                trackNodesIndex[i] = -1;
                trackVectorSectionsIndex[i] = -1;
                i++;
            }
            foreach (TrackVectorNode tvn in RuntimeData.Instance.TrackDB.TrackNodes.VectorNodes)
            {
                if (tvn.TrackVectorSections != null)
                {
                    int trackVectorSection = Array.FindIndex(tvn.TrackVectorSections, trVectorSection =>
                        trVectorSection.Location.TileX == WorldPosition.TileX && trVectorSection.Location.TileZ == WorldPosition.TileZ && trVectorSection.WorldFileUiD == UID);
                    if (trackVectorSection >= 0)
                        if (tvn.TrackVectorSections.Length > (int)nSections)
                        {
                            i = tvn.TrackVectorSections[trackVectorSection].Flag1 / 2;
                            trackNodesIndex[i] = tvn.Index;
                            trackVectorSectionsIndex[i] = trackVectorSection;
                            trackNodesOrientation[i] = tvn.TrackVectorSections[trackVectorSection].Flag1 % 2 == 0;

                        }
                }
            }
        }

        /// <summary>
        /// Computes the nearest turntable exit in the actual direction
        /// Returns the Y angle to be compared.
        /// </summary>
        public override void ComputeTarget(bool clockwise)
        {
            if (!ContinuousMotion)
                return;
            if (MultiPlayerManager.IsMultiPlayer())
            {
                MultiPlayerManager.Broadcast(new MovingTableMessage()
                {
                    MovingTableIndex = Simulator.Instance.MovingTables.IndexOf(Simulator.Instance.ActiveMovingTable),
                    MessageCode = MessageCode.GoToTarget,
                    Delta = YAngle,
                    MovingDirection = clockwise ? Rotation.Clockwise : Rotation.CounterClockwise
                });
            }
            RemotelyControlled = false;
            GeneralComputeTarget(clockwise);
        }

        public override void GeneralComputeTarget(bool clockwise)
        {
            if (!ContinuousMotion)
                return;
            ContinuousMotion = false;
            GoToTarget = false;
            RotationDirection = clockwise ? Rotation.Clockwise : Rotation.CounterClockwise;
            float targetThreshold = RemotelyControlled ? 0.2f : 0.1f;

            float forwardAngleDiff = (int)RotationDirection * 3.5f;
            float rearAngleDiff = (int)RotationDirection * 3.5f;
            ForwardConnected = false;
            RearConnected = false;
            if (angles.Count == 0)
            {
                RotationDirection = Rotation.None;
                ForwardConnectedTarget = -1;
                RearConnectedTarget = -1;
            }
            else
            {
                if (RotationDirection == Rotation.Clockwise)
                {
                    for (int i = angles.Count - 1; i >= 0; i--)
                        if (trackNodesIndex[i] != -1 && trackVectorSectionsIndex[i] != -1)
                        {
                            float angleDiff = MathHelper.WrapAngle(angles[i] + YAngle);
                            if (angleDiff < forwardAngleDiff && angleDiff >= 0)
                            {
                                ForwardConnectedTarget = i;
                                forwardAngleDiff = angleDiff;
                            }
                            angleDiff = MathHelper.WrapAngle(angles[i] + YAngle + (float)Math.PI);
                            if (angleDiff < rearAngleDiff && angleDiff >= 0)
                            {
                                RearConnectedTarget = i;
                                rearAngleDiff = angleDiff;
                            }
                        }
                    if (forwardAngleDiff < targetThreshold || rearAngleDiff < targetThreshold)
                    {
                        if (forwardAngleDiff < rearAngleDiff && Math.Abs(forwardAngleDiff - rearAngleDiff) > 0.01)
                            RearConnectedTarget = -1;
                        else if (forwardAngleDiff > rearAngleDiff && Math.Abs(forwardAngleDiff - rearAngleDiff) > 0.01)
                            ForwardConnectedTarget = -1;
                    }
                    else
                    {
                        RotationDirection = Rotation.None;
                        ForwardConnectedTarget = -1;
                        RearConnectedTarget = -1;
                    }
                }
                else if (RotationDirection == Rotation.CounterClockwise)
                {
                    for (int i = 0; i <= angles.Count - 1; i++)
                        if (trackNodesIndex[i] != -1 && trackVectorSectionsIndex[i] != -1)
                        {
                            float thisAngleDiff = MathHelper.WrapAngle(angles[i] + YAngle);
                            if (thisAngleDiff > forwardAngleDiff && thisAngleDiff <= 0)
                            {
                                ForwardConnectedTarget = i;
                                forwardAngleDiff = thisAngleDiff;
                            }
                            thisAngleDiff = MathHelper.WrapAngle(angles[i] + YAngle + (float)Math.PI);
                            if (thisAngleDiff > rearAngleDiff && thisAngleDiff <= 0)
                            {
                                RearConnectedTarget = i;
                                rearAngleDiff = thisAngleDiff;
                            }
                        }
                    if (forwardAngleDiff > -targetThreshold || rearAngleDiff > -targetThreshold)
                    {
                        if (forwardAngleDiff > rearAngleDiff && Math.Abs(forwardAngleDiff - rearAngleDiff) > 0.01)
                            RearConnectedTarget = -1;
                        else if (forwardAngleDiff < rearAngleDiff && Math.Abs(forwardAngleDiff - rearAngleDiff) > 0.01)
                            ForwardConnectedTarget = -1;
                    }
                    else
                    {
                        RotationDirection = Rotation.None;
                        ForwardConnectedTarget = -1;
                        RearConnectedTarget = -1;
                    }
                }
            }
            RemotelyControlled = false;
        }

        /// <summary>
        /// Starts continuous movement by player action
        /// </summary>
        public override void StartContinuous(bool clockwise)
        {
            if (TrainsOnMovingTable.Count == 1 && TrainsOnMovingTable[0].FrontOnBoard && TrainsOnMovingTable[0].BackOnBoard)
            {
                // Preparing for rotation
                Train train = TrainsOnMovingTable[0].Train;
                if (Math.Abs(train.SpeedMpS) > 0.1 || (train.LeadLocomotiveIndex != -1 && (train.LeadLocomotive.ThrottlePercent >= 1 || train.TrainType != TrainType.Remote && !(train.LeadLocomotive.Direction == MidpointDirection.N
                 || Math.Abs(train.MUReverserPercent) <= 1))) || (train.ControlMode != TrainControlMode.Manual && train.ControlMode != TrainControlMode.TurnTable &&
                 train.ControlMode != TrainControlMode.Explorer && train.ControlMode != TrainControlMode.Undefined))
                {
                    if (SendNotifications)
                        Simulator.Instance.Confirmer.Warning(Simulator.Catalog.GetString("Rotation can't start: check throttle, speed, direction and control mode"));
                    return;
                }
            }
            if (MultiPlayerManager.IsMultiPlayer())
            {
                MultiPlayerManager.Broadcast(new MovingTableMessage()
                {
                    MovingTableIndex = Simulator.Instance.MovingTables.IndexOf(Simulator.Instance.ActiveMovingTable),
                    MessageCode = MessageCode.StartingContinuous,
                    Delta = YAngle,
                    MovingDirection = clockwise ? Rotation.Clockwise : Rotation.CounterClockwise
                });
            }
            GeneralStartContinuous(clockwise);
        }

        public override void GeneralStartContinuous(bool clockwise)
        {
            if (MaxAngle > 0)
            {
                var positiveYAngle = YAngle >= 0 ? YAngle : YAngle + 2 * (float)Math.PI;
                if (RotationDirection == Rotation.CounterClockwise && positiveYAngle < 0.2 || RotationDirection == Rotation.Clockwise && positiveYAngle <= 2 * (float)Math.PI - MaxAngle && positiveYAngle > 0.2)
                {
                    RotationDirection = Rotation.None;
                    ContinuousMotion = false;
                    if (SendNotifications)
                        Simulator.Instance.Confirmer.Warning(Simulator.Catalog.GetString("Turntable is at its bound, can't rotate"));
                    return;
                }
            }
            if (TrainsOnMovingTable.Count > 1 || TrainsOnMovingTable.Count == 1 && TrainsOnMovingTable[0].FrontOnBoard ^ TrainsOnMovingTable[0].BackOnBoard)
            {
                RotationDirection = Rotation.None;
                ContinuousMotion = false;
                if (SendNotifications)
                    Simulator.Instance.Confirmer.Warning(Simulator.Catalog.GetString("Train partially on turntable, can't rotate"));
                return;
            }
            if (TrainsOnMovingTable.Count == 1 && TrainsOnMovingTable[0].FrontOnBoard && TrainsOnMovingTable[0].BackOnBoard)
            {
                // Preparing for rotation
                Train train = TrainsOnMovingTable[0].Train;
                if (train.ControlMode == TrainControlMode.Manual || train.ControlMode == TrainControlMode.Explorer || train.ControlMode == TrainControlMode.Undefined)
                {
                    ComputeTrainPosition(train);
                    train.ControlMode = TrainControlMode.TurnTable;
                }
                if (SendNotifications)
                    Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Turntable starting rotation with train"));

            }
            RotationDirection = clockwise ? Rotation.Clockwise : Rotation.CounterClockwise;
            ContinuousMotion = true;
        }

        // Computing position of cars relative to center of platform
        public void ComputeTrainPosition(Train train)
        {
            saveForwardConnected = ForwardConnected ^ !trackNodesOrientation[ConnectedTrackEnd];
            saveRearConnected = RearConnected;
            Matrix invAnimationXNAMatrix = Matrix.Invert(animationXNAMatrix);
            relativeCarPositions = new List<Matrix>();
            foreach (TrainCar trainCar in train?.Cars ?? throw new ArgumentNullException(nameof(train)))
            {
                trainCar.UpdateWorldPosition(trainCar.WorldPosition.NormalizeTo(WorldPosition.TileX, WorldPosition.TileZ));
                Matrix relativeCarPosition = Matrix.Multiply(trainCar.WorldPosition.XNAMatrix, invAnimationXNAMatrix);
                relativeCarPositions.Add(relativeCarPosition);
            }
            Vector3 XNALocation = train.FrontTDBTraveller.Location;
            XNALocation.Z = -XNALocation.Z;
            XNALocation.X += 2048 * (train.FrontTDBTraveller.TileX - WorldPosition.TileX);
            XNALocation.Z -= 2048 * (train.FrontTDBTraveller.TileZ - WorldPosition.TileZ);
            relativeFrontTravellerXNALocation = Vector3.Transform(XNALocation, invAnimationXNAMatrix);
            XNALocation = train.RearTDBTraveller.Location;
            XNALocation.Z = -XNALocation.Z;
            XNALocation.X += 2048 * (train.RearTDBTraveller.TileX - WorldPosition.TileX);
            XNALocation.Z -= 2048 * (train.RearTDBTraveller.TileZ - WorldPosition.TileZ);
            relativeRearTravellerXNALocation = Vector3.Transform(XNALocation, invAnimationXNAMatrix);
        }

        public void ComputeCenter(in WorldPosition worldPosition)
        {
            VectorExtension.Transform(offset, worldPosition.XNAMatrix, out Vector3 centerCoordinates);
            position = worldPosition.SetTranslation(centerCoordinates.X, centerCoordinates.Y, centerCoordinates.Z);
        }

        public void RotateTrain(Matrix animationXNAMatrix)
        {
            if ((RotationDirection != Rotation.None || GoToTarget || GoToAutoTarget) && TrainsOnMovingTable.Count == 1 && TrainsOnMovingTable[0].FrontOnBoard &&
                TrainsOnMovingTable[0].BackOnBoard && TrainsOnMovingTable[0].Train.ControlMode == TrainControlMode.TurnTable)
            {
                // Rotate together also train
                int relativeCarPositions = 0;
                foreach (TrainCar traincar in TrainsOnMovingTable[0].Train.Cars)
                {
                    if (base.relativeCarPositions != null)
                    {
                        traincar.UpdateWorldPosition(new WorldPosition(traincar.WorldPosition.TileX, traincar.WorldPosition.TileZ,
                        Matrix.Multiply(base.relativeCarPositions[relativeCarPositions], animationXNAMatrix)));
                        traincar.UpdateFreightAnimationDiscretePositions();
                        relativeCarPositions++;
                    }
                }
            }
        }

        public void AutoRotateTable(double elapsedClockSeconds)
        {
            GoToAutoTarget = true;

            double angleStep = (YAngle / Math.PI * 1800.0 + 3600) % 3600.0;
            float usedFrameRate = TurntableFrameRate ?? 30f;

            if (AutoRotationDirection == Rotation.Clockwise)
                angleStep -= elapsedClockSeconds * usedFrameRate;
            else if (AutoRotationDirection == Rotation.CounterClockwise)
                angleStep += elapsedClockSeconds * usedFrameRate;

            YAngle = TargetY = (float)MathHelperD.WrapAngle(angleStep / 1800.0 * Math.PI);
        }

        public override void Update()
        {
            foreach (TrainOnMovingTable trainOnTurntable in TrainsOnMovingTable)
            {
                if (trainOnTurntable.FrontOnBoard ^ trainOnTurntable.BackOnBoard)
                {
                    RotationDirection = Rotation.None;
                    ContinuousMotion = false;
                    return;
                }
            }

            if (ContinuousMotion)
            {
                ForwardConnected = false;
                RearConnected = false;
                ConnectedTrackEnd = -1;
                GoToTarget = false;
            }
            else
            {
                if (RotationDirection != Rotation.None || AutoRotationDirection != Rotation.None)
                {
                    ForwardConnected = false;
                    RearConnected = false;
                    if (ForwardConnectedTarget != -1)
                    {
                        if (Math.Abs(MathHelper.WrapAngle(angles[ForwardConnectedTarget] + YAngle)) < 0.005)
                        {
                            ForwardConnected = true;
                            GoToTarget = RotationDirection != Rotation.None;  // only set if not in auto mode
                            RotationDirection = Rotation.None;
                            AutoRotationDirection = Rotation.None;
                            ConnectedTrackEnd = ForwardConnectedTarget;
                            if (SendNotifications)
                                Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Turntable forward connected"));
                            TargetY = -angles[ForwardConnectedTarget];
                        }
                    }
                    else if (RearConnectedTarget != -1)
                    {
                        if (Math.Abs(MathHelper.WrapAngle(angles[RearConnectedTarget] + YAngle + (float)Math.PI)) < 0.005)
                        {
                            RearConnected = true;
                            GoToTarget = RotationDirection != Rotation.None;  // only set if not in auto mode
                            RotationDirection = Rotation.None;
                            AutoRotationDirection = Rotation.None;
                            ConnectedTrackEnd = RearConnectedTarget;
                            if (SendNotifications)
                                Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Turntable backward connected"));
                            TargetY = -MathHelper.WrapAngle(angles[RearConnectedTarget] + (float)Math.PI);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// TargetExactlyReached: if train on board, it can exit the turntable
        /// </summary>
        public void TargetExactlyReached()
        {
            Direction direction = ForwardConnected ? Direction.Forward : Direction.Backward;
            direction = saveForwardConnected ^ !trackNodesOrientation[ConnectedTrackEnd] ? direction : direction.Reverse();
            GoToTarget = false;
            if (TrainsOnMovingTable.Count == 1)
            {
                Train train = TrainsOnMovingTable[0].Train;
                if (train.ControlMode == TrainControlMode.TurnTable)
                    train.ReenterTrackSections(trackNodesIndex[ConnectedTrackEnd], finalFrontTravellerXNALocation, finalRearTravellerXNALocation, direction);
            }
        }

        /// <summary>
        /// CheckMovingTableAligned: checks if turntable aligned with entering train
        /// </summary>
        public override bool CheckMovingTableAligned(Train train, bool forward)
        {
            //Traveller.TravellerDirection direction;
            //if ((ForwardConnected || RearConnected) && trackVectorSectionsIndex[ConnectedTrackEnd] != -1 && trackNodesIndex[ConnectedTrackEnd] != -1 &&
            //    (trackNodesIndex[ConnectedTrackEnd] == train.FrontTDBTraveller.TN.Index || trackNodesIndex[ConnectedTrackEnd] == train.RearTDBTraveller.TN.Index))
            //{
            //    direction = ForwardConnected ? Traveller.TravellerDirection.Forward : Traveller.TravellerDirection.Backward;
            //    return true;
            //}
            //else
            //{
            //    direction = Traveller.TravellerDirection.Forward;
            //}
            //return false;
            ArgumentNullException.ThrowIfNull(train);

            return (ForwardConnected || RearConnected) && trackVectorSectionsIndex[ConnectedTrackEnd] != -1 && trackNodesIndex[ConnectedTrackEnd] != -1 &&
                (trackNodesIndex[ConnectedTrackEnd] == train.FrontTDBTraveller.TrackNode.Index || trackNodesIndex[ConnectedTrackEnd] == train.RearTDBTraveller.TrackNode.Index);
        }

        /// <summary>
        /// Check if train position is on turntable track section
        /// </summary>

        public bool CheckOnSection(Traveller trainPosition)
        {
            bool onTable = false;
            int nodeIndex = -1;

            for (int inode = 0; inode < trackNodesIndex.Length && nodeIndex == -1; inode++)
            {
                if (trackNodesIndex[inode] == trainPosition.TrackNode.Index)
                {
                    nodeIndex = inode;
                }
            }

            if (nodeIndex >= 0)
            {
                onTable = (trainPosition.TrackVectorSectionIndex == trackNodesIndex[nodeIndex]);
            }

            return (onTable);
        }

        /// <summary>
        /// PerformUpdateActions: actions to be performed at every animation step
        /// </summary>
        public void PerformUpdateActions(Matrix absAnimationMatrix)
        {
            RotateTrain(absAnimationMatrix);
            if ((GoToTarget || GoToAutoTarget) && TrainsOnMovingTable.Count == 1 && TrainsOnMovingTable[0].Train.ControlMode == TrainControlMode.TurnTable)
                RecalculateTravellerXNALocations(absAnimationMatrix);
            if (GoToTarget)
                TargetExactlyReached();
        }

        public (float startAngle, float endAngle) FindConnectingDirections(int requestedExit)
        {
            return (angles[ConnectedTrackEnd], angles[requestedExit]);
        }
    }
}
