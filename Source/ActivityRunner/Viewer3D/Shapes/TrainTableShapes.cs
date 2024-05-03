using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Microsoft.Xna.Framework;

using Orts.ActivityRunner.Viewer3D.Sound;
using Orts.Common;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts.Models;
using Orts.Simulation;
using Orts.Simulation.World;

using SharpDX.Direct3D9;

namespace Orts.ActivityRunner.Viewer3D.Shapes
{
    public class TurntableShape : PoseableShape
    {
        private double animationKey;  // advances with time
        private readonly TurnTable turntable; // linked turntable data
        private readonly SoundSource Sound;
        private bool rotating;
        private readonly int animationMatrixIndex = -1; // index of animation matrix

        /// <summary>
        /// Construct and initialize the class
        /// </summary>
        public TurntableShape(string path, IWorldPosition positionSource, ShapeFlags flags, TurnTable turntable, double startingY)
            : base(path, positionSource, flags)
        {
            this.turntable = turntable;
            //Turntable.StartingY = (float)startingY;
            this.turntable.TurntableFrameRate = SharedShape.Animations[0].FrameRate;
            animationKey = (this.turntable.YAngle / (float)Math.PI * 1800.0f + 3600) % 3600.0f;
            for (var imatrix = 0; imatrix < SharedShape.Matrices.Length; ++imatrix)
            {
                if (SharedShape.MatrixNames[imatrix].Equals(turntable.Animations[0], StringComparison.OrdinalIgnoreCase))
                {
                    animationMatrixIndex = imatrix;
                    break;
                }
            }
            if (viewer.Simulator.Route.DefaultTurntableSMS != null)
            {
                string soundPath = Simulator.Instance.RouteFolder.SoundFile(Simulator.Instance.Route.DefaultTurntableSMS);
                if (File.Exists(soundPath))
                {
                    Sound = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.Turntable, soundPath);
                    viewer.SoundProcess.AddSoundSource(this, Sound);
                }
                else if (File.Exists(soundPath = Simulator.Instance.RouteFolder.ContentFolder.SoundFile(Simulator.Instance.Route.DefaultTurntableSMS)))
                {
                    Sound = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.Turntable, soundPath);
                    viewer.SoundProcess.AddSoundSource(this, Sound);
                }
                else
                {
                    Trace.WriteLine($"Turntable soundfile {soundPath} not found");
                }
            }
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, animationKey);

            MatrixExtension.Multiply(in XNAMatrices[animationMatrixIndex], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            this.turntable.ReInitTrainPositions(absAnimationMatrix);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            double nextKey;
            if (turntable.AlignToRemote)
            {
                animationKey = (turntable.YAngle / (float)Math.PI * 1800.0f + 3600) % 3600.0f;
                if (animationKey < 0)
                    animationKey += SharedShape.Animations[0].FrameCount;
                turntable.AlignToRemote = false;
            }
            else
            {
                if (turntable.GoToTarget || turntable.GoToAutoTarget)
                {
                    nextKey = turntable.TargetY / MathHelper.TwoPi * SharedShape.Animations[0].FrameCount;
                }
                else
                {
                    var moveFrames = turntable.RotationDirection switch
                    {
                        Rotation.CounterClockwise => SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds,
                        Rotation.Clockwise => -SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds,
                        _ => 0,
                    };
                    nextKey = animationKey + moveFrames;
                }
                animationKey = nextKey % SharedShape.Animations[0].FrameCount;
                if (animationKey < 0)
                    animationKey += SharedShape.Animations[0].FrameCount;
                // used if Turntable cannot turn 360 degrees
                if (turntable.MaxAngle > 0 && animationKey != 0)
                {
                    if (animationKey < -SharedShape.Animations[0].FrameCount * turntable.MaxAngle / (2 * Math.PI) + SharedShape.Animations[0].FrameCount)
                    {
                        if (animationKey > 20)
                            animationKey = -SharedShape.Animations[0].FrameCount * turntable.MaxAngle / (float)(2 * Math.PI) + SharedShape.Animations[0].FrameCount;
                        else
                            animationKey = 0;
                    }
                }
                turntable.YAngle = MathHelper.WrapAngle((float)(nextKey / SharedShape.Animations[0].FrameCount * MathHelper.TwoPi));

                if ((turntable.RotationDirection != Rotation.None || turntable.AutoRotationDirection != Rotation.None) && !rotating)
                {
                    rotating = true;
                    Sound?.HandleEvent(turntable.TrainsOnMovingTable.Count == 1 &&
                        turntable.TrainsOnMovingTable[0].FrontOnBoard && turntable.TrainsOnMovingTable[0].BackOnBoard ? TrainEvent.MovingTableMovingLoaded : TrainEvent.MovingTableMovingEmpty);
                }
                else if (turntable.RotationDirection == Rotation.None && turntable.AutoRotationDirection == Rotation.None && rotating)
                {
                    rotating = false;
                    Sound?.HandleEvent(TrainEvent.MovingTableStopped);
                }
            }
            // Update the pose for each matrix
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, animationKey);

            MatrixExtension.Multiply(in XNAMatrices[animationMatrixIndex], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            turntable.PerformUpdateActions(absAnimationMatrix);
            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }
    }

    public class TransfertableShape : PoseableShape
    {
        private double animationKey;  // advances with time
        private readonly TransferTable transfertable; // linked turntable data
        private readonly SoundSource Sound;
        private bool translating;
        private readonly int animationMatrixIndex = -1; // index of animation matrix

        /// <summary>
        /// Construct and initialize the class
        /// </summary>
        public TransfertableShape(string path, IWorldPosition positionSource, ShapeFlags flags, TransferTable transfertable)
            : base(path, positionSource, flags)
        {
            this.transfertable = transfertable;
            animationKey = (this.transfertable.OffsetPos - this.transfertable.CenterOffsetComponent) / this.transfertable.Span * SharedShape.Animations[0].FrameCount;
            for (var imatrix = 0; imatrix < SharedShape.Matrices.Length; ++imatrix)
            {
                if (SharedShape.MatrixNames[imatrix].Equals(transfertable.Animations[0], StringComparison.OrdinalIgnoreCase))
                {
                    animationMatrixIndex = imatrix;
                    break;
                }
            }
            if (Simulator.Instance.Route.DefaultTurntableSMS != null)
            {
                string soundPath = Simulator.Instance.RouteFolder.SoundFile(Simulator.Instance.Route.DefaultTurntableSMS);
                if (File.Exists(soundPath))
                {
                    Sound = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.Turntable, soundPath);
                    viewer.SoundProcess.AddSoundSource(this, Sound);
                }
                else if (File.Exists(soundPath = Simulator.Instance.RouteFolder.ContentFolder.SoundFile(Simulator.Instance.Route.DefaultTurntableSMS)))
                {
                    Sound = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.Turntable, soundPath);
                    viewer.SoundProcess.AddSoundSource(this, Sound);
                }
                else
                {
                    Trace.WriteLine($"Turntable soundfile {soundPath} not found");
                }
            }
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, animationKey);

            MatrixExtension.Multiply(in XNAMatrices[animationMatrixIndex], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            this.transfertable.ReInitTrainPositions(absAnimationMatrix);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var animation = SharedShape.Animations[0];
            if (transfertable.AlignToRemote)
            {
                animationKey = (transfertable.OffsetPos - transfertable.CenterOffsetComponent) / transfertable.Span * SharedShape.Animations[0].FrameCount;
                if (animationKey < 0)
                    animationKey = 0;
                transfertable.AlignToRemote = false;
            }
            else
            {
                if (transfertable.GoToTarget)
                {
                    animationKey = (transfertable.TargetOffset - transfertable.CenterOffset.X) / transfertable.Span * SharedShape.Animations[0].FrameCount;
                }

                else if (transfertable.MotionDirection == MidpointDirection.Forward)
                {
                    animationKey += SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds;
                }
                else if (transfertable.MotionDirection == MidpointDirection.Reverse)
                {
                    animationKey -= SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds;
                }
                if (animationKey > SharedShape.Animations[0].FrameCount)
                    animationKey = SharedShape.Animations[0].FrameCount;
                if (animationKey < 0)
                    animationKey = 0;

                transfertable.OffsetPos = (float)animationKey / SharedShape.Animations[0].FrameCount * transfertable.Span + transfertable.CenterOffset.X;

                if (transfertable.MotionDirection != MidpointDirection.N && !translating)
                {
                    translating = true;
                    Sound?.HandleEvent(transfertable.TrainsOnMovingTable.Count == 1 &&
                        transfertable.TrainsOnMovingTable[0].FrontOnBoard && transfertable.TrainsOnMovingTable[0].BackOnBoard ? TrainEvent.MovingTableMovingLoaded : TrainEvent.MovingTableMovingEmpty);
                }
                else if (transfertable.MotionDirection == MidpointDirection.N && translating)
                {
                    translating = false;
                    Sound?.HandleEvent(TrainEvent.MovingTableStopped);
                }
            }
            // Update the pose for each matrix
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, animationKey);

            MatrixExtension.Multiply(in XNAMatrices[animationMatrixIndex], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            transfertable.PerformUpdateActions(absAnimationMatrix, WorldPosition);
            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }
    }

}
