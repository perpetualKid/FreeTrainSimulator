using System;

using MemoryPack;

using Microsoft.Xna.Framework;

using Orts.Common;
using Orts.Simulation.World;

namespace Orts.Simulation.Multiplayer.Messaging
{
    [MemoryPackable]
    public sealed partial class MovingTableMessage : MultiPlayerMessageContent
    {
        public int MovingTableIndex { get; set; }
        public MovingTable.MovingTableAction MessageCode { get; set; }
        public Rotation MovingDirection { get; set; }
        public float Delta { get; set; }

        public override void HandleMessage()
        {
            Simulator.Instance.ActiveMovingTable = Simulator.Instance.MovingTables[MovingTableIndex];
            if (Simulator.Instance.ActiveMovingTable is TurnTable turntable)
            {
                switch (MessageCode)
                {
                    case MovingTable.MovingTableAction.GoToTarget:
                        turntable.RemotelyControlled = true;
                        if (Math.Abs(MathHelper.WrapAngle(turntable.YAngle - Delta)) > 0.2f)
                        {
                            turntable.YAngle = Delta;
                            turntable.TargetY = Delta;
                            turntable.AlignToRemote = true;
                        }
                        turntable.GeneralComputeTarget(MovingDirection == Rotation.Clockwise);
                        break;
                    case MovingTable.MovingTableAction.StartingContinuous:
                        turntable.YAngle = Delta;
                        turntable.TargetY = Delta;
                        turntable.AlignToRemote = true;
                        turntable.GeneralStartContinuous(MovingDirection == Rotation.Clockwise);
                        break;
                    default:
                        break;
                }
            }
            else if (Simulator.Instance.ActiveMovingTable is TransferTable transfertable)
            {
                switch (MessageCode)
                {
                    case MovingTable.MovingTableAction.GoToTarget:
                        transfertable.RemotelyControlled = true;
                        if (Math.Abs(transfertable.OffsetPos - Delta) > 2.8f)
                        {
                            transfertable.OffsetPos = Delta;
                            transfertable.TargetOffset = Delta;
                            transfertable.AlignToRemote = true;
                        }
                        transfertable.GeneralComputeTarget(MovingDirection == Rotation.Clockwise);
                        break;
                    case MovingTable.MovingTableAction.StartingContinuous:
                        transfertable.OffsetPos = Delta;
                        transfertable.TargetOffset = Delta;
                        transfertable.AlignToRemote = true;
                        transfertable.GeneralStartContinuous(MovingDirection == Rotation.Clockwise);
                        break;
                    default:
                        break;
                }
            }
        }
    }
}
