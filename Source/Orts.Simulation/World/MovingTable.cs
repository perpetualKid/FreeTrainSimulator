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
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using FreeTrainSimulator.Common.Api;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Common.Position;
using Orts.Formats.Msts.Parsers;
using Orts.Models.State;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;

namespace Orts.Simulation.World
{
    /// <summary>
    /// Reads file ORTSTurntables.dat and creates the instances of turntables and transfertables
    /// </summary>
    public static class MovingTableFile
    {
        public static IEnumerable<MovingTable> ReadTurntableFile(string filePath)
        {
            List<MovingTable> result = new List<MovingTable>();
            if (!File.Exists(filePath))
                return result;

            Trace.Write(" TURNTBL");

            using (STFReader stf = new STFReader(filePath, false))
            {
                int count = stf.ReadInt(null);
                stf.ParseBlock(new STFReader.TokenProcessor[] {
                    new STFReader.TokenProcessor("turntable", ()=>{
                        if (--count < 0)
                            STFException.TraceWarning(stf, "Skipped extra Turntable");
                        else
                            result.Add(new TurnTable(stf));
                    }),
                    new STFReader.TokenProcessor("transfertable", ()=>{
                        if (--count < 0)
                            STFException.TraceWarning(stf, "Skipped extra Transfertable");
                        else
                            result.Add(new TransferTable(stf));
                    }),
                });
                if (count > 0)
                    STFException.TraceWarning(stf, count + " missing Turntable(s)");
            }

            return result;
        }
    }

    public abstract class MovingTable : ISaveStateApi<MovingTableSaveState>
    {
        public enum MovingTableAction
        {
            GoToTarget,
            StartingContinuous,
        }
        
        // Fixed data
        public string WFile { get; protected set; }
        public int UID { get; protected set; }
        public float Length { get; protected set; }

        private protected int[] trackNodesIndex;
        private protected int[] trackVectorSectionsIndex;
        private protected bool[] trackNodesOrientation; // true if forward, false if backward

        public int TrackShapeIndex { get; protected set; }

        private protected WorldPosition position = WorldPosition.None;
        // Dynamic data
        public ref readonly WorldPosition WorldPosition => ref position;
        public Collection<string> Animations { get; } = new Collection<string>();
        private protected Vector3 offset;
        public ref readonly Vector3 CenterOffset => ref offset; // shape offset of center of moving table;
        public bool ContinuousMotion { get; protected set; } // continuous motion on
        public int ConnectedTrackEnd { get; protected set; }  // 
        public bool GoToTarget { get; set; } //TODO 2021-06-21 should be protected set but TTTurnTable needs this
        public bool GoToAutoTarget { get; set; }
        public int? TurntableFrameRate { get; set; }
        public bool SendNotifications { get; set; } = true;      // send simulator confirmations
        public bool InUse { get; set; }                  // turntable is in use (used in auto mode for timetable)
        public Queue<int> WaitingTrains { get; private set; } = new Queue<int>();    // Queue of trains waiting to access table

        // additions to manage rotation or transfer of wagons
#pragma warning disable CA1002 // Do not expose generic lists
        public List<TrainOnMovingTable> TrainsOnMovingTable { get; } = new List<TrainOnMovingTable>(); // List of trains on turntable or transfertable
#pragma warning restore CA1002 // Do not expose generic lists
        private protected Matrix animationXNAMatrix = Matrix.Identity;
        private protected List<Matrix> relativeCarPositions;
        private protected Vector3 relativeFrontTravellerXNALocation;
        private protected Vector3 relativeRearTravellerXNALocation;
        private protected Vector3 finalFrontTravellerXNALocation;
        private protected Vector3 finalRearTravellerXNALocation;

        public bool AlignToRemote { get; set; }
        public bool RemotelyControlled { get; set; }

        public virtual ValueTask<MovingTableSaveState> Snapshot()
        {
            return ValueTask.FromResult(new MovingTableSaveState()
            {
                Index = UID,
                ContinousMotion = ContinuousMotion,
                MoveToTarget = GoToTarget,
                MoveToAutoTarget = GoToAutoTarget,
                TurntableFrameRate = TurntableFrameRate,
                ConnectedTrackEnd = ConnectedTrackEnd,
                SendNotifications = SendNotifications,
                Used = InUse,
                RelativeFrontTraveller = relativeFrontTravellerXNALocation,
                RelativeRearTraveller = relativeRearTravellerXNALocation,
                FinalFrontTraveller = finalFrontTravellerXNALocation,
                FinalRearTraveller = finalRearTravellerXNALocation,
                TrainsOnTable = new Collection<TrainOnTableItem>(TrainsOnMovingTable.Select((item) => new TrainOnTableItem(item.Train.Number, item.FrontOnBoard, item.BackOnBoard)).ToList()),
                WaitingTrains = new Queue<int>(WaitingTrains),
            });
        }

        public virtual ValueTask Restore(MovingTableSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            ContinuousMotion = saveState.ContinousMotion;
            GoToTarget = saveState.MoveToTarget;
            GoToAutoTarget = saveState.MoveToAutoTarget;
            TurntableFrameRate = saveState.TurntableFrameRate;
            ConnectedTrackEnd = saveState.ConnectedTrackEnd;
            SendNotifications = saveState.SendNotifications;
            InUse = saveState.Used;
            relativeFrontTravellerXNALocation = saveState.RelativeFrontTraveller;
            relativeRearTravellerXNALocation = saveState.RelativeRearTraveller;
            finalFrontTravellerXNALocation = saveState.FinalFrontTraveller;
            finalRearTravellerXNALocation = saveState.FinalRearTraveller;
            TrainsOnMovingTable.Clear();
            TrainsOnMovingTable.AddRange(saveState.TrainsOnTable.Select((item) => new TrainOnMovingTable(item.TrainNumber, item.FrontOnBoard, item.RearOnBoard)));
            WaitingTrains = new Queue<int>(saveState.WaitingTrains);
            return ValueTask.CompletedTask;
        }

        public virtual void Update()
        {

        }

        public virtual bool CheckMovingTableAligned(Train train, bool forward)
        {
            return false;
        }

        /// <summary>
        /// CheckTrainOnTurntable: checks if actual player train is on turntable
        /// </summary>
        public bool CheckTrainOnMovingTable(Train train)
        {
            if (train == null)
                return false;
            string tableType = this is TurnTable ? Simulator.Catalog.GetString("turntable") : Simulator.Catalog.GetString("transfertable");
            int trainIndex = (TrainsOnMovingTable as List<TrainOnMovingTable>)?.FindIndex(x => x.Train.Number == train.Number) ?? -1;
            if (WorldLocation.Within(train.FrontTDBTraveller.WorldLocation, WorldPosition.WorldLocation, Length / 2))
            {
                if (trainIndex == -1 || !TrainsOnMovingTable[trainIndex].FrontOnBoard)
                {
                    if (trainIndex == -1)
                    {
                        TrainOnMovingTable trainOnTurntable = new TrainOnMovingTable(train);
                        trainIndex = TrainsOnMovingTable.Count;
                        TrainsOnMovingTable.Add(trainOnTurntable);
                    }
                    if (!TrainsOnMovingTable[trainIndex].BackOnBoard)
                    {
                        // check if turntable aligned with train
                        bool aligned = CheckMovingTableAligned(train, true);
                        if (!aligned)
                        {
                            TrainsOnMovingTable[trainIndex].SetFrontState(true);
                            Simulator.Instance.Confirmer.Warning(Simulator.Catalog.GetString("Train slipped into non aligned {0}", tableType));
                            train.SetTrainOutOfControl(OutOfControlReason.SlippedIntoTurnTable);
                            train.SpeedMpS = 0;
                            foreach (TrainCar car in train.Cars)
                                car.SpeedMpS = 0;
                            return false;
                        }
                    }
                    if (SendNotifications)
                        Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Train front on {0}", tableType));
                }
                TrainsOnMovingTable[trainIndex].SetFrontState(true);
            }
            else
                if (trainIndex != -1 && TrainsOnMovingTable[trainIndex].FrontOnBoard)
            {
                if (SendNotifications) 
                    Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Train front outside {0}", tableType));
                if (TrainsOnMovingTable[trainIndex].BackOnBoard) 
                    TrainsOnMovingTable[trainIndex].SetFrontState(false);
                else
                {
                    TrainsOnMovingTable.RemoveAt(trainIndex);
                    trainIndex = -1;
                }
            }
            if (WorldLocation.Within(train.RearTDBTraveller.WorldLocation, WorldPosition.WorldLocation, Length / 2))
            {
                if (trainIndex == -1 || !TrainsOnMovingTable[trainIndex].BackOnBoard)
                {
                    if (trainIndex == -1)
                    {
                        TrainOnMovingTable trainOnTurntable = new TrainOnMovingTable(train);
                        trainIndex = TrainsOnMovingTable.Count;
                        TrainsOnMovingTable.Add(trainOnTurntable);
                    }
                    if (!TrainsOnMovingTable[trainIndex].FrontOnBoard)
                    {
                        // check if turntable aligned with train
                        bool aligned = CheckMovingTableAligned(train, false);
                        if (!aligned)
                        {
                            TrainsOnMovingTable[trainIndex].SetBackState(true);
                            Simulator.Instance.Confirmer.Warning(Simulator.Catalog.GetString("Train slipped into non aligned {0}", tableType));
                            train.SetTrainOutOfControl(OutOfControlReason.SlippedIntoTurnTable);
                            train.SpeedMpS = 0;
                            foreach (TrainCar car in train.Cars) 
                                car.SpeedMpS = 0;
                            return false;
                        }
                    }
                    Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Train rear on {0}", tableType));
                }
                TrainsOnMovingTable[trainIndex].SetBackState(true);
            }
            else
                if (trainIndex != -1 && TrainsOnMovingTable[trainIndex].BackOnBoard)
            {
                if (SendNotifications) 
                    Simulator.Instance.Confirmer.Information(Simulator.Catalog.GetString("Train rear outside {0}", tableType));
                if (TrainsOnMovingTable[trainIndex].FrontOnBoard) 
                    TrainsOnMovingTable[trainIndex].SetBackState(false);
                else
                {
                    TrainsOnMovingTable.RemoveAt(trainIndex);
                    trainIndex = -1;
                }
            }
            if (Simulator.Instance.ActivityRun != null && !train.IsPathless && train.TrainType != TrainType.Static && trainIndex != -1 &&
                TrainsOnMovingTable[trainIndex].FrontOnBoard && TrainsOnMovingTable[trainIndex].BackOnBoard && train.SpeedMpS <= 0.1f && train.ControlMode != TrainControlMode.Manual &&
                train.TCRoute.ActiveSubPath == train.TCRoute.TCRouteSubpaths.Count - 1 && train.TCRoute.TCRouteSubpaths[train.TCRoute.ActiveSubPath].Count > 1 &&
                (train.PresentPosition[Direction.Forward].RouteListIndex == train.TCRoute.TCRouteSubpaths[train.TCRoute.ActiveSubPath].Count - 2 ||
                train.PresentPosition[Direction.Backward].RouteListIndex == train.TCRoute.TCRouteSubpaths[train.TCRoute.ActiveSubPath].Count - 2))
                train.IsPathless = true;
            return false;
        }

        public virtual void StartContinuous(bool clockwise)
        {

        }

        public virtual void ComputeTarget(bool clockwise)
        {

        }

        public abstract void GeneralComputeTarget(bool clockwise);
        public abstract void GeneralStartContinuous(bool clockwise);

        public void ReInitTrainPositions(Matrix animationXNAMatrix)
        {
            this.animationXNAMatrix = animationXNAMatrix;
            if (this == Simulator.Instance.ActiveMovingTable && TrainsOnMovingTable.Count == 1)
            {
                Train train = TrainsOnMovingTable[0].Train;
                if (TrainsOnMovingTable[0].FrontOnBoard && TrainsOnMovingTable[0].BackOnBoard && Math.Abs(train.SpeedMpS) < 0.1)
                {
                    Matrix invAnimationXNAMatrix = Matrix.Invert(this.animationXNAMatrix);
                    relativeCarPositions = new List<Matrix>();
                    foreach (TrainCar trainCar in train.Cars)
                    {
                        trainCar.UpdateWorldPosition(trainCar.WorldPosition.NormalizeTo(WorldPosition.TileX, WorldPosition.TileZ));
                        Matrix relativeCarPosition = Matrix.Multiply(trainCar.WorldPosition.XNAMatrix, invAnimationXNAMatrix);
                        relativeCarPositions.Add(relativeCarPosition);
                    }

                }
            }
        }

        public void RecalculateTravellerXNALocations(Matrix animationXNAMatrix)
        {
            finalFrontTravellerXNALocation = Vector3.Transform(relativeFrontTravellerXNALocation, animationXNAMatrix);
            finalRearTravellerXNALocation = Vector3.Transform(relativeRearTravellerXNALocation, animationXNAMatrix);
        }

        public int FindExitNode(int trackNodeIndex)
        {
            for (int i = 0; i < trackNodesIndex.Length; i++)
            {
                if (trackNodesIndex[i] == trackNodeIndex)
                {
                    return i;
                }
            }
            return -1;
        }

        public bool TrackNodeOrientation(int trackNodeIndex)
        {
            return trackNodesOrientation[trackNodeIndex];
        }
    }

    public class TrainOnMovingTable
    {
        public Train Train { get; private set; }
        public bool FrontOnBoard { get; private set; }
        public bool BackOnBoard { get; private set; }

        public TrainOnMovingTable(Train train)
        {
            Train = train;
        }

        public TrainOnMovingTable(int trainNumber, bool frontOnBoard, bool rearOnBoard, Train train = null)
        {
            Train = train ?? Simulator.Instance.Trains.GetTrainByNumber(trainNumber);
            FrontOnBoard = frontOnBoard;
            BackOnBoard = rearOnBoard;
        }

        public void SetFrontState(bool frontOnBoard)
        {
            FrontOnBoard = frontOnBoard;
        }

        public void SetBackState(bool backOnBoard)
        {
            BackOnBoard = backOnBoard;
        }
    }
}
