﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.Common;
using Orts.ActivityRunner.Viewer3D.Sound;
using Orts.Common;
using Orts.Common.Position;
using Orts.Common.Xna;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.World;

using Hazard = Orts.Simulation.World.Hazard;

namespace Orts.ActivityRunner.Viewer3D.Shapes
{
    /// <summary>
    /// Has a heirarchy of objects that can be moved by adjusting the XNAMatrices
    /// at each node.
    /// </summary>
    public class PoseableShape : BaseShape
    {
        private readonly IWorldPosition positionSource;

        protected static Dictionary<string, bool> SeenShapeAnimationError { get; } = new Dictionary<string, bool>();

        public Matrix[] XNAMatrices = Array.Empty<Matrix>();  // the positions of the subobjects

        public readonly int[] Hierarchy;

        public override ref readonly WorldPosition WorldPosition => ref positionSource.WorldPosition;

        public PoseableShape(string path, IWorldPosition positionSource, ShapeFlags flags) :
            base(path, flags)
        {
            this.positionSource = positionSource;

            XNAMatrices = new Matrix[SharedShape.Matrices.Length];
            for (int iMatrix = 0; iMatrix < SharedShape.Matrices.Length; ++iMatrix)
                XNAMatrices[iMatrix] = SharedShape.Matrices[iMatrix];

            if (SharedShape.LodControls.Length > 0 && SharedShape.LodControls[0].DistanceLevels.Length > 0 && SharedShape.LodControls[0].DistanceLevels[0].SubObjects.Length > 0 && SharedShape.LodControls[0].DistanceLevels[0].SubObjects[0].ShapePrimitives.Length > 0)
                Hierarchy = SharedShape.LodControls[0].DistanceLevels[0].SubObjects[0].ShapePrimitives[0].Hierarchy;
            else
                Hierarchy = Array.Empty<int>();
        }

        public PoseableShape(string path, IWorldPosition positionSource)
            : this(path, positionSource, ShapeFlags.None)
        {
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }

        public void ConditionallyPrepareFrame(RenderFrame frame, ElapsedTime elapsedTime, bool[] matrixVisible = null)
        {
            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags, matrixVisible);
        }

        /// <summary>
        /// Adjust the pose of the specified node to the frame position specifed by key.
        /// </summary>
        public virtual void AnimateMatrix(int iMatrix, double key)
        {
            // Animate the given matrix.
            AnimateOneMatrix(iMatrix, key);

            // Animate all child nodes in the hierarchy too.
            for (var i = 0; i < Hierarchy.Length; i++)
                if (Hierarchy[i] == iMatrix)
                    AnimateMatrix(i, key);
        }

        private protected void AnimateOneMatrix(int iMatrix, double key)
        {
            if (SharedShape.Animations == null || SharedShape.Animations.Count == 0)
            {
                if (!SeenShapeAnimationError.ContainsKey(SharedShape.FilePath))
                    Trace.TraceInformation("Ignored missing animations data in shape {0}", SharedShape.FilePath);
                SeenShapeAnimationError[SharedShape.FilePath] = true;
                return;  // animation is missing
            }

            if (iMatrix < 0 || iMatrix >= SharedShape.Animations[0].AnimationNodes.Count || iMatrix >= XNAMatrices.Length)
            {
                if (!SeenShapeAnimationError.ContainsKey(SharedShape.FilePath))
                    Trace.TraceInformation("Ignored out of bounds matrix {1} in shape {0}", SharedShape.FilePath, iMatrix);
                SeenShapeAnimationError[SharedShape.FilePath] = true;
                return;  // mismatched matricies
            }

            var anim_node = SharedShape.Animations[0].AnimationNodes[iMatrix];
            if (anim_node.Controllers.Count == 0)
                return;  // missing controllers

            // Start with the intial pose in the shape file.
            var xnaPose = SharedShape.Matrices[iMatrix];

            foreach (Controller controller in anim_node.Controllers)
            {
                // Determine the frame index from the current frame ('key'). We will be interpolating between two key
                // frames (the items in 'controller') so we need to find the last one LESS than the current frame
                // and interpolate with the one after it.
                var index = 0;
                for (var i = 0; i < controller.Count; i++)
                    if (controller[i].Frame <= key)
                        index = i;
                    else if (controller[i].Frame > key) // Optimisation, not required for algorithm.
                        break;

                var position1 = controller[index];
                var position2 = index + 1 < controller.Count ? controller[index + 1] : controller[index];
                var frame1 = position1.Frame;
                var frame2 = position2.Frame;

                // Make sure to clamp the amount, as we can fall outside the frame range. Also ensure there's a
                // difference between frame1 and frame2 or we'll crash.
                float amount = frame1 < frame2 ? (float)MathHelperD.Clamp((key - frame1) / (frame2 - frame1), 0, 1) : 0;

                if (position1 is SlerpRotation slerp1 && position2 is SlerpRotation slerp2)  // rotate the existing matrix
                {

                    ref readonly Quaternion slerp1Quaternion = ref slerp1.Quaternion;
                    ref readonly Quaternion slerp2Quaternion = ref slerp2.Quaternion;
                    Quaternion q = Quaternion.Slerp(slerp1Quaternion.XnaQuaternion(), slerp2Quaternion.XnaQuaternion(), amount);
                    Vector3 location = xnaPose.Translation;
                    xnaPose = Matrix.CreateFromQuaternion(q);
                    xnaPose.Translation = location;
                }
                else if (position1 is LinearKey key1 && position2 is LinearKey key2)  // a key sets an absolute position, vs shifting the existing matrix
                {
                    xnaPose.Translation = Vector3.Lerp(key1.Position.XnaVector(), key2.Position.XnaVector(), amount);
                }
                else if (position1 is TcbKey tcbkey1 && position2 is TcbKey tcbkey2) // a tcb_key sets an absolute rotation, vs rotating the existing matrix
                {
                    ref readonly Quaternion tcb1Quaternion = ref tcbkey1.Quaternion;
                    ref readonly Quaternion tcb2Quaternion = ref tcbkey2.Quaternion;
                    Quaternion q = Quaternion.Slerp(tcb1Quaternion.XnaQuaternion(), tcb2Quaternion.XnaQuaternion(), amount);
                    Vector3 location = xnaPose.Translation;
                    xnaPose = Matrix.CreateFromQuaternion(q);
                    xnaPose.Translation = location;
                }
            }
            XNAMatrices[iMatrix] = xnaPose;  // update the matrix
        }
    }

    /// <summary>
    /// An animated shape has a continuous repeating motion defined
    /// in the animations of the shape file.
    /// </summary>
    public class AnimatedShape : PoseableShape
    {
        private protected double animationKey;  // advances with time
        private protected readonly float frameRateMultiplier = 1f; // e.g. in passenger view shapes MSTS divides by 30 the frame rate; this is the inverse

        /// <summary>
        /// Construct and initialize the class
        /// </summary>
        public AnimatedShape(string path, IWorldPosition positionSource, ShapeFlags flags, float frameRateDivisor = 1.0f)
            : base(path, positionSource, flags)
        {
            frameRateMultiplier = 1 / frameRateDivisor;
        }

        public AnimatedShape(string path, IWorldPosition positionSource)
            : this(path, positionSource, ShapeFlags.None)
        {
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            // if the shape has animations
            if (SharedShape.Animations?.Count > 0 && SharedShape.Animations[0].FrameCount > 0)
            {
                animationKey += SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds * frameRateMultiplier;
                while (animationKey > SharedShape.Animations[0].FrameCount)
                    animationKey -= SharedShape.Animations[0].FrameCount;
                while (animationKey < 0)
                    animationKey += SharedShape.Animations[0].FrameCount;

                // Update the pose for each matrix
                for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                    AnimateMatrix(matrix, animationKey);
            }
            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }

        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime, in WorldPosition position)
        {
            // if the shape has animations
            if (SharedShape.Animations != null && SharedShape.Animations.Count > 0 && SharedShape.Animations[0].FrameCount > 1)
            {
                animationKey += SharedShape.Animations[0].FrameRate * elapsedTime.ClockSeconds * frameRateMultiplier;
                while (animationKey > SharedShape.Animations[0].FrameCount)
                    animationKey -= SharedShape.Animations[0].FrameCount;
                while (animationKey < 0)
                    animationKey += SharedShape.Animations[0].FrameCount;

                // Update the pose for each matrix
                for (var matrix = 0; matrix < SharedShape.Matrices.Length; ++matrix)
                    AnimateMatrix(matrix, animationKey);
            }
            SharedShape.PrepareFrame(frame, position, XNAMatrices, Flags);
        }
    }

    //Class AnalogClockShape to animate analog OR-Clocks as child of AnimatedShape <- PoseableShape <- StaticShape
    public class AnalogClockShape : AnimatedShape
    {
        public AnalogClockShape(string path, IWorldPosition positionSource, ShapeFlags flags, float frameRateDivisor = 1.0f)
            : base(path, positionSource, flags, frameRateDivisor)
        {
        }

        /// <summary>
        /// Adjust the pose of the specified ORClock hand node to the frame position specifed by key.
        /// </summary>
        public override void AnimateMatrix(int iMatrix, double key)
        {
            AnimateORClock(iMatrix, key);                      //animate matrix of analog ORClock hand

            // Animate all child nodes in the hierarchy too.
            for (var i = 0; i < Hierarchy.Length; i++)
                if (Hierarchy[i] == iMatrix)
                    AnimateMatrix(i, key);
        }

        private void AnimateORClock(int iMatrix, double key)
        {
            if (SharedShape.Animations == null || SharedShape.Animations.Count == 0)
            {
                if (!SeenShapeAnimationError.ContainsKey(SharedShape.FilePath))
                    Trace.TraceInformation("Ignored missing animations data in shape {0}", SharedShape.FilePath);
                SeenShapeAnimationError[SharedShape.FilePath] = true;
                return;  // animation is missing
            }

            if (iMatrix < 0 || iMatrix >= SharedShape.Animations[0].AnimationNodes.Count || iMatrix >= XNAMatrices.Length)
            {
                if (!SeenShapeAnimationError.ContainsKey(SharedShape.FilePath))
                    Trace.TraceInformation("Ignored out of bounds matrix {1} in shape {0}", SharedShape.FilePath, iMatrix);
                SeenShapeAnimationError[SharedShape.FilePath] = true;
                return;  // mismatched matricies
            }

            var anim_node = SharedShape.Animations[0].AnimationNodes[iMatrix];
            if (anim_node.Controllers.Count == 0)
                return;  // missing controllers

            // Start with the intial pose in the shape file.
            var xnaPose = SharedShape.Matrices[iMatrix];

            foreach (Controller controller in anim_node.Controllers)
            {
                // Determine the frame index from the current frame ('key'). We will be interpolating between two key
                // frames (the items in 'controller') so we need to find the last one LESS than the current frame
                // and interpolate with the one after it.
                var index = 0;
                for (var i = 0; i < controller.Count; i++)
                    if (controller[i].Frame <= key)
                        index = i;
                    else if (controller[i].Frame > key) // Optimisation, not required for algorithm.
                        break;

                //OR-Clock-hands Animation -------------------------------------------------------------------------------------------------------------
                if (anim_node.Name.IndexOf("hand_clock", StringComparison.OrdinalIgnoreCase) > -1)           //anim_node seems to be an OR-Clock-hand-matrix of an analog OR-Clock
                {
                    TimeSpan current = TimeSpan.FromSeconds(Simulator.Instance.ClockTime);
                    int clockQuadrant = 0;                                                  //Preset: Start with Anim-Control 0 (first quadrant of OR-Clock)
                    bool calculateClockHand = false;                                        //Preset: No drawing of a new matrix by default
                    float quadrantAmount = 1;                                               //Preset: Represents part of the way from position1 to position2 (float Value between 0 and 1)
                    if (anim_node.Name.IndexOf("orts_chand_clock", StringComparison.OrdinalIgnoreCase) > -1) //Shape matrix is a CentiSecond Hand (continuous moved second hand) of an analog OR-clock
                    {
                        clockQuadrant = current.Seconds / 15;                              //Quadrant of the clock / Key-Index of anim_node (int Values: 0, 1, 2, 3)
                        quadrantAmount = (float)(current.Seconds - (clockQuadrant * 15)) / 15;  //Seconds      Percentage quadrant related (float Value between 0 and 1) 
                        quadrantAmount = quadrantAmount + ((float)current.Milliseconds / 1000 / 15);   //CentiSeconds Percentage quadrant related (float Value between 0 and 0.0666666)
                        if (controller.Count == 0 || clockQuadrant < 0 || clockQuadrant + 1 > controller.Count - 1)
                            clockQuadrant = 0; //If controller.Count dosen't match
                        calculateClockHand = true;                                          //Calculate the new Hand position (Quaternion) below
                    }
                    if (anim_node.Name.IndexOf("orts_shand_clock", StringComparison.OrdinalIgnoreCase) > -1) //Shape matrix is a Second Hand of an analog OR-clock
                    {
                        clockQuadrant = current.Seconds / 15;                              //Quadrant of the clock / Key-Index of anim_node (int Values: 0, 1, 2, 3)
                        quadrantAmount = (float)(current.Seconds - (clockQuadrant * 15)) / 15;  //Percentage quadrant related (float Value between 0 and 1) 
                        if (controller.Count == 0 || clockQuadrant < 0 || clockQuadrant + 1 > controller.Count - 1)
                            clockQuadrant = 0; //If controller.Count dosen't match
                        calculateClockHand = true;                                          //Calculate the new Hand position (Quaternion) below
                    }
                    if (anim_node.Name.IndexOf("orts_mhand_clock", StringComparison.OrdinalIgnoreCase) > -1) //Shape matrix is a Minute Hand of an analog OR-clock
                    {
                        clockQuadrant = current.Minutes / 15;                              //Quadrant of the clock / Key-Index of anim_node (Values: 0, 1, 2, 3)
                        quadrantAmount = (float)(current.Minutes - (clockQuadrant * 15)) / 15;  //Percentage quadrant related (Value between 0 and 1)
                        if (controller.Count == 0 || clockQuadrant < 0 || clockQuadrant + 1 > controller.Count - 1)
                            clockQuadrant = 0; //If controller.Count dosen't match
                        calculateClockHand = true;                                          //Calculate the new Hand position (Quaternion) below
                    }
                    if (anim_node.Name.IndexOf("orts_hhand_clock", StringComparison.OrdinalIgnoreCase) > -1) //Shape matrix is an Hour Hand of an analog OR-clock
                    {
                        clockQuadrant = (current.Hours % 12) / 3;                                 //Quadrant of the clock / Key-Index of anim_node (Values: 0, 1, 2, 3)
                        quadrantAmount = (float)(current.Hours % 12 - (clockQuadrant * 3)) / 3;      //Percentage quadrant related (Value between 0 and 1)
                        quadrantAmount = quadrantAmount + (((float)1 / 3) * ((float)current.Minutes / 60)); //add fine minute-percentage for Hour Hand between the full hours
                        if (controller.Count == 0 || clockQuadrant < 0 || clockQuadrant + 1 > controller.Count - 1)
                            clockQuadrant = 0; //If controller.Count dosen't match
                        calculateClockHand = true;                                          //Calculate the new Hand position (Quaternion) below
                    }
                    if (calculateClockHand == true & controller.Count > 0)                  //Calculate new Hand position as usual OR-style (Slerp-animation with Quaternions)
                    {
                        var position1 = controller[clockQuadrant];
                        var position2 = controller[clockQuadrant + 1];
                        if (position1 is SlerpRotation slerp1 && position2 is SlerpRotation slerp2)  //OR-Clock anim.node has slerp keys
                        {
                            ref readonly Quaternion slerp1Quaternion = ref slerp1.Quaternion;
                            ref readonly Quaternion slerp2Quaternion = ref slerp2.Quaternion;
                            Quaternion q = Quaternion.Slerp(slerp1Quaternion.XnaQuaternion(), slerp2Quaternion.XnaQuaternion(), quadrantAmount);
                            Vector3 location = xnaPose.Translation;
                            xnaPose = Matrix.CreateFromQuaternion(q);
                            xnaPose.Translation = location;
                        }
                        else if (position1 is LinearKey key1 && position2 is LinearKey key2) //OR-Clock anim.node has tcb keys
                        {
                            xnaPose.Translation = Vector3.Lerp(key1.Position.XnaVector(), key2.Position.XnaVector(), quadrantAmount);
                        }
                        else if (position1 is TcbKey tcbkey1 && position2 is TcbKey tcbkey2) //OR-Clock anim.node has tcb keys
                        {
                            ref readonly Quaternion tcb1Quaternion = ref tcbkey1.Quaternion;
                            ref readonly Quaternion tcb2Quaternion = ref tcbkey2.Quaternion;
                            Quaternion q = Quaternion.Slerp(tcb1Quaternion.XnaQuaternion(), tcb2Quaternion.XnaQuaternion(), quadrantAmount);
                            Vector3 location = xnaPose.Translation;
                            xnaPose = Matrix.CreateFromQuaternion(q);
                            xnaPose.Translation = location;
                        }
                    }
                }
            }
            XNAMatrices[iMatrix] = xnaPose;  // update the matrix
        }
    }

    public class SwitchTrackShape : PoseableShape
    {
        private protected double animationKey;  // tracks position of points as they move left and right

        private readonly TrackJunctionNode trackJunctionNode;  // has data on current aligment for the switch
        private readonly int mainRoute;                  // 0 or 1 - which route is considered the main route

        public SwitchTrackShape(string path, IWorldPosition positionSource, TrackJunctionNode trackJunctionNode)
            : base(path, positionSource, ShapeFlags.AutoZBias)
        {
            this.trackJunctionNode = trackJunctionNode;
            mainRoute = RuntimeData.Instance.TSectionDat.TrackShapes[trackJunctionNode.ShapeIndex].MainRoute;
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            // ie, with 2 frames of animation, the key will advance from 0 to 1
            if (trackJunctionNode.SelectedRoute == mainRoute)
            {
                if (animationKey > 0.001)
                    animationKey -= 0.002 * elapsedTime.ClockSeconds * 1000.0;
                if (animationKey < 0.001)
                    animationKey = 0;
            }
            else
            {
                if (animationKey < 0.999)
                    animationKey += 0.002 * elapsedTime.ClockSeconds * 1000.0;
                if (animationKey > 0.999)
                    animationKey = 1.0;
            }

            // Update the pose
            for (int iMatrix = 0; iMatrix < SharedShape.Matrices.Length; ++iMatrix)
                AnimateMatrix(iMatrix, animationKey);

            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }
    }

    public class SpeedPostShape : PoseableShape
    {
        private readonly SpeedPostObject speedPostObject;  // has data on current aligment for the switch
        private readonly VertexPositionNormalTexture[] vertices;
        private readonly int numberVertices;
        private readonly int numberIndices;
        private readonly short[] triangleListIndices;// Array of indices to vertices for triangles

        private protected readonly double animationKey;  // tracks position of points as they move left and right
        private readonly ShapePrimitive shapePrimitive;

        public SpeedPostShape(string path, IWorldPosition positionSource, SpeedPostObject speedPostObject)
            : base(path, positionSource, ShapeFlags.None)
        {

            this.speedPostObject = speedPostObject;
            int maxVertex = speedPostObject.SignShapes.Count * 48;// every face has max 7 digits, each has 2 triangles
            Material material = viewer.MaterialManager.Load("Scenery", Helpers.GetRouteTextureFile(Helpers.TextureFlags.None, speedPostObject.TextureFile), (int)(SceneryMaterialOptions.None | SceneryMaterialOptions.AlphaBlendingBlend), 0);

            // Create and populate a new ShapePrimitive
            int i = 0;
            float size = speedPostObject.TextSize.Size;
            int idlocation = 0;
            while (idlocation < speedPostObject.TrackItemIds.TrackDbItems.Count)
            {
                int id = speedPostObject.TrackItemIds.TrackDbItems[idlocation];
                //                SpeedPostItem item;
                string speed = string.Empty;
                if (!(RuntimeData.Instance.TrackDB.TrackItems[id] is SpeedPostItem item))
                    throw new InvalidCastException(RuntimeData.Instance.TrackDB.TrackItems[id].ItemName);  // Error to be handled in Scenery.cs

                //determine what to show: speed or number used in German routes
                if (item.ShowNumber)
                {
                    speed += item.NumberShown;
                    if (!item.ShowDot)
                        speed = speed.Replace(".", "", StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    //determine if the speed is for passenger or freight
                    if (item.IsFreight && !item.IsPassenger)
                        speed += "F";
                    else if (!item.IsFreight && item.IsPassenger)
                        speed += "P";

                    speed += item.Distance;
                }

                vertices = new VertexPositionNormalTexture[maxVertex];
                triangleListIndices = new short[maxVertex / 2 * 3]; // as is NumIndices

                for (i = 0; i < speedPostObject.SignShapes.Count; i++)
                {
                    //start position is the center of the text
                    Vector3 start = new Vector3(speedPostObject.SignShapes[i].X, speedPostObject.SignShapes[i].Y, speedPostObject.SignShapes[i].Z);
                    float rotation = speedPostObject.SignShapes[i].W;

                    //find the left-most of text
                    Vector3 offset;
                    if (Math.Abs(speedPostObject.TextSize.Offset.Y) > 0.01)
                        offset = new Vector3(0 - size / 2, 0, 0);
                    else
                        offset = new Vector3(0, 0 - size / 2, 0);

                    offset.X -= speed.Length * speedPostObject.TextSize.Offset.X / 2;
                    offset.Y -= speed.Length * speedPostObject.TextSize.Offset.Y / 2;

                    for (int j = 0; j < speed.Length; j++)
                    {
                        float tX = GetTextureCoordX(speed[j]);
                        float tY = GetTextureCoordY(speed[j]);
                        Matrix rot = Matrix.CreateRotationY(-rotation);

                        //the left-bottom vertex
                        Vector3 v = new Vector3(offset.X, offset.Y, 0.01f);
                        v = Vector3.Transform(v, rot);
                        v += start;
                        Vertex v1 = new Vertex(v.X, v.Y, v.Z, 0, 0, -1, tX, tY);

                        //the right-bottom vertex
                        v.X = offset.X + size;
                        v.Y = offset.Y;
                        v.Z = 0.01f;
                        v = Vector3.Transform(v, rot);
                        v += start;
                        Vertex v2 = new Vertex(v.X, v.Y, v.Z, 0, 0, -1, tX + 0.25f, tY);

                        //the right-top vertex
                        v.X = offset.X + size;
                        v.Y = offset.Y + size;
                        v.Z = 0.01f;
                        v = Vector3.Transform(v, rot);
                        v += start;
                        Vertex v3 = new Vertex(v.X, v.Y, v.Z, 0, 0, -1, tX + 0.25f, tY - 0.25f);

                        //the left-top vertex
                        v.X = offset.X;
                        v.Y = offset.Y + size;
                        v.Z = 0.01f;
                        v = Vector3.Transform(v, rot);
                        v += start;
                        Vertex v4 = new Vertex(v.X, v.Y, v.Z, 0, 0, -1, tX, tY - 0.25f);

                        //memory may not be enough
                        if (numberVertices > maxVertex - 4)
                        {
                            VertexPositionNormalTexture[] TempVertexList = new VertexPositionNormalTexture[maxVertex + 128];
                            short[] TempTriangleListIndices = new short[(maxVertex + 128) / 2 * 3]; // as is NumIndices
                            for (var k = 0; k < maxVertex; k++)
                                TempVertexList[k] = vertices[k];
                            for (var k = 0; k < maxVertex / 2 * 3; k++)
                                TempTriangleListIndices[k] = triangleListIndices[k];
                            triangleListIndices = TempTriangleListIndices;
                            vertices = TempVertexList;
                            maxVertex += 128;
                        }

                        //create first triangle
                        triangleListIndices[numberIndices++] = (short)numberVertices;
                        triangleListIndices[numberIndices++] = (short)(numberVertices + 2);
                        triangleListIndices[numberIndices++] = (short)(numberVertices + 1);
                        // Second triangle:
                        triangleListIndices[numberIndices++] = (short)numberVertices;
                        triangleListIndices[numberIndices++] = (short)(numberVertices + 3);
                        triangleListIndices[numberIndices++] = (short)(numberVertices + 2);

                        //create vertex
                        vertices[numberVertices].Position = v1.Position;
                        vertices[numberVertices].Normal = v1.Normal;
                        vertices[numberVertices].TextureCoordinate = v1.TexCoord;
                        vertices[numberVertices + 1].Position = v2.Position;
                        vertices[numberVertices + 1].Normal = v2.Normal;
                        vertices[numberVertices + 1].TextureCoordinate = v2.TexCoord;
                        vertices[numberVertices + 2].Position = v3.Position;
                        vertices[numberVertices + 2].Normal = v3.Normal;
                        vertices[numberVertices + 2].TextureCoordinate = v3.TexCoord;
                        vertices[numberVertices + 3].Position = v4.Position;
                        vertices[numberVertices + 3].Normal = v4.Normal;
                        vertices[numberVertices + 3].TextureCoordinate = v4.TexCoord;
                        numberVertices += 4;
                        offset.X += speedPostObject.TextSize.Offset.X;
                        offset.Y += speedPostObject.TextSize.Offset.Y; //move to next digit
                    }

                }
                idlocation++;
            }
            //create the shape primitive
            short[] newTList = new short[numberIndices];
            for (i = 0; i < numberIndices; i++)
                newTList[i] = triangleListIndices[i];
            VertexPositionNormalTexture[] newVList = new VertexPositionNormalTexture[numberVertices];
            for (i = 0; i < numberVertices; i++)
                newVList[i] = vertices[i];
            IndexBuffer indexBuffer = new IndexBuffer(viewer.Game.GraphicsDevice, typeof(short),
                                                            numberIndices, BufferUsage.WriteOnly);
            indexBuffer.SetData(newTList);
            shapePrimitive = new ShapePrimitive(viewer.Game.GraphicsDevice, material, new SharedShape.VertexBufferSet(newVList, viewer.Game.GraphicsDevice), indexBuffer, 0, numberVertices, numberIndices / 3, new[] { -1 }, 0);

        }

        private static float GetTextureCoordX(char c)
        {
            float x;
            switch (c)
            {
                case '.':
                    x = 0f;
                    break;
                case 'P':
                    x = 0.5f;
                    break;
                case 'F':
                    x = 0.75f;
                    break;
                default:
                    x = (c - '0') % 4 * 0.25f;
                    break;
            }
            Debug.Assert(x <= 1);
            Debug.Assert(x >= 0);
            return x;
        }

        private static float GetTextureCoordY(char c)
        {
            switch (c)
            {
                case '0':
                case '1':
                case '2':
                case '3':
                    return 0.25f;
                case '4':
                case '5':
                case '6':
                case '7':
                    return 0.5f;
                case '8':
                case '9':
                case 'P':
                case 'F':
                    return 0.75f;
                default:
                    return 1f;
            }
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            // Offset relative to the camera-tile origin
            int dTileX = WorldPosition.TileX - viewer.Camera.TileX;
            int dTileZ = WorldPosition.TileZ - viewer.Camera.TileZ;
            Vector3 tileOffsetWrtCamera = new Vector3(dTileX * 2048, 0, -dTileZ * 2048);

            // Initialize xnaXfmWrtCamTile to object-tile to camera-tile translation:
            MatrixExtension.Multiply(WorldPosition.XNAMatrix, Matrix.CreateTranslation(tileOffsetWrtCamera), out Matrix xnaXfmWrtCamTile);
            // (Transformation is now with respect to camera-tile origin)

            // TODO: Make this use AddAutoPrimitive instead.
            frame.AddPrimitive(shapePrimitive.Material, shapePrimitive, RenderPrimitiveGroup.World, ref xnaXfmWrtCamTile, ShapeFlags.None);

            // if there is no animation, that's normal and so no animation missing error is displayed
            if (SharedShape.Animations == null || SharedShape.Animations.Count == 0)
            {
                if (!SeenShapeAnimationError.ContainsKey(SharedShape.FilePath))
                    SeenShapeAnimationError[SharedShape.FilePath] = true;
            }
            // Update the pose
            for (int iMatrix = 0; iMatrix < SharedShape.Matrices.Length; ++iMatrix)
                AnimateMatrix(iMatrix, animationKey);

            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }

        internal override void Mark()
        {
            shapePrimitive.Mark();
            base.Mark();
        }
    } // class SpeedPostShape

    public class LevelCrossingShape : PoseableShape
    {
        private readonly LevelCrossingObject levelCrossingObject;
        private readonly SoundSource soundSource;
        private readonly LevelCrossing levelCrossing;

        private readonly float animationFrames;
        private readonly float animationSpeed;
        private bool opening = true;
        private double animationKey;

        public LevelCrossingShape(string path, IWorldPosition positionSource, ShapeFlags shapeFlags, LevelCrossingObject crossingObj)
            : base(path, positionSource, shapeFlags)
        {
            levelCrossingObject = crossingObj;
            if (!levelCrossingObject.Silent)
            {
                string soundFileName = null;
                if (!string.IsNullOrEmpty(levelCrossingObject.SoundFileName))
                    soundFileName = levelCrossingObject.SoundFileName;
                else if (!string.IsNullOrEmpty(SharedShape.SoundFileName))
                    soundFileName = SharedShape.SoundFileName;
                else if (!string.IsNullOrEmpty(viewer.Simulator.Route.DefaultCrossingSMS))
                    soundFileName = viewer.Simulator.Route.DefaultCrossingSMS;
                if (!string.IsNullOrEmpty(soundFileName))
                {
                    string soundPath = viewer.Simulator.RouteFolder.SoundFile(soundFileName);
                    if (File.Exists(soundPath))
                    {
                        soundSource = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.Crossing, soundPath);
                        viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { soundSource });
                    }
                    else if (File.Exists(soundPath = viewer.Simulator.RouteFolder.ContentFolder.SoundFile(soundFileName)))
                    {
                        soundSource = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.Crossing, soundPath);
                        viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { soundSource });
                    }
                    else
                    {
                        Trace.WriteLine($"Could not load sound file {soundPath}");
                    }
                }
            }
            levelCrossing = viewer.Simulator.LevelCrossings.CreateLevelCrossing(WorldPosition,
                levelCrossingObject.TrackItemIds.TrackDbItems, levelCrossingObject.TrackItemIds.RoadDbItems,
                levelCrossingObject.WarningTime, levelCrossingObject.MinimumDistance);
            // If there are no animations, we leave the frame count and speed at 0 and nothing will try to animate.
            if (SharedShape.Animations != null && SharedShape.Animations.Count > 0)
            {
                // LOOPED COSSINGS (animTiming < 0)
                //     MSTS plays through all the frames of the animation for "closed" and sits on frame 0 for "open". The
                //     speed of animation is the normal speed (frame rate at 30FPS) scaled by the timing value. Since the
                //     timing value is negative, the animation actually plays in reverse.
                // NON-LOOPED CROSSINGS (animTiming > 0)
                //     MSTS plays through the first 1.0 seconds of the animation forwards for closing and backwards for
                //     opening. The number of frames defined doesn't matter; the animation is limited by time so the frame
                //     rate (based on 30FPS) is what's needed.
                animationFrames = levelCrossingObject.AnimationTiming < 0 ? SharedShape.Animations[0].FrameCount : SharedShape.Animations[0].FrameRate / 30f;
                animationSpeed = SharedShape.Animations[0].FrameRate / 30f / levelCrossingObject.AnimationTiming;
            }
        }

        public override void Unload()
        {
            if (soundSource != null)
            {
                viewer.SoundProcess.RemoveSoundSources(this);
                soundSource.Dispose();
            }
            base.Unload();
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (!levelCrossingObject.Visible)
                return;

            if (opening == levelCrossing.HasTrain)
            {
                opening = !levelCrossing.HasTrain;
                soundSource?.HandleEvent(opening ? TrainEvent.CrossingOpening : TrainEvent.CrossingClosing);
            }

            if (opening)
                animationKey -= (float)elapsedTime.ClockSeconds * animationSpeed;
            else
                animationKey += (float)elapsedTime.ClockSeconds * animationSpeed;

            if (levelCrossingObject.AnimationTiming < 0)
            {
                // Stick to frame 0 for "open" and loop for "closed".
                if (opening)
                    animationKey = 0;
                if (animationKey < 0)
                    animationKey += animationFrames;
            }
            if (animationKey < 0)
                animationKey = 0;
            if (animationKey > animationFrames)
                animationKey = animationFrames;

            for (var i = 0; i < SharedShape.Matrices.Length; ++i)
                AnimateMatrix(i, animationKey);

            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }
    }

    public class HazardShape : PoseableShape
    {
        private readonly HazardObject hazardObject;
        private readonly Hazard hazard;

        private readonly int animationFrames;
        private double moved;
        private double animationKey;
        private double delayHazAnimation;

        public static HazardShape CreateHazard(string path, IWorldPosition positionSource, ShapeFlags shapeFlags, HazardObject hazardObject)
        {
            var h = viewer.Simulator.HazardManager.AddHazardIntoGame(hazardObject.ItemId, hazardObject.FileName);
            if (h == null)
                return null;
            return new HazardShape(viewer.Simulator.RouteFolder.ContentFolder.ShapeFile(h.HazardFile.Hazard.FileName) + "\0" + viewer.Simulator.RouteFolder.ContentFolder.TexturesFolder, positionSource, shapeFlags, hazardObject, h);

        }

        public HazardShape(string path, IWorldPosition positionSource, ShapeFlags shapeFlags, HazardObject hazardObject, Hazard h)
            : base(path, positionSource, shapeFlags)
        {
            this.hazardObject = hazardObject;
            hazard = h;
            animationFrames = SharedShape.Animations[0].FrameCount;
        }

        public override void Unload()
        {
            viewer.Simulator.HazardManager.RemoveHazardFromGame(hazardObject.ItemId);
            base.Unload();
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            if (hazard == null)
                return;
            Vector2 currentRange;
            animationKey += elapsedTime.ClockSeconds * 24;
            delayHazAnimation += elapsedTime.ClockSeconds;
            switch (hazard.State)
            {
                case Hazard.HazardState.Idle1:
                    currentRange = hazard.HazardFile.Hazard.IdleKey;
                    break;
                case Hazard.HazardState.Idle2:
                    currentRange = hazard.HazardFile.Hazard.IdleKey2;
                    break;
                case Hazard.HazardState.LookLeft:
                    currentRange = hazard.HazardFile.Hazard.SurpriseKeyLeft;
                    break;
                case Hazard.HazardState.LookRight:
                    currentRange = hazard.HazardFile.Hazard.SurpriseKeyRight;
                    break;
                case Hazard.HazardState.Scared:
                default:
                    currentRange = hazard.HazardFile.Hazard.SuccessScarperKey;
                    if (moved < hazard.HazardFile.Hazard.Distance)
                    {
                        var m = hazard.HazardFile.Hazard.Speed * elapsedTime.ClockSeconds;
                        moved += m;
                        // Shape's position isn't stored but only calculated dynamically as it's passed to PrepareFrame further down
                        // this seems acceptable as the number of Hazardous objects is rather small
                        //WorldPosition.SetLocation(HazardObj.Position.X, HazardObj.Position.Y, HazardObj.Position.Z);
                        hazardObject.UpdatePosition((float)m);
                    }
                    else
                    {
                        moved = 0;
                        hazard.State = Hazard.HazardState.Idle1;
                    }
                    break;
            }

            switch (hazard.State)
            {
                case Hazard.HazardState.Idle1:
                case Hazard.HazardState.Idle2:
                    if (delayHazAnimation > 5.0)
                    {
                        if (animationKey < currentRange.X)
                        {
                            animationKey = currentRange.X;
                            delayHazAnimation = 0;
                        }

                        if (animationKey > currentRange.Y)
                        {
                            animationKey = currentRange.Y;
                            delayHazAnimation = 0;
                        }
                    }
                    break;
                case Hazard.HazardState.LookLeft:
                case Hazard.HazardState.LookRight:
                    if (animationKey < currentRange.X)
                        animationKey = currentRange.X;
                    if (animationKey > currentRange.Y)
                        animationKey = currentRange.Y;
                    break;
                case Hazard.HazardState.Scared:
                    if (animationKey < currentRange.X)
                        animationKey = currentRange.X;
                    if (animationKey > currentRange.Y)
                        animationKey = currentRange.Y;
                    break;
            }

            for (var i = 0; i < SharedShape.Matrices.Length; ++i)
                AnimateMatrix(i, animationKey);

            //SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
            //            SharedShape.PrepareFrame(frame, WorldPosition.SetMstsTranslation(hazardObject.Position.X, hazardObject.Position.Y, hazardObject.Position.Z), XNAMatrices, Flags);
            SharedShape.PrepareFrame(frame, hazardObject.WorldPosition, XNAMatrices, Flags);
        }
    }

    public class FuelPickupItemShape : PoseableShape
    {
        private protected readonly PickupObject fuelPickupItemObject;
        //private readonly FuelPickupItem fuelPickupItem;
        private protected SoundSource soundSource;
        private protected float frameRate;

        private protected int animationFrames;
        private protected double animationKey;


        public FuelPickupItemShape(string path, IWorldPosition positionSource, ShapeFlags shapeFlags, PickupObject fuelpickupitemObj)
            : base(path, positionSource, shapeFlags)
        {
            fuelPickupItemObject = fuelpickupitemObj;
            Initialize();
        }

        protected virtual void Initialize()
        {
            string soundPath;
            if (Simulator.Instance.Route.DefaultDieselTowerSMS != null && fuelPickupItemObject.PickupType == PickupType.FuelDiesel) // Testing for Diesel PickupType
            {
                soundPath = Simulator.Instance.RouteFolder.SoundFile(Simulator.Instance.Route.DefaultDieselTowerSMS);
                if (File.Exists(soundPath))
                {
                    soundSource = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.FuelTower, soundPath);
                    viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { soundSource });
                }
                else if (File.Exists(soundPath = Simulator.Instance.RouteFolder.ContentFolder.SoundFile(viewer.Simulator.Route.DefaultDieselTowerSMS)))
                {
                    soundSource = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.FuelTower, soundPath);
                    viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { soundSource });
                }
                else
                {
                    Trace.WriteLine($"Diesel pickup soundfile {soundPath} not found");
                }
            }
            if (Simulator.Instance.Route.DefaultWaterTowerSMS != null && fuelPickupItemObject.PickupType == PickupType.FuelWater) // Testing for Water PickupType
            {
                soundPath = Simulator.Instance.RouteFolder.SoundFile(viewer.Simulator.Route.DefaultWaterTowerSMS);
                if (File.Exists(soundPath))
                {
                    soundSource = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.FuelTower, soundPath);
                    viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { soundSource });
                }
                else if (File.Exists(soundPath = Simulator.Instance.RouteFolder.ContentFolder.SoundFile(Simulator.Instance.Route.DefaultWaterTowerSMS)))
                {
                    soundSource = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.FuelTower, soundPath);
                    viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { soundSource });
                }
                else
                {
                    Trace.WriteLine($"Water pickup soundfile {soundPath} not found");
                }
            }
            if (fuelPickupItemObject.PickupType == PickupType.FuelCoal || fuelPickupItemObject.PickupType == PickupType.FreightCoal)
            {
                if (Simulator.Instance.Route.DefaultCoalTowerSMS != null && File.Exists(soundPath = Simulator.Instance.RouteFolder.SoundFile(Simulator.Instance.Route.DefaultCoalTowerSMS)))
                {
                    if (File.Exists(soundPath))
                    {
                        soundSource = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.FuelTower, soundPath);
                        viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { soundSource });
                    }
                    else if (File.Exists(soundPath = Simulator.Instance.RouteFolder.ContentFolder.SoundFile(Simulator.Instance.Route.DefaultCoalTowerSMS)))
                    {
                        soundSource = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.FuelTower, soundPath);
                        viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { soundSource });
                    }
                    else
                    {
                        Trace.WriteLine($"Fuel pickup soundfile {soundPath} not found");
                    }
                }
            }
            //fuelPickupItem = viewer.Simulator.FuelManager.CreateFuelStation(WorldPosition, fuelPickupItemObject.TrackItemIds.TrackDbItems);
            animationFrames = 1;
            frameRate = 1;
            if (SharedShape.Animations != null && SharedShape.Animations.Count > 0 && SharedShape.Animations[0].AnimationNodes != null && SharedShape.Animations[0].AnimationNodes.Count > 0)
            {
                frameRate = SharedShape.Animations[0].FrameCount / fuelPickupItemObject.Options.AnimationSpeed;
                foreach (var anim_node in SharedShape.Animations[0].AnimationNodes)
                    if (anim_node.Name == "ANIMATED_PARTS")
                    {
                        animationFrames = SharedShape.Animations[0].FrameCount;
                        break;
                    }
            }
        }

        public override void Unload()
        {
            if (soundSource != null)
            {
                viewer.SoundProcess.RemoveSoundSources(this);
                soundSource.Dispose();
            }
            base.Unload();
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {

            // 0 can be used as a setting for instant animation.
            if (FuelPickupItem.ReFill() && fuelPickupItemObject.UiD == MSTSWagon.RefillProcess.ActivePickupObjectUID)
            {
                if (animationKey == 0 && soundSource != null)
                    soundSource.HandleEvent(TrainEvent.FuelTowerDown);
                if (fuelPickupItemObject.Options.AnimationSpeed == 0)
                    animationKey = 1.0f;
                else if (animationKey < animationFrames)
                    animationKey += elapsedTime.ClockSeconds * frameRate;
            }

            if (!FuelPickupItem.ReFill() && animationKey > 0)
            {
                if (animationKey == animationFrames && soundSource != null)
                {
                    soundSource.HandleEvent(TrainEvent.FuelTowerTransferEnd);
                    soundSource.HandleEvent(TrainEvent.FuelTowerUp);
                }
                animationKey -= elapsedTime.ClockSeconds * frameRate;
            }

            if (animationKey < 0)
            {
                animationKey = 0;
            }
            if (animationKey > animationFrames)
            {
                animationKey = animationFrames;
                if (soundSource != null)
                    soundSource.HandleEvent(TrainEvent.FuelTowerTransferStart);
            }

            for (var i = 0; i < SharedShape.Matrices.Length; ++i)
                AnimateMatrix(i, animationKey);

            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
        }
    } // End Class FuelPickupItemShape

    public class ContainerHandlingItemShape : FuelPickupItemShape
    {
        private const float slowDownThreshold = 0.03f;

        private double animationKeyX;
        private double animationKeyY;
        private double animationKeyZ;
        private double animationKeyGrabber01;
        private double animationKeyGrabber02;
        private int animationMatrixXIndex;
        private int animationMatrixYIndex;
        private int animationMatrixZIndex;
        private int grabber01Index;
        private int grabber02Index;
        private Controller controllerX;
        private Controller controllerY;
        private Controller controllerZ;
        private Controller controllerGrabber01;
        private Controller controllerGrabber02;
        // To detect transitions that trigger sounds
        protected bool OldMoveX;
        protected bool OldMoveY;
        protected bool OldMoveZ;

        private ContainerHandlingItem containerHandlingItem;

        public ContainerHandlingItemShape(string path, IWorldPosition positionSource, ShapeFlags shapeFlags, PickupObject fuelpickupitemObj)
                        : base(path, positionSource, shapeFlags, fuelpickupitemObj)
        {
        }

        protected override void Initialize()
        {
            for (int i = 0; i < SharedShape.Matrices.Length; ++i)
            {
                switch (SharedShape.MatrixNames[i])
                {
                    case string s when s.Equals("zaxis", StringComparison.OrdinalIgnoreCase):
                        animationMatrixZIndex = i;
                        break;
                    case string s when s.Equals("xaxis", StringComparison.OrdinalIgnoreCase):
                        animationMatrixXIndex = i;
                        break;
                    case string s when s.Equals("yaxis", StringComparison.OrdinalIgnoreCase):
                        animationMatrixYIndex = i;
                        break;
                    case string s when s.Equals("grabber01", StringComparison.OrdinalIgnoreCase):
                        grabber01Index = i;
                        break;
                    case string s when s.Equals("grabber02", StringComparison.OrdinalIgnoreCase):
                        grabber02Index = i;
                        break;
                }
            }

            controllerX = SharedShape.Animations[0].AnimationNodes[animationMatrixXIndex].Controllers[0];
            controllerY = SharedShape.Animations[0].AnimationNodes[animationMatrixYIndex].Controllers[0];
            controllerZ = SharedShape.Animations[0].AnimationNodes[animationMatrixZIndex].Controllers[0];
            controllerGrabber01 = SharedShape.Animations[0].AnimationNodes[grabber01Index].Controllers[0];
            controllerGrabber02 = SharedShape.Animations[0].AnimationNodes[grabber02Index].Controllers[0];

            animationKeyX = Math.Abs((0 - ((LinearKey)controllerX[0]).Position.X) / (((LinearKey)controllerX[1]).Position.X - ((LinearKey)controllerX[0]).Position.X)) * controllerX[1].Frame;
            animationKeyY = Math.Abs((0 - ((LinearKey)controllerY[0]).Position.Y) / (((LinearKey)controllerY[1]).Position.Y - ((LinearKey)controllerY[0]).Position.Y)) * controllerY[1].Frame;
            animationKeyZ = Math.Abs((0 - ((LinearKey)controllerZ[0]).Position.Z) / (((LinearKey)controllerZ[1]).Position.Z - ((LinearKey)controllerZ[0]).Position.Z)) * controllerZ[1].Frame;
            string soundPath;
            if (fuelPickupItemObject.CraneSound != null && File.Exists(soundPath = Simulator.Instance.RouteFolder.SoundFile(fuelPickupItemObject.CraneSound)) ||
                File.Exists(soundPath = Simulator.Instance.RouteFolder.SoundFile("containercrane.sms")) ||
                File.Exists(soundPath = Simulator.Instance.RouteFolder.ContentFolder.SoundFile("containercrane.sms")))
            {
                soundSource = new SoundSource(WorldPosition.WorldLocation, SoundEventSource.ContainerCrane, soundPath);
                viewer.SoundProcess.AddSoundSources(this, new List<SoundSourceBase>() { soundSource });
            }
            else
                Trace.TraceWarning("Cannot find sound file {0}", soundPath);

            containerHandlingItem = Simulator.Instance.ContainerManager.ContainerHandlingItems[fuelPickupItemObject.TrackItemIds.TrackDbItems[0]];
            animationFrames = 1;
            frameRate = 1;
            if (SharedShape.Animations?.Count > 0 && SharedShape.Animations[0].AnimationNodes?.Count > 0)
            {
                frameRate = SharedShape.Animations[0].FrameCount / fuelPickupItemObject.Options.AnimationSpeed;
                foreach (AnimationNode anim_node in SharedShape.Animations[0].AnimationNodes)
                    if (anim_node.Name == "ANIMATED_PARTS")
                    {
                        animationFrames = SharedShape.Animations[0].FrameCount;
                        break;
                    }
            }
            AnimateOneMatrix(animationMatrixXIndex, animationKeyX);
            AnimateOneMatrix(animationMatrixYIndex, animationKeyY);
            AnimateOneMatrix(animationMatrixZIndex, animationKeyZ);

            Matrix absAnimationMatrix = XNAMatrices[animationMatrixYIndex];
            absAnimationMatrix = MatrixExtension.Multiply(absAnimationMatrix, XNAMatrices[animationMatrixXIndex]);
            absAnimationMatrix = MatrixExtension.Multiply(absAnimationMatrix, XNAMatrices[animationMatrixZIndex]);
            absAnimationMatrix = MatrixExtension.Multiply(absAnimationMatrix, WorldPosition.XNAMatrix);
            containerHandlingItem.PassSpanParameters(((LinearKey)controllerZ[0]).Position.Z, ((LinearKey)controllerZ[1]).Position.Z,
                ((LinearKey)controllerGrabber01[0]).Position.Z - ((LinearKey)controllerGrabber01[1]).Position.Z, ((LinearKey)controllerGrabber02[0]).Position.Z - ((LinearKey)controllerGrabber02[1]).Position.Z);
            containerHandlingItem.ReInitPositionOffset(absAnimationMatrix);

            animationKeyX = Math.Abs((containerHandlingItem.PickingSurfaceRelativeTopStartPosition.X - ((LinearKey)controllerX[0]).Position.X) / (((LinearKey)controllerX[1]).Position.X - ((LinearKey)controllerX[0]).Position.X)) * controllerX[1].Frame;
            animationKeyY = Math.Abs((containerHandlingItem.PickingSurfaceRelativeTopStartPosition.Y - ((LinearKey)controllerY[0]).Position.Y) / (((LinearKey)controllerY[1]).Position.Y - ((LinearKey)controllerY[0]).Position.Y)) * controllerY[1].Frame;
            animationKeyZ = Math.Abs((containerHandlingItem.PickingSurfaceRelativeTopStartPosition.Z - ((LinearKey)controllerZ[0]).Position.Z) / (((LinearKey)controllerZ[1]).Position.Z - ((LinearKey)controllerZ[0]).Position.Z)) * controllerZ[1].Frame;
            AnimateOneMatrix(animationMatrixXIndex, animationKeyX);
            AnimateOneMatrix(animationMatrixYIndex, animationKeyY);
            AnimateOneMatrix(animationMatrixZIndex, animationKeyZ);

            for (int i = 0; i < SharedShape.Matrices.Length; ++i)
            {
                switch (SharedShape.MatrixNames[i])
                {
                    case string s when s.StartsWith("cable", StringComparison.OrdinalIgnoreCase):
                        AnimateOneMatrix(i, animationKeyY);
                        break;
                    case string s when s.StartsWith("grabber", StringComparison.OrdinalIgnoreCase):
                        AnimateOneMatrix(i, 0);
                        break;
                }
            }
        }

        public override void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            // 0 can be used as a setting for instant animation.
            /*           if (ContainerHandlingItem.ReFill() && FuelPickupItemObj.UID == MSTSWagon.RefillProcess.ActivePickupObjectUID)
                       {
                           if (AnimationKey == 0 && Sound != null) Sound.HandleEvent(Event.FuelTowerDown);
                           if (FuelPickupItemObj.PickupAnimData.AnimationSpeed == 0) AnimationKey = 1.0f;
                           else if (AnimationKey < AnimationFrames)
                               AnimationKey += elapsedTime.ClockSeconds * FrameRate;
                       }

                       if (!ContainerHandlingItem.ReFill() && AnimationKey > 0)
                       {
                           if (AnimationKey == AnimationFrames && Sound != null)
                           {
                               Sound.HandleEvent(Event.FuelTowerTransferEnd);
                               Sound.HandleEvent(Event.FuelTowerUp);
                           }
                           AnimationKey -= elapsedTime.ClockSeconds * FrameRate;
                       }

                       if (AnimationKey < 0)
                       {
                           AnimationKey = 0;
                       }
                       if (AnimationKey > AnimationFrames)
                       {
                           AnimationKey = AnimationFrames;
                           if (Sound != null) Sound.HandleEvent(Event.FuelTowerTransferStart);
                       }

                       for (var i = 0; i < SharedShape.Matrices.Length; ++i)
                           AnimateMatrix(i, AnimationKey);
            */
            if (fuelPickupItemObject.UiD == MSTSWagon.RefillProcess.ActivePickupObjectUID)
            {
                float tempFrameRate;
                if (containerHandlingItem.MoveX)
                {
                    float animationTarget = Math.Abs((containerHandlingItem.TargetX - ((LinearKey)controllerX[0]).Position.X) / (((LinearKey)controllerX[1]).Position.X - ((LinearKey)controllerX[0]).Position.X)) * controllerX[1].Frame;
                    //                    if (AnimationKey == 0 && Sound != null) Sound.HandleEvent(Event.FuelTowerDown);
                    tempFrameRate = Math.Abs(animationKeyX - animationTarget) > slowDownThreshold ? frameRate : frameRate / 4;
                    if (animationKeyX < animationTarget)
                    {
                        animationKeyX += elapsedTime.ClockSeconds * tempFrameRate;
                        // don't oscillate!
                        if (animationKeyX >= animationTarget)
                        {
                            animationKeyX = animationTarget;
                            containerHandlingItem.MoveX = false;
                        }
                    }
                    else if (animationKeyX > animationTarget)
                    {
                        animationKeyX -= elapsedTime.ClockSeconds * tempFrameRate;
                        if (animationKeyX <= animationTarget)
                        {
                            animationKeyX = animationTarget;
                            containerHandlingItem.MoveX = false;
                        }
                    }
                    else
                        containerHandlingItem.MoveX = false;
                    if (animationKeyX < 0)
                        animationKeyX = 0;
                }

                if (containerHandlingItem.MoveY)
                {
                    float animationTarget = Math.Abs((containerHandlingItem.TargetY - ((LinearKey)controllerY[0]).Position.Y) / (((LinearKey)controllerY[1]).Position.Y - ((LinearKey)controllerY[0]).Position.Y)) * controllerY[1].Frame;
                    tempFrameRate = Math.Abs(animationKeyY - animationTarget) > slowDownThreshold ? frameRate : frameRate / 4;
                    if (animationKeyY < animationTarget)
                    {
                        animationKeyY += elapsedTime.ClockSeconds * tempFrameRate;
                        if (animationKeyY >= animationTarget)
                        {
                            animationKeyY = animationTarget;
                            containerHandlingItem.MoveY = false;
                        }
                    }
                    else if (animationKeyY > animationTarget)
                    {
                        animationKeyY -= elapsedTime.ClockSeconds * tempFrameRate;
                        if (animationKeyY <= animationTarget)
                        {
                            animationKeyY = animationTarget;
                            containerHandlingItem.MoveY = false;
                        }
                    }
                    else
                        containerHandlingItem.MoveY = false;
                    if (animationKeyY < 0)
                        animationKeyY = 0;
                }

                if (containerHandlingItem.MoveZ)
                {
                    float animationTarget = Math.Abs((containerHandlingItem.TargetZ - ((LinearKey)controllerZ[0]).Position.Z) / (((LinearKey)controllerZ[1]).Position.Z - ((LinearKey)controllerZ[0]).Position.Z)) * controllerZ[1].Frame;
                    tempFrameRate = Math.Abs(animationKeyZ - animationTarget) > slowDownThreshold ? frameRate : frameRate / 4;
                    if (animationKeyZ < animationTarget)
                    {
                        animationKeyZ += elapsedTime.ClockSeconds * tempFrameRate;
                        if (animationKeyZ >= animationTarget)
                        {
                            animationKeyZ = animationTarget;
                            containerHandlingItem.MoveZ = false;
                        }
                    }
                    else if (animationKeyZ > animationTarget)
                    {
                        animationKeyZ -= elapsedTime.ClockSeconds * tempFrameRate;
                        if (animationKeyZ <= animationTarget)
                        {
                            animationKeyZ = animationTarget;
                            containerHandlingItem.MoveZ = false;
                        }
                    }
                    else
                        containerHandlingItem.MoveZ = false;
                    if (animationKeyZ < 0)
                        animationKeyZ = 0;
                }

                if (containerHandlingItem.MoveGrabber)
                {
                    ref readonly Vector3 grabber01Position0 = ref ((LinearKey)controllerGrabber01[0]).Position;
                    ref readonly Vector3 grabber01Position1 = ref ((LinearKey)controllerGrabber01[1]).Position;
                    float animationTarget = Math.Abs((containerHandlingItem.TargetGrabber01 - grabber01Position0.Z + grabber01Position1.Z) / (grabber01Position1.Z - grabber01Position0.Z)) * controllerGrabber01[1].Frame;
                    tempFrameRate = Math.Abs(animationKeyGrabber01 - animationTarget) > slowDownThreshold ? frameRate : frameRate / 4;
                    if (animationKeyGrabber01 < animationTarget)
                    {
                        animationKeyGrabber01 += elapsedTime.ClockSeconds * tempFrameRate;
                        if (animationKeyGrabber01 >= animationTarget)
                        {
                            animationKeyGrabber01 = animationTarget;
                        }
                    }
                    else if (animationKeyGrabber01 > animationTarget)
                    {
                        animationKeyGrabber01 -= elapsedTime.ClockSeconds * tempFrameRate;
                        if (animationKeyGrabber01 <= animationTarget)
                        {
                            animationKeyGrabber01 = animationTarget;
                        }
                    }
                    if (animationKeyGrabber01 < 0)
                        animationKeyGrabber01 = 0;
                    ref readonly Vector3 grabber02Position0 = ref ((LinearKey)controllerGrabber02[0]).Position;
                    ref readonly Vector3 grabber02Position1 = ref ((LinearKey)controllerGrabber02[1]).Position;
                    float animationTarget2 = Math.Abs((containerHandlingItem.TargetGrabber02 - grabber02Position0.Z + grabber02Position1.Z) / (grabber02Position1.Z - grabber02Position0.Z)) * controllerGrabber02[1].Frame;
                    tempFrameRate = Math.Abs(animationKeyGrabber01 - animationTarget2) > slowDownThreshold ? frameRate : frameRate / 4;
                    if (animationKeyGrabber02 < animationTarget2)
                    {
                        animationKeyGrabber02 += elapsedTime.ClockSeconds * tempFrameRate;
                        if (animationKeyGrabber02 >= animationTarget2)
                        {
                            animationKeyGrabber02 = animationTarget2;
                        }
                    }
                    else if (animationKeyGrabber02 > animationTarget2)
                    {
                        animationKeyGrabber02 -= elapsedTime.ClockSeconds * tempFrameRate;
                        if (animationKeyGrabber02 <= animationTarget2)
                        {
                            animationKeyGrabber02 = animationTarget2;
                        }
                    }
                    if (animationTarget == animationKeyGrabber01 && animationTarget2 == animationKeyGrabber02)
                        containerHandlingItem.MoveGrabber = false;
                    if (animationKeyGrabber02 < 0)
                        animationKeyGrabber02 = 0;
                }
            }
            containerHandlingItem.ActualX = (((LinearKey)controllerX[1]).Position.X - ((LinearKey)controllerX[0]).Position.X) * animationKeyX / controllerX[1].Frame + ((LinearKey)controllerX[0]).Position.X;
            containerHandlingItem.ActualY = (((LinearKey)controllerY[1]).Position.Y - ((LinearKey)controllerY[0]).Position.Y) * animationKeyY / controllerY[1].Frame + ((LinearKey)controllerY[0]).Position.Y;
            containerHandlingItem.ActualZ = (((LinearKey)controllerZ[1]).Position.Z - ((LinearKey)controllerZ[0]).Position.Z) * animationKeyZ / controllerZ[1].Frame + ((LinearKey)controllerZ[0]).Position.Z;
            containerHandlingItem.ActualGrabber01 = (((LinearKey)controllerGrabber01[1]).Position.Z - ((LinearKey)controllerGrabber01[0]).Position.Z) * animationKeyGrabber01 / controllerGrabber01[1].Frame + ((LinearKey)controllerGrabber01[0]).Position.Z;
            containerHandlingItem.ActualGrabber02 = (((LinearKey)controllerGrabber02[1]).Position.Z - ((LinearKey)controllerGrabber02[0]).Position.Z) * animationKeyGrabber02 / controllerGrabber02[1].Frame + ((LinearKey)controllerGrabber02[0]).Position.Z;

            AnimateOneMatrix(animationMatrixXIndex, animationKeyX);
            AnimateOneMatrix(animationMatrixYIndex, animationKeyY);
            AnimateOneMatrix(animationMatrixZIndex, animationKeyZ);
            for (var imatrix = 0; imatrix < SharedShape.Matrices.Length; ++imatrix)
            {
                if (SharedShape.MatrixNames[imatrix].ToLower().StartsWith("cable"))
                    AnimateOneMatrix(imatrix, animationKeyY);
                else if (SharedShape.MatrixNames[imatrix].ToLower().StartsWith("grabber01"))
                    AnimateOneMatrix(imatrix, animationKeyGrabber01);
                else if (SharedShape.MatrixNames[imatrix].ToLower().StartsWith("grabber02"))
                    AnimateOneMatrix(imatrix, animationKeyGrabber02);
            }

            SharedShape.PrepareFrame(frame, WorldPosition, XNAMatrices, Flags);
            if (containerHandlingItem.ContainerAttached)
            {
                Matrix absAnimationMatrix = XNAMatrices[animationMatrixYIndex];
                absAnimationMatrix = MatrixExtension.Multiply(absAnimationMatrix, XNAMatrices[animationMatrixXIndex]);
                absAnimationMatrix = MatrixExtension.Multiply(absAnimationMatrix, XNAMatrices[animationMatrixZIndex]);
                absAnimationMatrix = MatrixExtension.Multiply(absAnimationMatrix, WorldPosition.XNAMatrix);
                containerHandlingItem.TransferContainer(absAnimationMatrix);
            }
            // let's make some noise

            if (!OldMoveX && containerHandlingItem.MoveX)
                soundSource?.HandleEvent(TrainEvent.CraneXAxisMove);
            if (OldMoveX && !containerHandlingItem.MoveX)
                soundSource?.HandleEvent(TrainEvent.CraneXAxisSlowDown);
            if (!OldMoveY && containerHandlingItem.MoveY)
                soundSource?.HandleEvent(TrainEvent.CraneYAxisMove);
            if (OldMoveY && !containerHandlingItem.MoveY)
                soundSource?.HandleEvent(TrainEvent.CraneYAxisSlowDown);
            if (!OldMoveZ && containerHandlingItem.MoveZ)
                soundSource?.HandleEvent(TrainEvent.CraneZAxisMove);
            if (OldMoveZ && !containerHandlingItem.MoveZ)
                soundSource?.HandleEvent(TrainEvent.CraneZAxisSlowDown);
            if (OldMoveY && !containerHandlingItem.MoveY && !(containerHandlingItem.TargetY == containerHandlingItem.PickingSurfaceRelativeTopStartPosition.Y))
                soundSource?.HandleEvent(TrainEvent.CraneYAxisDown);
            OldMoveX = containerHandlingItem.MoveX;
            OldMoveY = containerHandlingItem.MoveY;
            OldMoveZ = containerHandlingItem.MoveZ;
        }
    }

    public class RoadCarShape : AnimatedShape
    {
        public RoadCarShape(string path, IWorldPosition positionSource)
            : base(path, positionSource, ShapeFlags.ShadowCaster)
        {
        }
    }

}
