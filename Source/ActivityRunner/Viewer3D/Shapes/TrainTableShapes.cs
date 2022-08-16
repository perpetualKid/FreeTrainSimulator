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
        protected double animationKey;  // advances with time
        protected TurnTable Turntable; // linked turntable data
        private readonly SoundSource Sound;
        private bool Rotating;
        protected int IAnimationMatrix = -1; // index of animation matrix

        /// <summary>
        /// Construct and initialize the class
        /// </summary>
        public TurntableShape(string path, IWorldPosition positionSource, ShapeFlags flags, TurnTable turntable, double startingY)
            : base(path, positionSource, flags)
        {
            Turntable = turntable;
            //Turntable.StartingY = (float)startingY;
            Turntable.TurntableFrameRate = SharedShape.Animations[0].FrameRate;
            animationKey = (Turntable.YAngle / (float)Math.PI * 1800.0f + 3600) % 3600.0f;
            for (var imatrix = 0; imatrix < SharedShape.Matrices.Length; ++imatrix)
            {
                if (SharedShape.MatrixNames[imatrix].Equals(turntable.Animations[0], StringComparison.OrdinalIgnoreCase))
                {
                    IAnimationMatrix = imatrix;
                    break;
                }
            }
            if (viewer.Simulator.Route.DefaultTurntableSMS != null)
            {
                string soundPath = Simulator.Instance.RouteFolder.SoundFile(Simulator.Instance.Route.DefaultTurntableSMS);
                if (File.Exists(soundPath))
                {
                    Sound = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.Turntable, soundPath);
                    viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { Sound });
                }
                else if (File.Exists(soundPath = Simulator.Instance.RouteFolder.ContentFolder.SoundFile(Simulator.Instance.Route.DefaultTurntableSMS)))
                {
                    Sound = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.Turntable, soundPath);
                    viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { Sound });
                }
                else
                {
                    Trace.WriteLine($"Turntable soundfile {soundPath} not found");
                }
            }
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, animationKey);

            MatrixExtension.Multiply(in XNAMatrices[IAnimationMatrix], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            Turntable.ReInitTrainPositions(absAnimationMatrix);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            double nextKey;
            if (Turntable.AlignToRemote)
            {
                animationKey = (Turntable.YAngle / (float)Math.PI * 1800.0f + 3600) % 3600.0f;
                if (animationKey < 0)
                    animationKey += SharedShape.Animations[0].FrameCount;
                Turntable.AlignToRemote = false;
            }
            else
            {
                if (Turntable.GoToTarget || Turntable.GoToAutoTarget)
                {
                    nextKey = Turntable.TargetY / MathHelper.TwoPi * SharedShape.Animations[0].FrameCount;
                }
                else
                {
                    var moveFrames = Turntable.RotationDirection switch
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
                Turntable.YAngle = MathHelper.WrapAngle((float)(nextKey / SharedShape.Animations[0].FrameCount * MathHelper.TwoPi));

                if ((Turntable.RotationDirection != Rotation.None || Turntable.AutoRotationDirection != Rotation.None) && !Rotating)
                {
                    Rotating = true;
                    Sound?.HandleEvent(Turntable.TrainsOnMovingTable.Count == 1 &&
                        Turntable.TrainsOnMovingTable[0].FrontOnBoard && Turntable.TrainsOnMovingTable[0].BackOnBoard ? TrainEvent.MovingTableMovingLoaded : TrainEvent.MovingTableMovingEmpty);
                }
                else if (Turntable.RotationDirection == Rotation.None && Turntable.AutoRotationDirection == Rotation.None && Rotating)
                {
                    Rotating = false;
                    Sound?.HandleEvent(TrainEvent.MovingTableStopped);
                }
            }
            // Update the pose for each matrix
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, animationKey);

            MatrixExtension.Multiply(in XNAMatrices[IAnimationMatrix], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            Turntable.PerformUpdateActions(absAnimationMatrix);
            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }
    }

    public class TransfertableShape : PoseableShape
    {
        protected double animationKey;  // advances with time
        protected TransferTable Transfertable; // linked turntable data
        private readonly SoundSource Sound;
        private bool Translating;
        protected int IAnimationMatrix = -1; // index of animation matrix

        /// <summary>
        /// Construct and initialize the class
        /// </summary>
        public TransfertableShape(string path, IWorldPosition positionSource, ShapeFlags flags, TransferTable transfertable)
            : base(path, positionSource, flags)
        {
            Transfertable = transfertable;
            animationKey = (Transfertable.OffsetPos - Transfertable.CenterOffsetComponent) / Transfertable.Span * SharedShape.Animations[0].FrameCount;
            for (var imatrix = 0; imatrix < SharedShape.Matrices.Length; ++imatrix)
            {
                if (SharedShape.MatrixNames[imatrix].Equals(transfertable.Animations[0], StringComparison.OrdinalIgnoreCase))
                {
                    IAnimationMatrix = imatrix;
                    break;
                }
            }
            if (Simulator.Instance.Route.DefaultTurntableSMS != null)
            {
                string soundPath = Simulator.Instance.RouteFolder.SoundFile(Simulator.Instance.Route.DefaultTurntableSMS);
                if (File.Exists(soundPath))
                {
                    Sound = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.Turntable, soundPath);
                    viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { Sound });
                }
                else if (File.Exists(soundPath = Simulator.Instance.RouteFolder.ContentFolder.SoundFile(Simulator.Instance.Route.DefaultTurntableSMS)))
                {
                    Sound = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.Turntable, soundPath);
                    viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { Sound });
                }
                else
                {
                    Trace.WriteLine($"Turntable soundfile {soundPath} not found");
                }
            }
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, animationKey);

            MatrixExtension.Multiply(in XNAMatrices[IAnimationMatrix], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            Transfertable.ReInitTrainPositions(absAnimationMatrix);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            var animation = SharedShape.Animations[0];
            if (Transfertable.AlignToRemote)
            {
                animationKey = (Transfertable.OffsetPos - Transfertable.CenterOffsetComponent) / Transfertable.Span * SharedShape.Animations[0].FrameCount;
                if (animationKey < 0)
                    animationKey = 0;
                Transfertable.AlignToRemote = false;
            }
            else
            {
                if (Transfertable.GoToTarget)
                {
                    animationKey = (Transfertable.TargetOffset - Transfertable.CenterOffset.X) / Transfertable.Span * SharedShape.Animations[0].FrameCount;
                }

                else if (Transfertable.MotionDirection == MidpointDirection.Forward)
                {
                    animationKey += SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds;
                }
                else if (Transfertable.MotionDirection == MidpointDirection.Reverse)
                {
                    animationKey -= SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds;
                }
                if (animationKey > SharedShape.Animations[0].FrameCount)
                    animationKey = SharedShape.Animations[0].FrameCount;
                if (animationKey < 0)
                    animationKey = 0;

                Transfertable.OffsetPos = (float)animationKey / SharedShape.Animations[0].FrameCount * Transfertable.Span + Transfertable.CenterOffset.X;

                if (Transfertable.MotionDirection != MidpointDirection.N && !Translating)
                {
                    Translating = true;
                    Sound?.HandleEvent(Transfertable.TrainsOnMovingTable.Count == 1 &&
                        Transfertable.TrainsOnMovingTable[0].FrontOnBoard && Transfertable.TrainsOnMovingTable[0].BackOnBoard ? TrainEvent.MovingTableMovingLoaded : TrainEvent.MovingTableMovingEmpty);
                }
                else if (Transfertable.MotionDirection == MidpointDirection.N && Translating)
                {
                    Translating = false;
                    Sound?.HandleEvent(TrainEvent.MovingTableStopped);
                }
            }
            // Update the pose for each matrix
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, animationKey);

            MatrixExtension.Multiply(in XNAMatrices[IAnimationMatrix], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            Transfertable.PerformUpdateActions(absAnimationMatrix, WorldPosition);
            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }
    }

}
