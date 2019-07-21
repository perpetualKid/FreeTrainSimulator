using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Xna.Framework;
using Orts.Common;
using Orts.Common.Xna;
using Orts.Simulation;

namespace Orts.ActivityRunner.Viewer3D.Shapes
{
    public class TurntableShape : PoseableShape
    {
        protected float AnimationKey;  // advances with time
        protected Turntable Turntable; // linked turntable data
        readonly SoundSource Sound;
        bool Rotating = false;
        protected int IAnimationMatrix = -1; // index of animation matrix

        /// <summary>
        /// Construct and initialize the class
        /// </summary>
        public TurntableShape(string path, IWorldPosition positionSource, ShapeFlags flags, Turntable turntable, double startingY)
            : base(path, positionSource, flags)
        {
            Turntable = turntable;
            Turntable.StartingY = (float)startingY;
            AnimationKey = (Turntable.YAngle / (float)Math.PI * 1800.0f + 3600) % 3600.0f;
            for (var imatrix = 0; imatrix < SharedShape.Matrices.Length; ++imatrix)
            {
                if (SharedShape.MatrixNames[imatrix].ToLower() == turntable.Animations[0].ToLower())
                {
                    IAnimationMatrix = imatrix;
                    break;
                }
            }
            if (viewer.Simulator.TRK.Tr_RouteFile.DefaultTurntableSMS != null)
            {
                var soundPath = viewer.Simulator.RoutePath + @"\\sound\\" + viewer.Simulator.TRK.Tr_RouteFile.DefaultTurntableSMS;
                try
                {
                    Sound = new SoundSource(viewer, WorldPosition.WorldLocation, Events.Source.ORTSTurntable, soundPath);
                    viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { Sound });
                }
                catch
                {
                    soundPath = viewer.Simulator.BasePath + @"\\sound\\" + viewer.Simulator.TRK.Tr_RouteFile.DefaultTurntableSMS;
                    try
                    {
                        Sound = new SoundSource(viewer, WorldPosition.WorldLocation, Events.Source.ORTSTurntable, soundPath);
                        viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { Sound });
                    }
                    catch (Exception error)
                    {
                        Trace.WriteLine(new FileLoadException(soundPath, error));
                    }
                }
            }
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, AnimationKey);

            MatrixExtension.Multiply(in XNAMatrices[IAnimationMatrix], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            Turntable.ReInitTrainPositions(absAnimationMatrix);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (Turntable.GoToTarget)
            {
                AnimationKey = (Turntable.TargetY / (float)Math.PI * 1800.0f + 3600) % 3600.0f;
            }

            else if (Turntable.Counterclockwise)
            {
                AnimationKey += SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds;
            }
            else if (Turntable.Clockwise)
            {
                AnimationKey -= SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds;
            }
            while (AnimationKey > SharedShape.Animations[0].FrameCount) AnimationKey -= SharedShape.Animations[0].FrameCount;
            while (AnimationKey < 0) AnimationKey += SharedShape.Animations[0].FrameCount;

            Turntable.YAngle = MathHelper.WrapAngle(AnimationKey / 1800.0f * (float)Math.PI);

            if ((Turntable.Clockwise || Turntable.Counterclockwise) && !Rotating)
            {
                Rotating = true;
                if (Sound != null) Sound.HandleEvent(Turntable.TrainsOnMovingTable.Count == 1 &&
                    Turntable.TrainsOnMovingTable[0].FrontOnBoard && Turntable.TrainsOnMovingTable[0].BackOnBoard ? Event.MovingTableMovingLoaded : Event.MovingTableMovingEmpty);
            }
            else if ((!Turntable.Clockwise && !Turntable.Counterclockwise && Rotating))
            {
                Rotating = false;
                if (Sound != null) Sound.HandleEvent(Event.MovingTableStopped);
            }

            // Update the pose for each matrix
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, AnimationKey);

            MatrixExtension.Multiply(in XNAMatrices[IAnimationMatrix], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            Turntable.PerformUpdateActions(absAnimationMatrix);
            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }
    }

    public class TransfertableShape : PoseableShape
    {
        protected float AnimationKey;  // advances with time
        protected Transfertable Transfertable; // linked turntable data
        readonly SoundSource Sound;
        bool Translating = false;
        protected int IAnimationMatrix = -1; // index of animation matrix

        /// <summary>
        /// Construct and initialize the class
        /// </summary>
        public TransfertableShape(string path, IWorldPosition positionSource, ShapeFlags flags, Transfertable transfertable)
            : base(path, positionSource, flags)
        {
            Transfertable = transfertable;
            AnimationKey = (Transfertable.XPos - Transfertable.CenterOffset.X) / Transfertable.Width * SharedShape.Animations[0].FrameCount;
            for (var imatrix = 0; imatrix < SharedShape.Matrices.Length; ++imatrix)
            {
                if (SharedShape.MatrixNames[imatrix].ToLower() == transfertable.Animations[0].ToLower())
                {
                    IAnimationMatrix = imatrix;
                    break;
                }
            }
            if (viewer.Simulator.TRK.Tr_RouteFile.DefaultTurntableSMS != null)
            {
                var soundPath = viewer.Simulator.RoutePath + @"\\sound\\" + viewer.Simulator.TRK.Tr_RouteFile.DefaultTurntableSMS;
                try
                {
                    Sound = new SoundSource(viewer, WorldPosition.WorldLocation, Events.Source.ORTSTurntable, soundPath);
                    viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { Sound });
                }
                catch
                {
                    soundPath = viewer.Simulator.BasePath + @"\\sound\\" + viewer.Simulator.TRK.Tr_RouteFile.DefaultTurntableSMS;
                    try
                    {
                        Sound = new SoundSource(viewer, WorldPosition.WorldLocation, Events.Source.ORTSTurntable, soundPath);
                        viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { Sound });
                    }
                    catch (Exception error)
                    {
                        Trace.WriteLine(new FileLoadException(soundPath, error));
                    }
                }
            }
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, AnimationKey);

            MatrixExtension.Multiply(in XNAMatrices[IAnimationMatrix], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            Transfertable.ReInitTrainPositions(absAnimationMatrix);
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (Transfertable.GoToTarget)
            {
                AnimationKey = (Transfertable.TargetX - Transfertable.CenterOffset.X) / Transfertable.Width * SharedShape.Animations[0].FrameCount;
            }

            else if (Transfertable.Forward)
            {
                AnimationKey += SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds;
            }
            else if (Transfertable.Reverse)
            {
                AnimationKey -= SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds;
            }
            if (AnimationKey > SharedShape.Animations[0].FrameCount) AnimationKey = SharedShape.Animations[0].FrameCount;
            if (AnimationKey < 0) AnimationKey = 0;

            Transfertable.XPos = AnimationKey / SharedShape.Animations[0].FrameCount * Transfertable.Width + Transfertable.CenterOffset.X;

            if ((Transfertable.Forward || Transfertable.Reverse) && !Translating)
            {
                Translating = true;
                if (Sound != null) Sound.HandleEvent(Transfertable.TrainsOnMovingTable.Count == 1 &&
                    Transfertable.TrainsOnMovingTable[0].FrontOnBoard && Transfertable.TrainsOnMovingTable[0].BackOnBoard ? Event.MovingTableMovingLoaded : Event.MovingTableMovingEmpty);
            }
            else if ((!Transfertable.Forward && !Transfertable.Reverse && Translating))
            {
                Translating = false;
                if (Sound != null) Sound.HandleEvent(Event.MovingTableStopped);
            }

            // Update the pose for each matrix
            for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                AnimateMatrix(matrix, AnimationKey);

            MatrixExtension.Multiply(in XNAMatrices[IAnimationMatrix], in WorldPosition.XNAMatrix, out Matrix absAnimationMatrix);
            Transfertable.PerformUpdateActions(absAnimationMatrix, WorldPosition);
            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }
    }

}
