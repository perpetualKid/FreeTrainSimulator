// COPYRIGHT 2009, 2010, 2011, 2012, 2013 by the Open Rails project.
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

// This file is the responsibility of the 3D & Environment Team.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Api;
using FreeTrainSimulator.Common.Calc;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Common.Position;
using FreeTrainSimulator.Common.Xna;
using FreeTrainSimulator.Models.Imported.State;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.ActivityRunner.Viewer3D.RollingStock;
using Orts.ActivityRunner.Viewer3D.RollingStock.CabView;
using Orts.Formats.Msts;
using Orts.Formats.Msts.Models;
using Orts.Simulation;
using Orts.Simulation.Physics;
using Orts.Simulation.RollingStocks;
using Orts.Simulation.Signalling;
using Orts.Simulation.Track;
using Orts.Simulation.World;

namespace Orts.ActivityRunner.Viewer3D
{
    public abstract class Camera : ISaveStateApi<CameraSaveState>
    {
        #region Camera event Handling setup
        private sealed class CameraEventHandler
        {
            private static CameraEventHandler instance;

            private CameraEventHandler(Viewer viewer)
            {
                viewer.UserCommandController.AddEvent(UserCommand.CameraZoomIn, KeyEventType.KeyDown, (UserCommandArgs commandArgs, GameTime gameTime) => viewer.Camera.Zoom(1, commandArgs, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraZoomOut, KeyEventType.KeyDown, (UserCommandArgs commandArgs, GameTime gameTime) => viewer.Camera.Zoom(-1, commandArgs, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraZoomIn, KeyEventType.KeyPressed, () => viewer.Camera.ZoomCommandStart());
                viewer.UserCommandController.AddEvent(UserCommand.CameraZoomOut, KeyEventType.KeyPressed, () => viewer.Camera.ZoomCommandStart());
                viewer.UserCommandController.AddEvent(UserCommand.CameraZoomIn, KeyEventType.KeyReleased, () => viewer.Camera.ZoomCommandEnd());
                viewer.UserCommandController.AddEvent(UserCommand.CameraZoomOut, KeyEventType.KeyReleased, () => viewer.Camera.ZoomCommandEnd());
                viewer.UserCommandController.AddEvent(UserCommand.CameraToggleLetterboxCab, KeyEventType.KeyPressed, () => viewer.Camera.ToggleLetterboxCab());
                viewer.UserCommandController.AddEvent(UserCommand.CameraChangePassengerViewPoint, KeyEventType.KeyPressed, () => viewer.Camera.ChangePassengerViewPoint());
                viewer.UserCommandController.AddEvent(UserCommand.CameraScrollLeft, KeyEventType.KeyDown, (GameTime gameTime) => viewer.Camera.Scroll(false, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraScrollRight, KeyEventType.KeyDown, (GameTime gameTime) => viewer.Camera.Scroll(true, gameTime));

                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateRight, KeyEventType.KeyDown, (UserCommandArgs commandArgs, GameTime gameTime) => viewer.Camera.RotateHorizontally(1, commandArgs, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateLeft, KeyEventType.KeyDown, (UserCommandArgs commandArgs, GameTime gameTime) => viewer.Camera.RotateHorizontally(-1, commandArgs, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateUp, KeyEventType.KeyDown, (UserCommandArgs commandArgs, GameTime gameTime) => viewer.Camera.RotateVertically(1, commandArgs, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateDown, KeyEventType.KeyDown, (UserCommandArgs commandArgs, GameTime gameTime) => viewer.Camera.RotateVertically(-1, commandArgs, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateRight, KeyEventType.KeyPressed, () => viewer.Camera.RotateHorizontallyCommandStart());
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateLeft, KeyEventType.KeyPressed, () => viewer.Camera.RotateHorizontallyCommandStart());
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateUp, KeyEventType.KeyPressed, () => viewer.Camera.RotateVerticallyCommandStart());
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateDown, KeyEventType.KeyPressed, () => viewer.Camera.RotateVerticallyCommandStart());
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateRight, KeyEventType.KeyReleased, () => viewer.Camera.RotateHorizontallyCommandEnd());
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateLeft, KeyEventType.KeyReleased, () => viewer.Camera.RotateHorizontallyCommandEnd());
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateUp, KeyEventType.KeyReleased, () => viewer.Camera.RotateVerticallyCommandEnd());
                viewer.UserCommandController.AddEvent(UserCommand.CameraRotateDown, KeyEventType.KeyReleased, () => viewer.Camera.RotateVerticallyCommandEnd());

                viewer.UserCommandController.AddEvent(UserCommand.CameraPanRight, KeyEventType.KeyDown, (UserCommandArgs commandArgs, GameTime gameTime) => viewer.Camera.PanHorizontally(1, commandArgs, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanLeft, KeyEventType.KeyDown, (UserCommandArgs commandArgs, GameTime gameTime) => viewer.Camera.PanHorizontally(-1, commandArgs, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanUp, KeyEventType.KeyDown, (UserCommandArgs commandArgs, GameTime gameTime) => viewer.Camera.PanVertically(1, commandArgs, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanDown, KeyEventType.KeyDown, (UserCommandArgs commandArgs, GameTime gameTime) => viewer.Camera.PanVertically(-1, commandArgs, gameTime));
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanRight, KeyEventType.KeyPressed, () => viewer.Camera.PanHorizontallyCommandStart());
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanLeft, KeyEventType.KeyPressed, () => viewer.Camera.PanHorizontallyCommandStart());
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanRight, KeyEventType.KeyPressed, () => viewer.Camera.SwitchCabView(1));
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanLeft, KeyEventType.KeyPressed, () => viewer.Camera.SwitchCabView(-1));
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanUp, KeyEventType.KeyPressed, () => viewer.Camera.PanVerticallyCommandStart());
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanDown, KeyEventType.KeyPressed, () => viewer.Camera.PanVerticallyCommandStart());
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanRight, KeyEventType.KeyReleased, () => viewer.Camera.PanHorizontallyCommandEnd());
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanLeft, KeyEventType.KeyReleased, () => viewer.Camera.PanHorizontallyCommandEnd());
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanUp, KeyEventType.KeyReleased, () => viewer.Camera.PanVerticallyCommandEnd());
                viewer.UserCommandController.AddEvent(UserCommand.CameraPanDown, KeyEventType.KeyReleased, () => viewer.Camera.PanVerticallyCommandEnd());

                viewer.UserCommandController.AddEvent(UserCommand.CameraCarNext, KeyEventType.KeyPressed, () => viewer.Camera.CarNext());
                viewer.UserCommandController.AddEvent(UserCommand.CameraCarPrevious, KeyEventType.KeyPressed, () => viewer.Camera.CarPrevious());
                viewer.UserCommandController.AddEvent(UserCommand.CameraCarFirst, KeyEventType.KeyPressed, () => viewer.Camera.CarFirst());
                viewer.UserCommandController.AddEvent(UserCommand.CameraCarLast, KeyEventType.KeyPressed, () => viewer.Camera.CarLast());
                viewer.UserCommandController.AddEvent(UserCommand.CameraBrowseBackwards, KeyEventType.KeyPressed, () => viewer.Camera.BrowseBackwards());
                viewer.UserCommandController.AddEvent(UserCommand.CameraBrowseForwards, KeyEventType.KeyPressed, () => viewer.Camera.BrowseForwards());
                viewer.UserCommandController.AddEvent(CommonUserCommand.VerticalScrollChanged, (UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers) => viewer.Camera.ZoomByMouseCommmand(commandArgs, gameTime, modifiers));
                viewer.UserCommandController.AddEvent(CommonUserCommand.AlternatePointerDragged, (UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers) => viewer.Camera.RotateByMouseCommmand(commandArgs, gameTime, modifiers));
                viewer.UserCommandController.AddEvent(CommonUserCommand.AlternatePointerPressed, (UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers) => viewer.Camera.RotateByMouseCommmandStart());
                viewer.UserCommandController.AddEvent(CommonUserCommand.AlternatePointerReleased, (UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers) => viewer.Camera.RotateByMouseCommmandEnd());
                viewer.UserCommandController.AddEvent(CommonUserCommand.PointerPressed, (UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers) => viewer.Camera.CabControlClickCommand(commandArgs, modifiers));
                viewer.UserCommandController.AddEvent(CommonUserCommand.PointerReleased, (UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers) => viewer.Camera.CabControlReleaseCommand(commandArgs, modifiers));
                viewer.UserCommandController.AddEvent(CommonUserCommand.PointerMoved, (UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers) => viewer.Camera.CabControlPointerMovedCommand(commandArgs, modifiers));
                viewer.UserCommandController.AddEvent(CommonUserCommand.PointerDragged, (UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers) => viewer.Camera.CabControlDraggedCommand(commandArgs, modifiers));
            }

            internal static void Initialize(Viewer viewer)
            {
                if (null == instance)
                {
                    instance = new CameraEventHandler(viewer);
                }
            }
        }

        private protected virtual void ZoomCommandStart() { }
        private protected virtual void ZoomCommandEnd() { }
        private protected virtual void Zoom(int zoomSign, UserCommandArgs commandArgs, GameTime gameTime) { }
        private protected virtual void ZoomByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers) { }
        private protected virtual void RotateHorizontallyCommandStart() { }
        private protected virtual void RotateHorizontallyCommandEnd() { }
        private protected virtual void RotateHorizontally(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime) { }
        private protected virtual void RotateVerticallyCommandStart() { }
        private protected virtual void RotateVerticallyCommandEnd() { }
        private protected virtual void RotateVertically(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime) { }
        private protected virtual void PanHorizontallyCommandStart() { }
        private protected virtual void PanHorizontallyCommandEnd() { }
        private protected virtual void PanHorizontally(int panSign, UserCommandArgs commandArgs, GameTime gameTime) { }
        private protected virtual void PanVerticallyCommandStart() { }
        private protected virtual void PanVerticallyCommandEnd() { }
        private protected virtual void PanVertically(int panSign, UserCommandArgs commandArgs, GameTime gameTime) { }
        private protected virtual void SwitchCabView(int direction) { }
        private protected virtual void Scroll(bool right, GameTime gameTime) { }
        private protected virtual void ToggleLetterboxCab() { }
        private protected virtual void ChangePassengerViewPoint() { }
        private protected virtual void CarNext() { }
        private protected virtual void CarPrevious() { }
        private protected virtual void CarFirst() { }
        private protected virtual void CarLast() { }
        private protected virtual void BrowseBackwards() { }
        private protected virtual void BrowseForwards() { }
        private protected virtual void RotateByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers) { }
        private protected virtual void RotateByMouseCommmandStart() { }
        private protected virtual void RotateByMouseCommmandEnd() { }

        private protected virtual void CabControlClickCommand(UserCommandArgs commandArgs, KeyModifiers modifiers) { }
        private protected virtual void CabControlReleaseCommand(UserCommandArgs commandArgs, KeyModifiers modifiers) { }
        private protected virtual void CabControlPointerMovedCommand(UserCommandArgs commandArgs, KeyModifiers modifiers) { }
        private protected virtual void CabControlDraggedCommand(UserCommandArgs commandArgs, KeyModifiers modifiers) { }

        #endregion

        private const int SpeedFactorFastSlow = 8;  // Use by GetSpeed
        private protected const int TerrainAltitudeMargin = 2;
        private protected const float SpeedAdjustmentForRotation = 0.1f;

        private protected double commandStartTime;

        // 2.1 sets the limit at just under a right angle as get unwanted swivel at the full right angle.
        private protected static CameraAngleClamper VerticalClamper = new CameraAngleClamper(-MathHelper.Pi / 2.1f, MathHelper.Pi / 2.1f);

        private protected readonly Viewer viewer;
        private protected WorldLocation cameraLocation;
        private Matrix xnaView;
        private Matrix projection;
        private static Matrix skyProjection;
        private static Matrix distantMountainProjection;

        private Vector3 frustumRightProjected;
        private Vector3 frustumLeft;
        private Vector3 frustumRight;

        // We need to allow different cameras to have different near planes.
        private protected float NearPlane = 1.0f;

        public ref readonly Tile Tile => ref cameraLocation.Tile;
        public ref readonly Vector3 Location => ref cameraLocation.Location;
        public ref readonly WorldLocation CameraWorldLocation => ref cameraLocation;

        public float FieldOfView { get; set; }

        public ref Matrix XnaView => ref xnaView;

        public bool ViewChanged { get; private set; }

        public ref Matrix XnaProjection => ref projection;

        public static ref Matrix XnaDistantMountainProjection => ref distantMountainProjection;

        // This sucks. It's really not camera-related at all.
        public static ref Matrix XNASkyProjection => ref skyProjection;

        // The following group of properties are used by other code to vary
        // behavior by camera; e.g. Style is used for activating sounds,
        // AttachedCar for rendering the train or not, and IsUnderground for
        // automatically switching to/from cab view in tunnels.
        public CameraStyle Style { get; protected set; } = CameraStyle.External;
        public TrainCar AttachedCar { get; protected set; }
        public virtual bool IsAvailable { get; protected set; } = true;
        public virtual bool IsUnderground { get; protected set; }
        public string Name { get; protected set; } = string.Empty;

        /// <summary>
        /// All OpenAL sound positions are normalized to this tile.
        /// Cannot be (0, 0) constantly, because some routes use extremely large tile coordinates,
        /// which would lead to imprecise absolute world coordinates, thus stuttering.
        /// </summary>
        private static Tile soundBaseTile = Tile.Zero;
        public static ref readonly Tile SoundBaseTile => ref soundBaseTile;


        protected Camera(Viewer viewer)
        {
            this.viewer = viewer ?? throw new ArgumentNullException(nameof(viewer));
            FieldOfView = this.viewer.Settings.ViewingFOV;
            CameraEventHandler.Initialize(viewer);
        }

        protected Camera(Viewer viewer, Camera previousCamera) // maintain visual continuity
            : this(viewer)
        {
            if (previousCamera != null)
            {
                cameraLocation = previousCamera.CameraWorldLocation;
                FieldOfView = previousCamera.FieldOfView;
            }
        }

        /// <summary>
        /// Resets a camera's position, location and attachment information.
        /// </summary>
        public virtual void Reset()
        {
            FieldOfView = viewer.Settings.ViewingFOV;
            ScreenChanged();
        }

        /// <summary>
        /// Switches the <see cref="Viewer3D"/> to this camera, updating the view information.
        /// </summary>
        public void Activate()
        {
            ScreenChanged();
            OnActivate(viewer.Camera == this);
            viewer.Camera = this;
            viewer.Simulator.PlayerIsInCab = Style == CameraStyle.Cab || Style == CameraStyle.Cab3D;
            Update(ElapsedTime.Zero);
            Matrix currentView = xnaView;
            xnaView = GetCameraView();
            ViewChanged = currentView != xnaView;
            soundBaseTile = cameraLocation.Tile;
        }

        /// <summary>
        /// A camera can use this method to handle any preparation when being activated.
        /// </summary>
        protected virtual void OnActivate(bool sameCamera)
        {
        }

        /// <summary>
        /// A camera can use this method to update any calculated data that may have changed.
        /// </summary>
        /// <param name="elapsedTime"></param>
        public virtual void Update(in ElapsedTime elapsedTime)
        {
        }

        /// <summary>
        /// A camera should use this method to return a unique view.
        /// </summary>
        protected abstract Matrix GetCameraView();

        /// <summary>
        /// Notifies the camera that the screen dimensions have changed.
        /// </summary>
        public void ScreenChanged()
        {
            var aspectRatio = (float)viewer.DisplaySize.X / viewer.DisplaySize.Y;
            var farPlaneDistance = SkyConstants.Radius + 100;  // so far the sky is the biggest object in view
            var fovWidthRadians = MathHelper.ToRadians(FieldOfView);

            if (viewer.Settings.DistantMountains)
                distantMountainProjection = Matrix.CreatePerspectiveFieldOfView(fovWidthRadians, aspectRatio, MathHelper.Clamp(viewer.Settings.ViewingDistance - 500, 500, 1500), viewer.Settings.DistantMountainsViewingDistance);
            projection = Matrix.CreatePerspectiveFieldOfView(fovWidthRadians, aspectRatio, NearPlane, viewer.Settings.ViewingDistance);
            skyProjection = Matrix.CreatePerspectiveFieldOfView(fovWidthRadians, aspectRatio, NearPlane, farPlaneDistance);    // TODO remove? 
            frustumRightProjected.X = (float)Math.Cos(fovWidthRadians / 2 * aspectRatio);  // Precompute the right edge of the view frustrum.
            frustumRightProjected.Z = (float)Math.Sin(fovWidthRadians / 2 * aspectRatio);
        }

        /// <summary>
        /// Updates view and projection from this camera's data.
        /// </summary>
        /// <param name="frame"></param>
        /// <param name="elapsedTime"></param>
        public void PrepareFrame(RenderFrame frame, in ElapsedTime elapsedTime)
        {
            Matrix currentView = xnaView;
            xnaView = GetCameraView();
            ViewChanged = currentView != xnaView;
            frame.SetCamera(this);
            frustumLeft.X = -xnaView.M11 * frustumRightProjected.X + xnaView.M13 * frustumRightProjected.Z;
            frustumLeft.Y = -xnaView.M21 * frustumRightProjected.X + xnaView.M23 * frustumRightProjected.Z;
            frustumLeft.Z = -xnaView.M31 * frustumRightProjected.X + xnaView.M33 * frustumRightProjected.Z;
            frustumLeft.Normalize();
            frustumRight.X = xnaView.M11 * frustumRightProjected.X + xnaView.M13 * frustumRightProjected.Z;
            frustumRight.Y = xnaView.M21 * frustumRightProjected.X + xnaView.M23 * frustumRightProjected.Z;
            frustumRight.Z = xnaView.M31 * frustumRightProjected.X + xnaView.M33 * frustumRightProjected.Z;
            frustumRight.Normalize();
        }

        // Cull for fov
        public bool InFov(Vector3 mstsObjectCenter, float objectRadius)
        {
            mstsObjectCenter.X -= cameraLocation.Location.X;
            mstsObjectCenter.Y -= cameraLocation.Location.Y;
            mstsObjectCenter.Z -= cameraLocation.Location.Z;
            // TODO: This *2 is a complete fiddle because some objects don't currently pass in a correct radius and e.g. track sections vanish.
            objectRadius *= 2;
            if (frustumLeft.X * mstsObjectCenter.X + frustumLeft.Y * mstsObjectCenter.Y - frustumLeft.Z * mstsObjectCenter.Z > objectRadius)
                return false;
            if (frustumRight.X * mstsObjectCenter.X + frustumRight.Y * mstsObjectCenter.Y - frustumRight.Z * mstsObjectCenter.Z > objectRadius)
                return false;
            return true;
        }

        // Cull for distance
        public bool InRange(Vector3 mstsObjectCenter, float objectRadius, float objectViewingDistance)
        {
            mstsObjectCenter.X -= cameraLocation.Location.X;
            mstsObjectCenter.Z -= cameraLocation.Location.Z;

            // An object cannot be visible further away than the viewing distance.
            if (objectViewingDistance > viewer.Settings.ViewingDistance)
                objectViewingDistance = viewer.Settings.ViewingDistance;

            var distanceSquared = mstsObjectCenter.X * mstsObjectCenter.X + mstsObjectCenter.Z * mstsObjectCenter.Z;

            return distanceSquared < (objectRadius + objectViewingDistance) * (objectRadius + objectViewingDistance);
        }

        /// <summary>
        /// If the nearest part of the object is within camera viewing distance
        /// and is within the object's defined viewing distance then
        /// we can see it.   The objectViewingDistance allows a small object
        /// to specify a cutoff beyond which the object can't be seen.
        /// </summary>
        public bool CanSee(Vector3 mstsObjectCenter, float objectRadius, float objectViewingDistance)
        {
            if (!InRange(mstsObjectCenter, objectRadius, objectViewingDistance))
                return false;

            if (!InFov(mstsObjectCenter, objectRadius))
                return false;

            return true;
        }

        private protected static float GetSpeed(GameTime gameTime, UserCommandArgs userCommandArgs, Viewer viewer)
        {
            double speed = 5 * gameTime.ElapsedGameTime.TotalSeconds;
            if (userCommandArgs is ModifiableKeyCommandArgs modifiableKeyCommandArgs)
            {
                if (modifiableKeyCommandArgs.AdditionalModifiers.HasFlag(viewer.UserSettings.KeyboardSettings.CameraMoveFastModifier))
                    speed *= SpeedFactorFastSlow;
                else if (modifiableKeyCommandArgs.AdditionalModifiers.HasFlag(viewer.UserSettings.KeyboardSettings.CameraMoveSlowModifier))
                    speed /= SpeedFactorFastSlow;
            }
            return (float)speed;
        }

        private protected static float GetSpeed(GameTime gameTime, UserCommandArgs userCommandArgs, KeyModifiers modifiers, Viewer viewer)
        {
            if (userCommandArgs is ScrollCommandArgs scrollCommandArgs)
            {
                double speed = 5 * gameTime.ElapsedGameTime.TotalSeconds;
                if (modifiers.HasFlag(viewer.UserSettings.KeyboardSettings.CameraMoveFastModifier))
                    speed *= SpeedFactorFastSlow;
                else if (modifiers.HasFlag(viewer.UserSettings.KeyboardSettings.CameraMoveSlowModifier))
                    speed /= SpeedFactorFastSlow;
                return (float)speed * scrollCommandArgs.Delta;
            }
            return 0;
        }

        protected virtual void ZoomIn(float speed)
        {
        }

        // TODO: Add a way to record this zoom operation for Replay.
        private protected void ZoomByMouse(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers, float speedAdjustmentFactor = 1)
        {
            float fieldOfView = MathHelper.Clamp(FieldOfView - GetSpeed(gameTime, commandArgs, modifiers, viewer) * speedAdjustmentFactor / 10, 1, 135);
            _ = new FieldOfViewCommand(viewer.Log, fieldOfView);
        }

        /// <summary>
        /// Returns a position in XNA space relative to the camera's tile
        /// </summary>
        /// <param name="worldLocation"></param>
        /// <returns></returns>
        public Vector3 XnaLocation(in WorldLocation worldLocation) => (worldLocation.Location + (worldLocation.Tile - cameraLocation.Tile).TileVector()).XnaVector();

        protected class CameraAngleClamper
        {
            private readonly float Minimum;
            private readonly float Maximum;

            public CameraAngleClamper(float minimum, float maximum)
            {
                Minimum = minimum;
                Maximum = maximum;
            }

            public float Clamp(float angle)
            {
                return MathHelper.Clamp(angle, Minimum, Maximum);
            }
        }

        /// <summary>
        /// Set OpenAL listener position based on CameraWorldLocation normalized to SoundBaseTile
        /// </summary>
        public void UpdateListener()
        {
            Vector3 listenerLocation = CameraWorldLocation.NormalizeTo(SoundBaseTile).Location;
            float[] cameraPosition = new float[] {
                        listenerLocation.X,
                        listenerLocation.Y,
                        listenerLocation.Z};

            float[] cameraVelocity = new float[] { 0, 0, 0 };

            if (!(this is TracksideCamera) && !(this is FreeRoamCamera) && AttachedCar != null)
            {
                var cars = viewer.World.Trains.Cars;
                if (cars.TryGetValue(AttachedCar, out TrainCarViewer value))
                    cameraVelocity = value.Velocity;
                else
                    cameraVelocity = new float[] { 0, 0, 0 };
            }

            float[] cameraOrientation = new float[] {
                        XnaView.Backward.X, XnaView.Backward.Y, XnaView.Backward.Z,
                        XnaView.Down.X, XnaView.Down.Y, XnaView.Down.Z };

            OpenAL.Listenerfv(OpenAL.AL_POSITION, cameraPosition);
            OpenAL.Listenerfv(OpenAL.AL_VELOCITY, cameraVelocity);
            OpenAL.Listenerfv(OpenAL.AL_ORIENTATION, cameraOrientation);
        }

        public virtual ValueTask<CameraSaveState> Snapshot()
        {
            return ValueTask.FromResult(new CameraSaveState()
            {
                Location = cameraLocation,
                FieldOfView = FieldOfView,
            });
        }

        public virtual ValueTask Restore(CameraSaveState saveState)
        {
            ArgumentNullException.ThrowIfNull(saveState, nameof(saveState));

            FieldOfView = saveState.FieldOfView;
            cameraLocation = saveState.Location;
            return ValueTask.CompletedTask;
        }
    }

    public abstract class RotatingCamera : Camera
    {
        // Current camera values
        private protected float rotationXRadians;
        private protected float rotationYRadians;
        private protected float xRadians;
        private protected float yRadians;
        private protected float zRadians;

        // Target camera values
        private protected float? rotationXTarget;
        private protected float? rotationYTarget;
        private protected float? moveXTarget;
        private protected float? moveYTarget;
        private protected float? moveZTarget;
        private protected double endTime;

        protected RotatingCamera(Viewer viewer)
            : base(viewer)
        {
        }

        protected RotatingCamera(Viewer viewer, Camera previousCamera)
            : base(viewer, previousCamera)
        {
            if (previousCamera != null)
            {
                previousCamera.XnaView.MatrixToAngles(out float h, out float a, out float b);
                rotationXRadians = -b;
                rotationYRadians = -h;
            }
        }


        public override async ValueTask<CameraSaveState> Snapshot()
        {
            CameraSaveState saveState = await base.Snapshot().ConfigureAwait(false);
            saveState.TrackingRotation = new System.Numerics.Vector3(rotationXRadians, rotationYRadians, 0);
            return saveState;
        }

        public override async ValueTask Restore([NotNull] CameraSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(true);
            rotationXRadians = saveState.TrackingRotation.X;
            rotationYRadians = saveState.TrackingRotation.Y;
        }

        public override void Reset()
        {
            base.Reset();
            rotationXRadians = rotationYRadians = xRadians = yRadians = zRadians = 0;
        }

        protected override Matrix GetCameraView()
        {
            var lookAtPosition = Vector3.UnitZ;
            lookAtPosition = Vector3.Transform(lookAtPosition, Matrix.CreateRotationX(rotationXRadians));
            lookAtPosition = Vector3.Transform(lookAtPosition, Matrix.CreateRotationY(rotationYRadians));
            lookAtPosition += cameraLocation.Location;
            lookAtPosition.Z *= -1;
            return Matrix.CreateLookAt(XnaLocation(cameraLocation), lookAtPosition, Vector3.Up);
        }

        private protected static float GetMouseDelta(float delta, GameTime gameTime, KeyModifiers keyModifiers, Viewer viewer)
        {
            // Ignore CameraMoveFast as that is too fast to be useful
            delta *= 0.005f;
            if (keyModifiers.HasFlag(viewer.UserSettings.KeyboardSettings.CameraMoveSlowModifier))
                delta *= 0.1f;
            return delta;
        }

        private protected virtual void RotateByMouse(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers)
        {
            PointerMoveCommandArgs pointerMoveCommandArgs = commandArgs as PointerMoveCommandArgs;

            // Mouse movement doesn't use 'var speed' because the MouseMove 
            // parameters are already scaled down with increasing frame rates, 
            rotationXRadians += GetMouseDelta(pointerMoveCommandArgs.Delta.Y, gameTime, modifiers, viewer);
            rotationYRadians += GetMouseDelta(pointerMoveCommandArgs.Delta.X, gameTime, modifiers, viewer);
        }

        private protected override void RotateByMouseCommmandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void RotateByMouseCommmandEnd()
        {
            var commandEndTime = viewer.Simulator.ClockTime;
            _ = new CameraMouseRotateCommand(viewer.Log, commandStartTime, commandEndTime, rotationXRadians, rotationYRadians);
        }

        protected void UpdateRotation(in ElapsedTime elapsedTime)
        {
            var replayRemainingS = endTime - viewer.Simulator.ClockTime;
            if (replayRemainingS > 0)
            {
                var replayFraction = elapsedTime.ClockSeconds / replayRemainingS;
                if (rotationXTarget != null && rotationYTarget != null)
                {
                    var replayRemainingX = rotationXTarget - rotationXRadians;
                    var replayRemainingY = rotationYTarget - rotationYRadians;
                    var replaySpeedX = (float)(replayRemainingX * replayFraction);
                    var replaySpeedY = (float)(replayRemainingY * replayFraction);

                    if (IsCloseEnough(rotationXRadians, rotationXTarget, replaySpeedX))
                    {
                        rotationXTarget = null;
                    }
                    else
                    {
                        RotateDown(replaySpeedX);
                    }
                    if (IsCloseEnough(rotationYRadians, rotationYTarget, replaySpeedY))
                    {
                        rotationYTarget = null;
                    }
                    else
                    {
                        RotateRight(replaySpeedY);
                    }
                }
                else
                {
                    if (rotationXTarget != null)
                    {
                        var replayRemainingX = rotationXTarget - rotationXRadians;
                        var replaySpeedX = (float)(replayRemainingX * replayFraction);
                        if (IsCloseEnough(rotationXRadians, rotationXTarget, replaySpeedX))
                        {
                            rotationXTarget = null;
                        }
                        else
                        {
                            RotateDown(replaySpeedX);
                        }
                    }
                    if (rotationYTarget != null)
                    {
                        var replayRemainingY = rotationYTarget - rotationYRadians;
                        var replaySpeedY = (float)(replayRemainingY * replayFraction);
                        if (IsCloseEnough(rotationYRadians, rotationYTarget, replaySpeedY))
                        {
                            rotationYTarget = null;
                        }
                        else
                        {
                            RotateRight(replaySpeedY);
                        }
                    }
                }
            }
        }

        protected virtual void RotateDown(float speed)
        {
            rotationXRadians += speed;
            rotationXRadians = VerticalClamper.Clamp(rotationXRadians);
            MoveCamera();
        }

        protected virtual void RotateRight(float speed)
        {
            rotationYRadians += speed;
            MoveCamera();
        }

        protected void MoveCamera()
        {
            MoveCamera(Vector3.Zero);
        }

        protected void MoveCamera(float x, float y, float z)
        {
            MoveCamera(new Vector3(x, y, z));
        }

        protected void MoveCamera(Vector3 movement)
        {
            Matrix matrix = Matrix.CreateRotationX(rotationXRadians);
            Vector3.Transform(ref movement, ref matrix, out movement);
            matrix = Matrix.CreateRotationY(rotationYRadians);
            Vector3.Transform(ref movement, ref matrix, out movement);
            cameraLocation = new WorldLocation(cameraLocation.Tile, cameraLocation.Location + movement, true);
        }

        /// <summary>
        /// A margin of half a step (increment/2) is used to prevent hunting once the target is reached.
        /// </summary>
        /// <param name="current"></param>
        /// <param name="target"></param>
        /// <param name="increment"></param>
        /// <returns></returns>
        protected static bool IsCloseEnough(float current, float? target, float increment)
        {
            Trace.Assert(target != null, "Camera target position must not be null");
            // If a pause interrupts a camera movement, then the increment will become zero.
            if (increment == 0)
            {  // To avoid divide by zero error, just kill the movement.
                return true;
            }
            else
            {
                var error = (float)target - current;
                return error / increment < 0.5;
            }
        }

        public void TargetRotateUpDown(float targetX, double endTime)
        {
            rotationXTarget = targetX;
            this.endTime = endTime;
        }

        public void TargetRotateLeftRight(float targetY, double endTime)
        {
            rotationYTarget = targetY;
            this.endTime = endTime;
        }

        public void TargetRotateByMouse(float targetX, float targetY, double endTime)
        {
            rotationXTarget = targetX;
            rotationYTarget = targetY;
            this.endTime = endTime;
        }

        public void TargetMoveX(float targetX, double endTime)
        {
            moveXTarget = targetX;
            this.endTime = endTime;
        }

        public void TargetMoveY(float targetY, double endTime)
        {
            moveYTarget = targetY;
            this.endTime = endTime;
        }

        public void TargetMoveZ(float targetZ, double endTime)
        {
            moveZTarget = targetZ;
            this.endTime = endTime;
        }
    }

    public abstract class LookAtCamera : RotatingCamera
    {
        private protected WorldLocation targetLocation;

        public ref readonly WorldLocation TargetWorldLocation => ref targetLocation;

        public override bool IsUnderground
        {
            get
            {
                float elevationAtTarget = viewer.Tiles.GetElevation(targetLocation);
                return targetLocation.Location.Y + TerrainAltitudeMargin < elevationAtTarget;
            }
        }

        protected LookAtCamera(Viewer viewer)
            : base(viewer)
        {
        }

        public override async ValueTask<CameraSaveState> Snapshot()
        {
            CameraSaveState saveState = await base.Snapshot().ConfigureAwait(false);
            saveState.TargetLocation = targetLocation;
            return saveState;
        }

        public override async ValueTask Restore([NotNull] CameraSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            targetLocation = saveState.TargetLocation;
        }

        protected override Matrix GetCameraView()
        {
            return Matrix.CreateLookAt(XnaLocation(cameraLocation), XnaLocation(targetLocation), Vector3.UnitY);
        }
    }

    public class FreeRoamCamera : RotatingCamera
    {
        private const float ZoomFactor = 2f;

        public FreeRoamCamera(Viewer viewer, Camera previousCamera)
            : base(viewer, previousCamera)
        {
            Name = Viewer.Catalog.GetString("Free");
        }

        public void SetLocation(in WorldLocation location)
        {
            cameraLocation = location;
        }

        public override void Reset()
        {
            // Intentionally do nothing at all.
        }

        private protected override void ZoomCommandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void ZoomCommandEnd()
        {
            _ = new CameraZCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, zRadians);
        }

        private protected override void Zoom(int zoomSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer);
            ZoomIn(zoomSign * speed * ZoomFactor);
        }

        private protected override void RotateHorizontallyCommandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void RotateHorizontallyCommandEnd()
        {
            _ = new CameraRotateLeftRightCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, rotationYRadians);
        }

        private protected override void RotateHorizontally(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            RotateRight(rotateSign * speed);
        }

        private protected override void RotateVerticallyCommandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void RotateVerticallyCommandEnd()
        {
            _ = new CameraRotateUpDownCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, rotationXRadians);
        }

        private protected override void RotateVertically(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            RotateDown(-rotateSign * speed);
        }

        private protected override void PanHorizontallyCommandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void PanHorizontallyCommandEnd()
        {
            _ = new CameraXCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, xRadians);
        }

        private protected override void PanHorizontally(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer);
            PanRight(panSign * speed);
        }

        private protected override void PanVerticallyCommandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void PanVerticallyCommandEnd()
        {
            _ = new CameraYCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, yRadians);
        }

        private protected override void PanVertically(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer);
            PanUp(panSign * speed);
        }

        private protected override void ZoomByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers)
        {
            ZoomByMouse(commandArgs, gameTime, modifiers);
        }

        private protected override void RotateByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers)
        {
            RotateByMouse(commandArgs, gameTime, modifiers);
        }

        public override void Update(in ElapsedTime elapsedTime)
        {
            UpdateRotation(elapsedTime);

            double replayRemainingS = endTime - viewer.Simulator.ClockTime;
            if (replayRemainingS > 0)
            {
                double replayFraction = elapsedTime.ClockSeconds / replayRemainingS;
                // Panning
                if (moveXTarget != null)
                {
                    float? replayRemainingX = moveXTarget - xRadians;
                    float replaySpeedX = Math.Abs((float)(replayRemainingX * replayFraction));
                    if (IsCloseEnough(xRadians, moveXTarget, replaySpeedX))
                    {
                        moveXTarget = null;
                    }
                    else
                    {
                        PanRight(replaySpeedX);
                    }
                }
                if (moveYTarget != null)
                {
                    float? replayRemainingY = moveYTarget - yRadians;
                    float replaySpeedY = Math.Abs((float)(replayRemainingY * replayFraction));
                    if (IsCloseEnough(yRadians, moveYTarget, replaySpeedY))
                    {
                        moveYTarget = null;
                    }
                    else
                    {
                        PanUp(replaySpeedY);
                    }
                }
                // Zooming
                if (moveZTarget != null)
                {
                    float? replayRemainingZ = moveZTarget - zRadians;
                    float replaySpeedZ = Math.Abs((float)(replayRemainingZ * replayFraction));
                    if (IsCloseEnough(zRadians, moveZTarget, replaySpeedZ))
                    {
                        moveZTarget = null;
                    }
                    else
                    {
                        ZoomIn(replaySpeedZ);
                    }
                }
            }
            UpdateListener();
        }

        protected virtual void PanRight(float speed)
        {
            xRadians += speed;
            MoveCamera(speed, 0, 0);
        }

        protected virtual void PanUp(float speed)
        {
            speed = VerticalClamper.Clamp(speed);    // Only the vertical needs to be clamped
            yRadians += speed;
            MoveCamera(0, speed, 0);
        }

        protected override void ZoomIn(float speed)
        {
            zRadians += speed;
            MoveCamera(0, 0, speed);
        }
    }

    public abstract class AttachedCamera : RotatingCamera
    {
        private protected Vector3 attachedLocation;
        private protected WorldPosition lookedAtPosition = WorldPosition.None;

        protected AttachedCamera(Viewer viewer)
            : base(viewer)
        {
        }

        public async override ValueTask<CameraSaveState> Snapshot()
        {
            CameraSaveState saveState = await base.Snapshot().ConfigureAwait(false);
            if (AttachedCar?.Train != null && AttachedCar.Train == viewer.SelectedTrain)
                saveState.AttachedTrainCarIndex = viewer.SelectedTrain.Cars.IndexOf(AttachedCar);
            else
                saveState.AttachedTrainCarIndex = -1;
            saveState.TargetLocation = new WorldLocation(0, 0, attachedLocation);
            return saveState;
        }

        public override async ValueTask Restore([NotNull] CameraSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            if (saveState.AttachedTrainCarIndex > -1)
            {
                if (saveState.AttachedTrainCarIndex < viewer.SelectedTrain.Cars.Count)
                    AttachedCar = viewer.SelectedTrain.Cars[saveState.AttachedTrainCarIndex];
                else if (viewer.SelectedTrain.Cars.Count > 0)
                    AttachedCar = viewer.SelectedTrain.Cars[^1];
            }
            attachedLocation = saveState.TargetLocation.Location;
        }

        protected override void OnActivate(bool sameCamera)
        {
            if (AttachedCar == null || AttachedCar.Train != viewer.SelectedTrain)
            {
                if (viewer.SelectedTrain.MUDirection != MidpointDirection.Reverse)
                    SetCameraCar(GetCameraCars().First());
                else
                    SetCameraCar(GetCameraCars().Last());
            }
            base.OnActivate(sameCamera);
        }

        private protected virtual List<TrainCar> GetCameraCars()
        {
            if (viewer.SelectedTrain.TrainType == TrainType.AiIncorporated)
                viewer.ChangeSelectedTrain(viewer.SelectedTrain.IncorporatingTrain);
            return viewer.SelectedTrain.Cars;
        }

        protected virtual void SetCameraCar(TrainCar car)
        {
            AttachedCar = car;
        }

        protected virtual bool IsCameraFlipped()
        {
            return false;
        }

        public virtual void NextCar()
        {
            var trainCars = GetCameraCars().ToList();
            SetCameraCar(AttachedCar == trainCars.First() ? AttachedCar : trainCars[trainCars.IndexOf(AttachedCar) - 1]);
        }

        public virtual void PreviousCar()
        {
            var trainCars = GetCameraCars().ToList();
            SetCameraCar(AttachedCar == trainCars.Last() ? AttachedCar : trainCars[trainCars.IndexOf(AttachedCar) + 1]);
        }

        public virtual void FirstCar()
        {
            var trainCars = GetCameraCars();
            SetCameraCar(trainCars.First());
        }

        public virtual void LastCar()
        {
            var trainCars = GetCameraCars();
            SetCameraCar(trainCars.Last());
        }

        public void UpdateLocation(in WorldPosition worldPosition)
        {
            Vector3 source = IsCameraFlipped() ? new Vector3(-attachedLocation.X, attachedLocation.Y, attachedLocation.Z) :
                new Vector3(attachedLocation.X, attachedLocation.Y, -attachedLocation.Z);
            Vector3.Transform(source, worldPosition.XNAMatrix).Deconstruct(out float x, out float y, out float z);
            cameraLocation = new WorldLocation(worldPosition.Tile, x, y, -z);
        }

        protected override Matrix GetCameraView()
        {
            var flipped = IsCameraFlipped();
            var lookAtPosition = Vector3.UnitZ;
            lookAtPosition = Vector3.Transform(lookAtPosition, Matrix.CreateRotationX(rotationXRadians));
            lookAtPosition = Vector3.Transform(lookAtPosition, Matrix.CreateRotationY(rotationYRadians + (flipped ? MathHelper.Pi : 0)));
            if (flipped)
            {
                lookAtPosition.X -= attachedLocation.X;
                lookAtPosition.Y += attachedLocation.Y;
                lookAtPosition.Z -= attachedLocation.Z;
            }
            else
            {
                lookAtPosition.X += attachedLocation.X;
                lookAtPosition.Y += attachedLocation.Y;
                lookAtPosition.Z += attachedLocation.Z;
            }
            lookAtPosition.Z *= -1;
            lookAtPosition = Vector3.Transform(lookAtPosition, viewer.Camera is TrackingCamera ? lookedAtPosition.XNAMatrix : AttachedCar.WorldPosition.XNAMatrix);
            // Don't forget to rotate the up vector so the camera rotates with us.
            Vector3 up;
            if (viewer.Camera is TrackingCamera)
                up = Vector3.Up;
            else
            {
                var upRotation = AttachedCar.WorldPosition.XNAMatrix;
                upRotation.Translation = Vector3.Zero;
                up = Vector3.Transform(Vector3.Up, upRotation);
            }
            return Matrix.CreateLookAt(XnaLocation(cameraLocation), lookAtPosition, up);
        }

        public override void Update(in ElapsedTime elapsedTime)
        {
            if (AttachedCar != null)
            {
                Vector3 source = IsCameraFlipped() ? new Vector3(-attachedLocation.X, attachedLocation.Y, attachedLocation.Z) :
                    new Vector3(attachedLocation.X, attachedLocation.Y, -attachedLocation.Z);
                Vector3.Transform(source, AttachedCar.WorldPosition.XNAMatrix).Deconstruct(out float x, out float y, out float z);
                cameraLocation = new WorldLocation(AttachedCar.WorldPosition.Tile, x, y, -z);
            }
            UpdateRotation(elapsedTime);
            UpdateListener();
        }
    }

    public class TrackingCamera : AttachedCamera
    {
        private const float BrowseSpeedMpS = 4;
        private const float StartPositionDistance = 20;
        private const float StartPositionXRadians = 0.399f;
        private const float StartPositionYRadians = 0.387f;
        private const float ZoomFactor = 0.1f;

        private readonly bool attachedToFront;

        private protected float positionDistance = StartPositionDistance;
        private protected float positionXRadians = StartPositionXRadians;
        private protected float positionYRadians = StartPositionYRadians;

        private float? positionDistanceTarget;
        private float? positionXTarget;
        private float? positionYTarget;

        private bool browseBackwards;
        private bool browseForwards;
        private float zDistanceM; // used to browse train;
        private Traveller browsedTraveller;
        private float browseDistance = 20;
        private bool browseMode;
        private float wagonOffsetLowerLimit;
        private float wagonOffsetUpperLimit;

        public override bool IsUnderground
        {
            get
            {
                var elevationAtTrain = viewer.Tiles.GetElevation(lookedAtPosition.WorldLocation);
                var elevationAtCamera = viewer.Tiles.GetElevation(cameraLocation);
                return lookedAtPosition.WorldLocation.Location.Y + TerrainAltitudeMargin < elevationAtTrain || cameraLocation.Location.Y + TerrainAltitudeMargin < elevationAtCamera;
            }
        }

        public TrackingCamera(Viewer viewer, bool attachedToFront)
            : base(viewer)
        {
            this.attachedToFront = attachedToFront;
            Name = attachedToFront ? Viewer.Catalog.GetString("Outside Front") : Viewer.Catalog.GetString("Outside Rear");
            positionYRadians = StartPositionYRadians + (this.attachedToFront ? 0 : MathHelper.Pi);
            rotationXRadians = positionXRadians;
            rotationYRadians = positionYRadians - MathHelper.Pi;
        }

        public override async ValueTask<CameraSaveState> Snapshot()
        {
            CameraSaveState saveState = await base.Snapshot().ConfigureAwait(false);
            saveState.BrowseTracking = browseMode;
            saveState.BrowseForward = browseForwards;
            saveState.BrowseBackward = browseBackwards;
            saveState.TrackingPosition = new System.Numerics.Vector3(positionXRadians, positionYRadians, positionDistance);
            saveState.Distance = zDistanceM;
            return saveState;
        }

        public async override ValueTask Restore([NotNull] CameraSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            browseMode = saveState.BrowseTracking;
            browseForwards = saveState.BrowseForward;
            browseBackwards = saveState.BrowseBackward;
            positionXRadians = saveState.TrackingPosition.X;
            positionYRadians = saveState.TrackingPosition.Y;
            positionDistance = saveState.TrackingPosition.Z;
            zDistanceM = saveState.Distance;

            if (AttachedCar != null && AttachedCar.Train == viewer.SelectedTrain)
            {
                IEnumerable<TrainCar> trainCars = GetCameraCars();
                browseDistance = AttachedCar.CarLengthM * 0.5f;
                if (attachedToFront)
                {
                    browsedTraveller = new Traveller(AttachedCar.Train.FrontTDBTraveller);
                    browsedTraveller.Move(-AttachedCar.CarLengthM * 0.5f + zDistanceM);
                }
                else
                {
                    browsedTraveller = new Traveller(AttachedCar.Train.RearTDBTraveller);
                    browsedTraveller.Move((AttachedCar.CarLengthM - trainCars.First().CarLengthM - trainCars.Last().CarLengthM) * 0.5f + AttachedCar.Train.Length - zDistanceM);
                }
                //               LookedAtPosition = new WorldPosition(attachedCar.WorldPosition);
                ComputeCarOffsets(this);
            }

        }

        public override void Reset()
        {
            base.Reset();
            positionDistance = StartPositionDistance;
            positionXRadians = StartPositionXRadians;
            positionYRadians = StartPositionYRadians + (attachedToFront ? 0 : MathHelper.Pi);
            rotationXRadians = positionXRadians;
            rotationYRadians = positionYRadians - MathHelper.Pi;
        }

        public void MoveXTarget(float targetX, double endTime)
        {
            positionXTarget = targetX;
            base.endTime = endTime;
        }

        public void MoveYTarget(float targetY, double endTime)
        {
            positionYTarget = targetY;
            base.endTime = endTime;
        }

        public void MoveDistanceTarget(float targetDistance, double endTime)
        {
            positionDistanceTarget = targetDistance;
            base.endTime = endTime;
        }

        protected override void OnActivate(bool sameCamera)
        {
            browseMode = browseForwards = browseBackwards = false;
            if (AttachedCar == null || AttachedCar.Train != viewer.SelectedTrain)
            {
                if (attachedToFront)
                {
                    SetCameraCar(GetCameraCars().First());
                    browsedTraveller = new Traveller(AttachedCar.Train.FrontTDBTraveller);
                    zDistanceM = -AttachedCar.CarLengthM / 2;
                    wagonOffsetUpperLimit = 0;
                    wagonOffsetLowerLimit = -AttachedCar.CarLengthM;
                }
                else
                {
                    var trainCars = GetCameraCars();
                    SetCameraCar(trainCars.Last());
                    browsedTraveller = new Traveller(AttachedCar.Train.RearTDBTraveller);
                    zDistanceM = -AttachedCar.Train.Length + (trainCars.First().CarLengthM + trainCars.Last().CarLengthM) * 0.5f + AttachedCar.CarLengthM / 2;
                    wagonOffsetLowerLimit = -AttachedCar.Train.Length + trainCars.First().CarLengthM * 0.5f;
                    wagonOffsetUpperLimit = wagonOffsetLowerLimit + AttachedCar.CarLengthM;
                }
                browseDistance = AttachedCar.CarLengthM * 0.5f;
            }
            base.OnActivate(sameCamera);
        }

        protected override bool IsCameraFlipped()
        {
            return !browseMode && AttachedCar.Flipped;
        }

        private protected override void ZoomCommandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void ZoomCommandEnd()
        {
            _ = new TrackingCameraZCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, positionDistance);
        }

        private protected override void Zoom(int zoomSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer);
            ZoomIn(zoomSign * speed * ZoomFactor);
        }

        private protected override void RotateHorizontallyCommandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void RotateHorizontallyCommandEnd()
        {
            _ = new CameraRotateLeftRightCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, rotationYRadians);
        }

        private protected override void RotateHorizontally(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            RotateRight(rotateSign * speed);
        }

        private protected override void RotateVerticallyCommandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void RotateVerticallyCommandEnd()
        {
            _ = new CameraRotateUpDownCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, rotationXRadians);
        }

        private protected override void RotateVertically(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            RotateDown(-rotateSign * speed);
        }

        private protected override void PanHorizontallyCommandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void PanHorizontallyCommandEnd()
        {
            _ = new TrackingCameraXCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, positionXRadians);
        }

        private protected override void PanHorizontally(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            PanRight(panSign * speed);
        }

        private protected override void PanVerticallyCommandStart()
        {
            viewer.CheckReplaying();
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void PanVerticallyCommandEnd()
        {
            _ = new TrackingCameraYCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, positionYRadians);
        }

        private protected override void PanVertically(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            PanUp(panSign * speed);
        }

        private protected override void CarFirst()
        {
            _ = new FirstCarCommand(viewer.Log);
        }

        private protected override void CarLast()
        {
            _ = new LastCarCommand(viewer.Log);
        }

        private protected override void CarPrevious()
        {
            _ = new PreviousCarCommand(viewer.Log);
        }

        private protected override void CarNext()
        {
            _ = new NextCarCommand(viewer.Log);
        }

        private protected override void BrowseForwards()
        {
            _ = new ToggleBrowseForwardsCommand(viewer.Log);
        }

        private protected override void BrowseBackwards()
        {
            _ = new ToggleBrowseBackwardsCommand(viewer.Log);
        }

        private protected override void ZoomByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers)
        {
            ZoomByMouse(commandArgs, gameTime, modifiers);
        }

        private protected override void RotateByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers)
        {
            base.RotateByMouse(commandArgs, gameTime, modifiers);
        }

        public override void Update(in ElapsedTime elapsedTime)
        {
            var replayRemainingS = endTime - viewer.Simulator.ClockTime;
            if (replayRemainingS > 0)
            {
                var replayFraction = elapsedTime.ClockSeconds / replayRemainingS;
                // Panning
                if (positionXTarget != null)
                {
                    var replayRemainingX = positionXTarget - positionXRadians;
                    var replaySpeedX = (float)(replayRemainingX * replayFraction);
                    if (IsCloseEnough(positionXRadians, positionXTarget, replaySpeedX))
                    {
                        positionXTarget = null;
                    }
                    else
                    {
                        PanUp(replaySpeedX);
                    }
                }
                if (positionYTarget != null)
                {
                    var replayRemainingY = positionYTarget - positionYRadians;
                    var replaySpeedY = (float)(replayRemainingY * replayFraction);
                    if (IsCloseEnough(positionYRadians, positionYTarget, replaySpeedY))
                    {
                        positionYTarget = null;
                    }
                    else
                    {
                        PanRight(replaySpeedY);
                    }
                }
                // Zooming
                if (positionDistanceTarget != null)
                {
                    var replayRemainingZ = positionDistanceTarget - positionDistance;
                    var replaySpeedZ = (float)(replayRemainingZ * replayFraction);
                    if (IsCloseEnough(positionDistance, positionDistanceTarget, replaySpeedZ))
                    {
                        positionDistanceTarget = null;
                    }
                    else
                    {
                        ZoomIn(replaySpeedZ / positionDistance);
                    }
                }
            }

            // Rotation
            UpdateRotation(elapsedTime);

            // Update location of attachment
            attachedLocation.X = 0;
            attachedLocation.Y = 2;
            attachedLocation.Z = positionDistance;
            attachedLocation = Vector3.Transform(attachedLocation, Matrix.CreateRotationX(-positionXRadians));
            attachedLocation = Vector3.Transform(attachedLocation, Matrix.CreateRotationY(positionYRadians));

            // Update location of camera
            if (browseMode)
            {
                UpdateTrainBrowsing(elapsedTime);
                attachedLocation.Z += browseDistance * (attachedToFront ? 1 : -1);
                lookedAtPosition = new WorldPosition(browsedTraveller.Tile, Matrix.CreateFromYawPitchRoll(-browsedTraveller.RotY, 0, 0)).SetTranslation(browsedTraveller.X, browsedTraveller.Y, -browsedTraveller.Z);
            }
            else if (AttachedCar != null)
            {
                lookedAtPosition = AttachedCar.WorldPosition;
            }
            UpdateLocation(lookedAtPosition);
            UpdateListener();
        }

        protected void UpdateTrainBrowsing(in ElapsedTime elapsedTime)
        {
            var trainCars = GetCameraCars();
            if (browseBackwards)
            {
                var ZIncrM = -BrowseSpeedMpS * elapsedTime.ClockSeconds;
                zDistanceM += (float)ZIncrM;
                if (-zDistanceM >= AttachedCar.Train.Length - (trainCars.First().CarLengthM + trainCars.Last().CarLengthM) * 0.5f)
                {
                    ZIncrM = -AttachedCar.Train.Length + (trainCars.First().CarLengthM + trainCars.Last().CarLengthM) * 0.5f - (zDistanceM - ZIncrM);
                    zDistanceM = -AttachedCar.Train.Length + (trainCars.First().CarLengthM + trainCars.Last().CarLengthM) * 0.5f;
                    browseBackwards = false;
                }
                else if (zDistanceM < wagonOffsetLowerLimit)
                {
                    base.PreviousCar();
                    wagonOffsetUpperLimit = wagonOffsetLowerLimit;
                    wagonOffsetLowerLimit -= AttachedCar.CarLengthM;
                }
                browsedTraveller.Move(elapsedTime.ClockSeconds * AttachedCar.Train.SpeedMpS + ZIncrM);
            }
            else if (browseForwards)
            {
                var ZIncrM = BrowseSpeedMpS * elapsedTime.ClockSeconds;
                zDistanceM += (float)ZIncrM;
                if (zDistanceM >= 0)
                {
                    ZIncrM -= zDistanceM;
                    zDistanceM = 0;
                    browseForwards = false;
                }
                else if (zDistanceM > wagonOffsetUpperLimit)
                {
                    base.NextCar();
                    wagonOffsetLowerLimit = wagonOffsetUpperLimit;
                    wagonOffsetUpperLimit += AttachedCar.CarLengthM;
                }
                browsedTraveller.Move(elapsedTime.ClockSeconds * AttachedCar.Train.SpeedMpS + ZIncrM);
            }
            else
                browsedTraveller.Move(elapsedTime.ClockSeconds * AttachedCar.Train.SpeedMpS);
        }

        private protected void ComputeCarOffsets(TrackingCamera camera)
        {
            var trainCars = camera.GetCameraCars();
            camera.wagonOffsetUpperLimit = trainCars.First().CarLengthM * 0.5f;
            foreach (TrainCar trainCar in trainCars)
            {
                camera.wagonOffsetLowerLimit = camera.wagonOffsetUpperLimit - trainCar.CarLengthM;
                if (zDistanceM > wagonOffsetLowerLimit)
                    break;
                else
                    camera.wagonOffsetUpperLimit = camera.wagonOffsetLowerLimit;
            }
        }

        protected void PanUp(float speed)
        {
            positionXRadians += speed;
            positionXRadians = VerticalClamper.Clamp(positionXRadians);
            rotationXRadians += speed;
            rotationXRadians = VerticalClamper.Clamp(rotationXRadians);
        }

        protected void PanRight(float speed)
        {
            speed *= -1;//Tracking Cameras work opposite way, see also https://github.com/perpetualKid/ORTS-MG/issues/90
            positionYRadians += speed;
            rotationYRadians += speed;
        }

        protected override void ZoomIn(float speed)
        {
            speed *= -1;//Tracking Cameras work opposite way, see also https://github.com/perpetualKid/ORTS-MG/issues/90
            // Speed depends on distance, slows down when zooming in, speeds up zooming out.
            positionDistance += speed * positionDistance;
            positionDistance = MathHelper.Clamp(positionDistance, 1, 100);
        }

        /// <summary>
        /// Swaps front and rear tracking camera after reversal point, to avoid abrupt change of picture
        /// </summary>

        public void SwapCameras()
        {
            if (attachedToFront)
            {
                SwapParams(this, viewer.BackCamera);
                viewer.BackCamera.Activate();
            }
            else
            {
                SwapParams(this, viewer.FrontCamera);
                viewer.FrontCamera.Activate();
            }
        }


        /// <summary>
        /// Swaps parameters of Front and Back Camera
        /// </summary>
        private protected void SwapParams(TrackingCamera oldCamera, TrackingCamera newCamera)
        {
            (oldCamera.AttachedCar, newCamera.AttachedCar) = (newCamera.AttachedCar, oldCamera.AttachedCar);
            (oldCamera.positionDistance, newCamera.positionDistance) = (newCamera.positionDistance, oldCamera.positionDistance);
            (oldCamera.positionXRadians, newCamera.positionXRadians) = (newCamera.positionXRadians, oldCamera.positionXRadians);
            float swapFloat = newCamera.positionYRadians;
            newCamera.positionYRadians = oldCamera.positionYRadians + MathHelper.Pi * (attachedToFront ? 1 : -1);
            oldCamera.positionYRadians = swapFloat - MathHelper.Pi * (attachedToFront ? 1 : -1);
            swapFloat = newCamera.rotationXRadians;
            newCamera.rotationXRadians = oldCamera.rotationXRadians;
            oldCamera.rotationXRadians = swapFloat;
            swapFloat = newCamera.rotationYRadians;
            newCamera.rotationYRadians = oldCamera.rotationYRadians - MathHelper.Pi * (attachedToFront ? 1 : -1);
            oldCamera.rotationYRadians = swapFloat + MathHelper.Pi * (attachedToFront ? 1 : -1);

            // adjust and swap data for camera browsing

            newCamera.browseForwards = newCamera.browseBackwards = false;
            var trainCars = newCamera.GetCameraCars();
            newCamera.zDistanceM = -newCamera.AttachedCar.Train.Length + (trainCars.First().CarLengthM + trainCars.Last().CarLengthM) * 0.5f - oldCamera.zDistanceM;
            ComputeCarOffsets(newCamera);
            // Todo travellers
        }


        public override void NextCar()
        {
            browseBackwards = false;
            browseForwards = false;
            browseMode = false;
            var trainCars = GetCameraCars();
            var wasFirstCar = AttachedCar == trainCars.First();
            base.NextCar();
            if (!wasFirstCar)
            {
                wagonOffsetLowerLimit = wagonOffsetUpperLimit;
                wagonOffsetUpperLimit += AttachedCar.CarLengthM;
                zDistanceM = wagonOffsetLowerLimit + AttachedCar.CarLengthM * 0.5f;
            }
        }

        public override void PreviousCar()
        {
            browseBackwards = false;
            browseForwards = false;
            browseMode = false;
            var trainCars = GetCameraCars();
            var wasLastCar = AttachedCar == trainCars.Last();
            base.PreviousCar();
            if (!wasLastCar)
            {
                wagonOffsetUpperLimit = wagonOffsetLowerLimit;
                wagonOffsetLowerLimit -= AttachedCar.CarLengthM;
                zDistanceM = wagonOffsetLowerLimit + AttachedCar.CarLengthM * 0.5f;
            }
        }

        public override void FirstCar()
        {
            browseBackwards = false;
            browseForwards = false;
            browseMode = false;
            base.FirstCar();
            zDistanceM = 0;
            wagonOffsetUpperLimit = AttachedCar.CarLengthM * 0.5f;
            wagonOffsetLowerLimit = -AttachedCar.CarLengthM * 0.5f;
        }

        public override void LastCar()
        {
            browseBackwards = false;
            browseForwards = false;
            browseMode = false;
            base.LastCar();
            var trainCars = GetCameraCars();
            zDistanceM = -AttachedCar.Train.Length + (trainCars.First().CarLengthM + trainCars.Last().CarLengthM) * 0.5f;
            wagonOffsetLowerLimit = -AttachedCar.Train.Length + trainCars.First().CarLengthM * 0.5f;
            wagonOffsetUpperLimit = wagonOffsetLowerLimit + AttachedCar.CarLengthM;
        }

        public void ToggleBrowseBackwards()
        {
            browseBackwards = !browseBackwards;
            if (browseBackwards)
            {
                if (!browseMode)
                {
                    browsedTraveller = new Traveller(AttachedCar.Train.FrontTDBTraveller);
                    browsedTraveller.Move(-AttachedCar.CarLengthM * 0.5f + zDistanceM);
                    browseDistance = AttachedCar.CarLengthM * 0.5f;
                    browseMode = true;
                }
            }
            browseForwards = false;
        }

        public void ToggleBrowseForwards()
        {
            browseForwards = !browseForwards;
            if (browseForwards)
            {
                if (!browseMode)
                {
                    //                    LookedAtPosition = new WorldPosition(attachedCar.WorldPosition);
                    browsedTraveller = new Traveller(AttachedCar.Train.RearTDBTraveller);
                    var trainCars = GetCameraCars();
                    browsedTraveller.Move((AttachedCar.CarLengthM - trainCars.First().CarLengthM - trainCars.Last().CarLengthM) * 0.5f + AttachedCar.Train.Length + zDistanceM);
                    browseDistance = AttachedCar.CarLengthM * 0.5f;
                    browseMode = true;
                }
            }
            browseBackwards = false;
        }
    }

    public abstract class NonTrackingCamera : AttachedCamera
    {
        protected NonTrackingCamera(Viewer viewer)
            : base(viewer)
        {
        }

        private protected override void RotateHorizontallyCommandStart()
        {
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void RotateHorizontallyCommandEnd()
        {
            _ = new CameraRotateLeftRightCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, rotationYRadians);
        }

        private protected override void RotateHorizontally(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            RotateRight(rotateSign * speed);
        }

        private protected override void RotateVerticallyCommandStart()
        {
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void RotateVerticallyCommandEnd()
        {
            _ = new CameraRotateUpDownCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, rotationXRadians);
        }

        private protected override void RotateVertically(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            RotateDown(-rotateSign * speed);
        }

        private protected override void PanHorizontallyCommandStart()
        {
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void PanHorizontallyCommandEnd()
        {
            _ = new CameraRotateLeftRightCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, rotationYRadians);
        }

        private protected override void PanHorizontally(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            RotateRight(panSign * speed);
        }

        private protected override void PanVerticallyCommandStart()
        {
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void PanVerticallyCommandEnd()
        {
            _ = new CameraRotateUpDownCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, rotationXRadians);
        }

        private protected override void PanVertically(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            RotateDown(-panSign * speed);
        }

        private protected override void CarFirst()
        {
            _ = new FirstCarCommand(viewer.Log);
        }

        private protected override void CarLast()
        {
            _ = new LastCarCommand(viewer.Log);
        }

        private protected override void CarPrevious()
        {
            _ = new PreviousCarCommand(viewer.Log);
        }

        private protected override void CarNext()
        {
            _ = new NextCarCommand(viewer.Log);
        }

        private protected override void ZoomByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers)
        {
            ZoomByMouse(commandArgs, gameTime, modifiers);
        }

        private protected override void RotateByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers)
        {
            RotateByMouse(commandArgs, gameTime, modifiers);
        }
    }

    public class BrakemanCamera : NonTrackingCamera
    {
        private bool attachedToRear;

        public BrakemanCamera(Viewer viewer)
            : base(viewer)
        {
            NearPlane = 0.25f;
            Name = Viewer.Catalog.GetString("Brakeman");
        }

        private protected override List<TrainCar> GetCameraCars()
        {            
            return new List<TrainCar>() { base.GetCameraCars()[0], base.GetCameraCars()[^1] };
        }

        protected override void SetCameraCar(TrainCar car)
        {
            base.SetCameraCar(car);
            attachedLocation = new Vector3(1.8f, 2.0f, AttachedCar.CarLengthM / 2 - 0.3f);
            attachedToRear = car?.Train.Cars[0] != car;
        }

        protected override bool IsCameraFlipped()
        {
            return attachedToRear ^ AttachedCar.Flipped;
        }

        // attached car may be no more part of the list, therefore base methods would return errors
        public override void NextCar()
        {
            FirstCar();
        }

        public override void PreviousCar()
        {
            LastCar();
        }
        public override void LastCar()
        {
            base.LastCar();
            attachedToRear = true;
        }

    }

    public class InsideCamera3D : NonTrackingCamera
    {
        public InsideCamera3D(Viewer viewer)
            : base(viewer)
        {
            NearPlane = 0.1f;
        }

        private protected Vector3 viewPointLocation;
        private protected float viewPointRotationXRadians;
        private protected float viewPointRotationYRadians;
        private protected Vector3 startViewPointLocation;
        private protected float startViewPointRotationXRadians;
        private protected float startViewPointRotationYRadians;
        private protected string prevcar = "";
        private protected int actViewPoint;
        private protected int prevViewPoint = -1;
        private protected bool usingRearCab;
        private float x, y, z;

        /// <summary>
        /// A camera can use this method to handle any preparation when being activated.
        /// </summary>
        protected override void OnActivate(bool sameCamera)
        {
            var trainCars = GetCameraCars();
            List<TrainCar> trainCarList;
            int index;
            if (trainCars.Count == 0)
                return;//may not have passenger or 3d cab viewpoints
            if (sameCamera)
            {
                if (!trainCars.Contains(AttachedCar))
                {
                    AttachedCar = trainCars.First();
                }
                else if ((index = (trainCarList = trainCars.ToList()).IndexOf(AttachedCar)) < trainCarList.Count - 1)
                {
                    AttachedCar = trainCarList[index + 1];
                }
                else
                    AttachedCar = trainCars.First();
            }
            else
            {
                if (!trainCars.Contains(AttachedCar))
                    AttachedCar = trainCars.First();
            }
            SetCameraCar(AttachedCar);
        }

        private protected override void Zoom(int zoomSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            // Move camera
            z = zoomSign * GetSpeed(gameTime, commandArgs, viewer) * 5;
            MoveCameraXYZ(0, 0, z);
        }

        private protected override void RotateHorizontallyCommandStart()
        {
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void RotateHorizontallyCommandEnd()
        {
            _ = new CameraMoveXYZCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, x, 0, 0);
        }

        private protected override void RotateHorizontally(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            x = rotateSign * GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation * 2;
            MoveCameraXYZ(x, 0, 0);
        }

        private protected override void RotateVerticallyCommandStart()
        {
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void RotateVerticallyCommandEnd()
        {
            _ = new CameraMoveXYZCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, 0, y, 0);
        }

        private protected override void RotateVertically(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            y = rotateSign * GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation / 2;
            MoveCameraXYZ(0, y, 0);
        }

        private protected override void PanHorizontallyCommandStart()
        {
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void PanHorizontallyCommandEnd()
        {
            _ = new CameraRotateLeftRightCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, rotationYRadians);
        }

        private protected override void PanHorizontally(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            RotateRight(panSign * speed);
        }

        private protected override void PanVerticallyCommandStart()
        {
            commandStartTime = viewer.Simulator.ClockTime;
        }

        private protected override void PanVerticallyCommandEnd()
        {
            _ = new CameraRotateUpDownCommand(viewer.Log, commandStartTime, viewer.Simulator.ClockTime, rotationXRadians);
        }

        private protected override void PanVertically(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer) * SpeedAdjustmentForRotation;
            RotateDown(-panSign * speed);
        }

        private protected override void CarFirst()
        {
            _ = new FirstCarCommand(viewer.Log);
        }

        private protected override void CarLast()
        {
            _ = new LastCarCommand(viewer.Log);
        }

        private protected override void CarPrevious()
        {
            _ = new PreviousCarCommand(viewer.Log);
        }

        private protected override void CarNext()
        {
            _ = new NextCarCommand(viewer.Log);
        }

        private protected override void ZoomByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers)
        {
            ZoomByMouse(commandArgs, gameTime, modifiers, SpeedAdjustmentForRotation);
        }

        public void MoveCameraXYZ(float x, float y, float z, double endTime)
        {
            MoveCameraXYZ(x, y, z);
            base.endTime = endTime;
        }

        public void MoveCameraXYZ(float x, float y, float z)
        {
            if (usingRearCab)
            {
                x = -x;
                z = -z;
            }
            attachedLocation.X += x;
            attachedLocation.Y += y;
            attachedLocation.Z += z;
            viewPointLocation.X = attachedLocation.X;
            viewPointLocation.Y = attachedLocation.Y;
            viewPointLocation.Z = attachedLocation.Z;
            if (AttachedCar != null)
                UpdateLocation(AttachedCar.WorldPosition);
        }

        /// <summary>
        /// Remembers angle of camera to apply when user returns to this type of car.
        /// </summary>
        /// <param name="speed"></param>
        protected override void RotateRight(float speed)
        {
            base.RotateRight(speed);
            viewPointRotationYRadians = rotationYRadians;
        }

        /// <summary>
        /// Remembers angle of camera to apply when user returns to this type of car.
        /// </summary>
        /// <param name="speed"></param>
        protected override void RotateDown(float speed)
        {
            base.RotateDown(speed);
            viewPointRotationXRadians = rotationXRadians;
        }

        /// <summary>
        /// Remembers angle of camera to apply when user returns to this type of car.
        /// </summary>
        private protected override void RotateByMouseCommmandEnd()
        {
            base.RotateByMouseCommmandEnd();
            viewPointRotationXRadians = rotationXRadians;
            viewPointRotationYRadians = rotationYRadians;
        }

        public override async ValueTask<CameraSaveState> Snapshot()
        {
            CameraSaveState saveState = await base.Snapshot().ConfigureAwait(false);
            saveState.CurrentViewPoint = actViewPoint;
            saveState.PreviousViewPoint = prevViewPoint;
            saveState.CarId = prevcar;
            saveState.TrackingPosition = startViewPointLocation.ToNumerics();
            saveState.TrackingRotationStart = new System.Numerics.Vector3(startViewPointRotationXRadians, startViewPointRotationYRadians, 0);
            return saveState;
        }

        public override async ValueTask Restore([NotNull] CameraSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            actViewPoint = saveState.CurrentViewPoint;
            prevViewPoint = saveState.PreviousViewPoint;
            prevcar = saveState.CarId;
            startViewPointLocation = saveState.TrackingPosition;
            startViewPointRotationXRadians = saveState.TrackingRotationStart.X;
            startViewPointRotationYRadians = saveState.TrackingRotationStart.Y;
        }

        public override void Reset()
        {
            base.Reset();
            viewPointLocation = startViewPointLocation;
            attachedLocation = startViewPointLocation;
            viewPointRotationXRadians = startViewPointRotationXRadians;
            viewPointRotationYRadians = startViewPointRotationYRadians;
            rotationXRadians = startViewPointRotationXRadians;
            rotationYRadians = startViewPointRotationYRadians;
            xRadians = startViewPointRotationXRadians;
            yRadians = startViewPointRotationYRadians;
        }
    }

    public class PassengerCamera : InsideCamera3D
    {
        public override bool IsAvailable { get { return viewer.SelectedTrain?.Cars.Any(c => c.PassengerViewpoints != null) ?? false; } }

        public PassengerCamera(Viewer viewer)
            : base(viewer)
        {
            Style = CameraStyle.Passenger;
            Name = Viewer.Catalog.GetString("Passenger");
        }

        private protected override List<TrainCar> GetCameraCars()
        {
            return base.GetCameraCars().Where(c => c.PassengerViewpoints != null).ToList();
        }

        protected override void SetCameraCar(TrainCar car)
        {
            base.SetCameraCar(car);
            // Settings are held so that when switching back from another camera, view is not reset.
            // View is only reset on move to a different car and/or viewpoint or "Ctrl + 8".
            if (car != null && car.CarID != prevcar)
            {
                actViewPoint = 0;
                ResetViewPoint(car);
            }
            else if (actViewPoint != prevViewPoint)
            {
                ResetViewPoint(car);
            }
        }

        protected void ResetViewPoint(TrainCar car)
        {
            ArgumentNullException.ThrowIfNull(car);
            prevcar = car.CarID;
            prevViewPoint = actViewPoint;
            viewPointLocation = AttachedCar.PassengerViewpoints[actViewPoint].Location;
            viewPointRotationXRadians = AttachedCar.PassengerViewpoints[actViewPoint].RotationXRadians;
            viewPointRotationYRadians = AttachedCar.PassengerViewpoints[actViewPoint].RotationYRadians;
            rotationXRadians = viewPointRotationXRadians;
            rotationYRadians = viewPointRotationYRadians;
            attachedLocation = viewPointLocation;
            startViewPointLocation = viewPointLocation;
            startViewPointRotationXRadians = viewPointRotationXRadians;
            startViewPointRotationYRadians = viewPointRotationYRadians;
        }

        private protected override void ChangePassengerViewPoint()
        {
            _ = new CameraChangePassengerViewPointCommand(viewer.Log);
        }

        public void SwitchSideCameraCar(TrainCar car)
        {
            attachedLocation.X = -attachedLocation.X;
            rotationYRadians = -rotationYRadians;
        }

        public void ChangePassengerViewPoint(TrainCar car)
        {
            ArgumentNullException.ThrowIfNull(car, nameof(car));

            actViewPoint++;
            if (actViewPoint >= (car.PassengerViewpoints?.Count ?? 0))
                actViewPoint = 0;
            SetCameraCar(car);
        }
    }

    public class CabCamera3D : InsideCamera3D
    {
        private CabViewDiscreteRenderer selectedControl;
        private CabViewDiscreteRenderer pointedControl;

        public bool Enabled { get; set; }

        public override bool IsAvailable => viewer.SelectedTrain != null && viewer.SelectedTrain.IsActualPlayerTrain &&
                    viewer.PlayerLocomotive != null && viewer.PlayerLocomotive.CabViewpoints != null &&
                    (viewer.PlayerLocomotive.HasFront3DCab || viewer.PlayerLocomotive.HasRear3DCab);

        public CabCamera3D(Viewer viewer)
            : base(viewer)
        {
            Style = CameraStyle.Cab3D;
            Name = Viewer.Catalog.GetString("3D Cab");
        }

        private protected override List<TrainCar> GetCameraCars()
        {
            if (viewer.SelectedTrain != null && viewer.SelectedTrain.IsActualPlayerTrain &&
                viewer.PlayerLocomotive != null && viewer.PlayerLocomotive.CabViewpoints != null)
            {
                return new List<TrainCar>() { viewer.PlayerLocomotive };
            }
            else
                return base.GetCameraCars();
        }

        protected override void SetCameraCar(TrainCar car)
        {
            base.SetCameraCar(car);
            // Settings are held so that when switching back from another camera, view is not reset.
            // View is only reset on move to a different cab or "Ctrl + 8".
            if (AttachedCar.CabViewpoints != null && car != null)
            {
                if (car.CarID != prevcar || actViewPoint != prevViewPoint)
                {
                    prevcar = car.CarID;
                    prevViewPoint = actViewPoint;
                    viewPointLocation = AttachedCar.CabViewpoints[actViewPoint].Location;
                    viewPointRotationXRadians = AttachedCar.CabViewpoints[actViewPoint].RotationXRadians;
                    viewPointRotationYRadians = AttachedCar.CabViewpoints[actViewPoint].RotationYRadians;
                    rotationXRadians = viewPointRotationXRadians;
                    rotationYRadians = viewPointRotationYRadians;
                    attachedLocation = viewPointLocation;
                    startViewPointLocation = viewPointLocation;
                    startViewPointRotationXRadians = viewPointRotationXRadians;
                    startViewPointRotationYRadians = viewPointRotationYRadians;
                }
            }
        }

        public void ChangeCab(TrainCar newCar)
        {
            if (newCar is MSTSLocomotive mstsLocomotive)
            {
                if (usingRearCab != mstsLocomotive.UsingRearCab)
                    rotationYRadians += MathHelper.Pi;
                actViewPoint = mstsLocomotive.UsingRearCab ? 1 : 0;
                usingRearCab = mstsLocomotive.UsingRearCab;
                SetCameraCar(newCar);
            }
        }

        public override async ValueTask<CameraSaveState> Snapshot()
        {
            CameraSaveState saveState = await base.Snapshot().ConfigureAwait(false);
            saveState.UsingRearCab = usingRearCab;
            return saveState;
        }

        public override async ValueTask Restore([NotNull] CameraSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            usingRearCab = saveState.UsingRearCab;
        }

        public override bool IsUnderground
        {
            get
            {
                // Camera is underground if target (base) is underground or
                // track location is underground. The latter means we switch
                // to cab view instead of putting the camera above the tunnel.
                if (base.IsUnderground)
                    return true;
                var elevationAtCameraTarget = viewer.Tiles.GetElevation(AttachedCar.WorldPosition.WorldLocation);
                return AttachedCar.WorldPosition.Location.Y + TerrainAltitudeMargin < elevationAtCameraTarget || AttachedCar.TunnelLengthAheadFront > 0;
            }
        }

        private CabViewDiscreteRenderer FindNearestControl(Point position, MSTSLocomotiveViewer mstsLocomotiveViewer, float maxDelta)
        {
            CabViewDiscreteRenderer result = null;
            Vector3 nearsource = new Vector3(position.X, position.Y, 0f);
            Vector3 farsource = new Vector3(position.X, position.Y, 1f);
            Matrix world = Matrix.CreateTranslation(0, 0, 0);

            ref readonly Viewport viewport = ref viewer.RenderProcess.Viewport;
            Vector3 nearPoint = viewport.Unproject(nearsource, XnaProjection, XnaView, world);
            Vector3 farPoint = viewport.Unproject(farsource, XnaProjection, XnaView, world);

            Shapes.PoseableShape trainCarShape = mstsLocomotiveViewer.CabViewer3D.TrainCarShape;
            Dictionary<(ControlType, int), AnimatedPartMultiState> animatedParts = mstsLocomotiveViewer.CabViewer3D.AnimateParts;
            Dictionary<(ControlType, int), CabViewControlRenderer> controlMap = mstsLocomotiveViewer.CabRenderer3D.ControlMap;
            float bestDistance = maxDelta;  // squared click range
            foreach (KeyValuePair<(ControlType, int), AnimatedPartMultiState> animatedPart in animatedParts)
            {
                if (controlMap.TryGetValue(animatedPart.Value.Key, out CabViewControlRenderer cabRenderer) && cabRenderer is CabViewDiscreteRenderer screenRenderer)
                {
                    bool eligibleToCheck = true;
                    if (screenRenderer.control.Screens?.Count > 0 && !"all".Equals(screenRenderer.control.Screens[0], StringComparison.OrdinalIgnoreCase))
                    {
                        eligibleToCheck = false;
                        foreach (var screen in screenRenderer.control.Screens)
                        {
                            if (mstsLocomotiveViewer.CabRenderer3D.ActiveScreen[screenRenderer.control.Display] == screen)
                            {
                                eligibleToCheck = true;
                                break;
                            }
                        }
                    }
                    if (eligibleToCheck)
                    {
                        foreach (int i in animatedPart.Value.MatrixIndexes)
                        {
                            Matrix startingPoint = Matrix.Identity;
                            int j = i;
                            while (j >= 0 && j < trainCarShape.Hierarchy.Length && trainCarShape.Hierarchy[j] != -1)
                            {
                                startingPoint = MatrixExtension.Multiply(in startingPoint, in trainCarShape.XNAMatrices[j]);
                                j = trainCarShape.Hierarchy[j];
                            }
                            MatrixExtension.Multiply(in startingPoint, in trainCarShape.WorldPosition.XNAMatrix, out Matrix matrix);
                            WorldLocation matrixWorldLocation = new WorldLocation(trainCarShape.WorldPosition.WorldLocation.Tile,
                                matrix.Translation.X, matrix.Translation.Y, -matrix.Translation.Z);
                            Vector3 xnaCenter = XnaLocation(matrixWorldLocation);
                            float distance = xnaCenter.LineSegmentDistanceSquare(nearPoint, farPoint);

                            if (bestDistance > distance)
                            {
                                result = cabRenderer as CabViewDiscreteRenderer;
                                bestDistance = distance;
                            }
                        }
                    }
                }
            }
            return result;
        }

        private protected override void CabControlClickCommand(UserCommandArgs commandArgs, KeyModifiers modifiers)
        {
            PointerCommandArgs pointerCommandArgs = commandArgs as PointerCommandArgs;

            if (viewer.PlayerLocomotiveViewer is MSTSLocomotiveViewer mstsLocomotiveViewer && mstsLocomotiveViewer.Has3DCabRenderer)
            {
                selectedControl = pointedControl ?? FindNearestControl(pointerCommandArgs.Position, mstsLocomotiveViewer, 0.015f);
                selectedControl?.HandleUserInput(GenericButtonEventType.Pressed, pointerCommandArgs.Position, Vector2.Zero);
            }
        }

        private protected override void CabControlReleaseCommand(UserCommandArgs commandArgs, KeyModifiers modifiers)
        {
            selectedControl?.HandleUserInput(GenericButtonEventType.Released, (commandArgs as PointerCommandArgs).Position, Vector2.Zero);
            selectedControl = null;
        }

        private protected override void CabControlDraggedCommand(UserCommandArgs commandArgs, KeyModifiers modifiers)
        {
            selectedControl?.HandleUserInput(GenericButtonEventType.Down, (commandArgs as PointerCommandArgs).Position, (commandArgs as PointerMoveCommandArgs).Delta);
        }

        private protected override void CabControlPointerMovedCommand(UserCommandArgs commandArgs, KeyModifiers modifiers)
        {
            PointerCommandArgs pointerCommandArgs = commandArgs as PointerCommandArgs;

            if (viewer.PlayerLocomotiveViewer is MSTSLocomotiveViewer mstsLocomotiveViewer && mstsLocomotiveViewer.Has3DCabRenderer)
            {
                CabViewDiscreteRenderer control = pointedControl;
                pointedControl = FindNearestControl(pointerCommandArgs.Position, mstsLocomotiveViewer, 0.01f);

                if (pointedControl != null)
                {
                    if (pointedControl != control)
                        // say what control you have here
                        viewer.Simulator.Confirmer.Message(ConfirmLevel.None, string.IsNullOrEmpty(pointedControl.ControlLabel) ? pointedControl.GetControlName(pointerCommandArgs.Position) : pointedControl.ControlLabel);
                    viewer.RenderProcess.ActualCursor = Cursors.Hand;
                }
                else
                {
                    viewer.RenderProcess.ActualCursor = Cursors.Default;
                }
            }
        }
    }

    public class HeadOutCamera : NonTrackingCamera
    {
        private readonly bool forward;
        private int currentViewpointIndex;
        private bool prevCabWasRear;

        // Head-out camera is only possible on the player train.
        public override bool IsAvailable { get { return viewer.PlayerTrain?.Cars.Any(c => c.HeadOutViewpoints != null) ?? false; } }

        public HeadOutCamera(Viewer viewer, bool forwardheadDirection)
            : base(viewer)
        {
            NearPlane = 0.25f;
            Name = Viewer.Catalog.GetString("Head out");
            forward = forwardheadDirection;
            rotationYRadians = forward ? 0 : -MathHelper.Pi;
        }

        private protected override List<TrainCar> GetCameraCars()
        {
            // Head-out camera is only possible on the player train.
            return viewer.PlayerTrain.Cars.Where(c => c.HeadOutViewpoints != null).ToList();
        }

        protected override void SetCameraCar(TrainCar car)
        {
            base.SetCameraCar(car);
            if (AttachedCar.HeadOutViewpoints != null)
                attachedLocation = AttachedCar.HeadOutViewpoints[currentViewpointIndex].Location;

            if (!forward)
                attachedLocation.X *= -1;
        }

        public void ChangeCab(TrainCar newCar)
        {
            if (newCar is MSTSLocomotive mstsLocomotive)
            {
                if (prevCabWasRear != mstsLocomotive.UsingRearCab)
                    rotationYRadians += MathHelper.Pi;
                currentViewpointIndex = mstsLocomotive.UsingRearCab ? 1 : 0;
                prevCabWasRear = mstsLocomotive.UsingRearCab;
                SetCameraCar(newCar);
            }
        }
    }

    public class CabCamera : NonTrackingCamera
    {
        private CabViewDiscreteRenderer selectedControl;
        private CabViewDiscreteRenderer pointedControl;

        private float rotationRatio = 0.00081f;
        private float rotationRatioHorizontal = 0.00081f;

        public int SideLocation { get; protected set; }

        // Cab camera is only possible on the player train.
        public override bool IsAvailable { get { return viewer.PlayerLocomotive != null && (viewer.PlayerLocomotive.HasFrontCab || viewer.PlayerLocomotive.HasRearCab); } }

        public override bool IsUnderground
        {
            get
            {
                // Camera is underground if target (base) is underground or
                // track location is underground. The latter means we switch
                // to cab view instead of putting the camera above the tunnel.
                if (base.IsUnderground)
                    return true;
                var elevationAtCameraTarget = viewer.Tiles.GetElevation(AttachedCar.WorldPosition.WorldLocation);
                return AttachedCar.WorldPosition.Location.Y + TerrainAltitudeMargin < elevationAtCameraTarget || AttachedCar.TunnelLengthAheadFront > 0;
            }
        }

        public CabCamera(Viewer viewer)
            : base(viewer)
        {
            Name = Viewer.Catalog.GetString("Cab");
            Style = CameraStyle.Cab;
        }

        public override async ValueTask<CameraSaveState> Snapshot()
        {
            CameraSaveState saveState = await base.Snapshot().ConfigureAwait(false);
            saveState.SideLocation = SideLocation;
            return saveState;
        }

        public override async ValueTask Restore([NotNull] CameraSaveState saveState)
        {
            await base.Restore(saveState).ConfigureAwait(false);
            SideLocation = saveState.SideLocation;
        }

        public override void Reset()
        {
            FieldOfView = viewer.Settings.ViewingFOV;
            rotationXRadians = rotationYRadians = xRadians = yRadians = zRadians = 0;
            viewer.CabYOffsetPixels = (viewer.DisplaySize.Y - viewer.CabHeightPixels) / 2;
            viewer.CabXOffsetPixels = (viewer.CabWidthPixels - viewer.DisplaySize.X) / 2;
            if (AttachedCar != null)
            {
                Initialize();
            }
            ScreenChanged();
            OnActivate(true);
        }

        public void Initialize()
        {
            if (viewer.Settings.Letterbox2DCab)
            {
                float fovFactor = 1f - Math.Max((float)viewer.CabXLetterboxPixels / viewer.DisplaySize.X, (float)viewer.CabYLetterboxPixels / viewer.DisplaySize.Y);
                FieldOfView = MathHelper.ToDegrees((float)(2 * Math.Atan(fovFactor * Math.Tan(MathHelper.ToRadians(viewer.Settings.ViewingFOV / 2)))));
            }
            else if (viewer.Settings.Cab2DStretch == 0 && viewer.CabExceedsDisplayHorizontally <= 0)
            {
                // We must modify FOV to get correct lookout
                FieldOfView = MathHelper.ToDegrees((float)(2 * Math.Atan((float)viewer.DisplaySize.Y / viewer.DisplaySize.X / viewer.CabTextureInverseRatio * Math.Tan(MathHelper.ToRadians(viewer.Settings.ViewingFOV / 2)))));
                rotationRatio = (float)(0.962314f * 2 * Math.Tan(MathHelper.ToRadians(FieldOfView / 2)) / viewer.DisplaySize.Y);
            }
            else if (viewer.CabExceedsDisplayHorizontally > 0)
            {
                rotationRatioHorizontal = (float)(0.962314f * 2 * viewer.DisplaySize.X / viewer.DisplaySize.Y * Math.Tan(MathHelper.ToRadians(viewer.Settings.ViewingFOV / 2)) / viewer.DisplaySize.X);
            }
            InitialiseRotation(AttachedCar);
        }

        protected override void OnActivate(bool sameCamera)
        {
            // Cab camera is only possible on the player locomotive.
            SetCameraCar(GetCameraCars().First());
            base.OnActivate(sameCamera);
        }

        private protected override List<TrainCar> GetCameraCars()
        {
            // Cab camera is only possible on the player locomotive.
            return new List<TrainCar>() { viewer.PlayerLocomotive };
        }

        protected override void SetCameraCar(TrainCar car)
        {
            base.SetCameraCar(car);
            if (car is MSTSLocomotive locomotive)
            {
                var viewpoints = locomotive.CabViews[locomotive.UsingRearCab ? CabViewType.Rear : CabViewType.Front].ViewPointList;
                attachedLocation = viewpoints[SideLocation].Location;
            }
            InitialiseRotation(AttachedCar);
        }

        /// <summary>
        /// Switches to another cab view (e.g. side view).
        /// Applies the inclination of the previous external view due to PanUp() to the new external view. 
        /// </summary>
        private void ShiftView(int index)
        {
            var loco = AttachedCar as MSTSLocomotive;

            SideLocation += index;

            var count = loco.CabViews[loco.UsingRearCab ? CabViewType.Rear : CabViewType.Front].ViewPointList.Count;
            // Wrap around
            if (SideLocation < 0)
                SideLocation = count - 1;
            else if (SideLocation >= count)
                SideLocation = 0;

            SetCameraCar(AttachedCar);
        }

        /// <summary>
        /// Where cabview image doesn't fit the display exactly, this method mimics the player looking up
        /// and pans the image down to reveal details at the top of the cab.
        /// The external view also moves down by a similar amount.
        /// </summary>
        private void PanUp(bool up, float speed)
        {
            int max = 0;
            int min = viewer.DisplaySize.Y - viewer.CabHeightPixels - 2 * viewer.CabYLetterboxPixels; // -ve value
            int cushionPixels = 40;
            int slowFactor = 4;

            // Cushioned approach to limits of travel. Within 40 pixels, travel at 1/4 speed
            if (up && Math.Abs(viewer.CabYOffsetPixels - max) < cushionPixels)
                speed /= slowFactor;
            if (!up && Math.Abs(viewer.CabYOffsetPixels - min) < cushionPixels)
                speed /= slowFactor;
            viewer.CabYOffsetPixels += (up) ? (int)speed : -(int)speed;
            // Enforce limits to travel
            if (viewer.CabYOffsetPixels >= max)
            {
                viewer.CabYOffsetPixels = max;
                return;
            }
            if (viewer.CabYOffsetPixels <= min)
            {
                viewer.CabYOffsetPixels = min;
                return;
            }
            // Adjust inclination (up/down angle) of external view to match.
            var viewSpeed = (int)speed * rotationRatio; // factor found by trial and error.
            rotationXRadians -= (up) ? viewSpeed : -viewSpeed;
        }

        /// <summary>
        /// Where cabview image doesn't fit the display exactly (cabview image "larger" than display, this method mimics the player looking left and right
        /// and pans the image left/right to reveal details at the sides of the cab.
        /// The external view also moves sidewards by a similar amount.
        /// </summary>
        private void ScrollRight(bool right, float speed)
        {
            int min = 0;
            int max = viewer.CabWidthPixels - viewer.DisplaySize.X - 2 * viewer.CabXLetterboxPixels; // -ve value
            int cushionPixels = 40;
            int slowFactor = 4;

            // Cushioned approach to limits of travel. Within 40 pixels, travel at 1/4 speed
            if (right && Math.Abs(viewer.CabXOffsetPixels - max) < cushionPixels)
                speed /= slowFactor;
            if (!right && Math.Abs(viewer.CabXOffsetPixels - min) < cushionPixels)
                speed /= slowFactor;
            viewer.CabXOffsetPixels += (right) ? (int)speed : -(int)speed;
            // Enforce limits to travel
            if (viewer.CabXOffsetPixels >= max)
            {
                viewer.CabXOffsetPixels = max;
                return;
            }
            if (viewer.CabXOffsetPixels <= min)
            {
                viewer.CabXOffsetPixels = min;
                return;
            }
            // Adjust direction (right/left angle) of external view to match.
            var viewSpeed = (int)speed * rotationRatioHorizontal; // factor found by trial and error.
            rotationYRadians += (right) ? viewSpeed : -viewSpeed;
        }

        /// <summary>
        /// Sets direction for view out of cab front window. Also called when toggling between full screen and windowed.
        /// </summary>
        /// <param name="attachedCar"></param>
        public void InitialiseRotation(TrainCar attachedCar)
        {
            if (attachedCar == null)
                return;

            var loco = attachedCar as MSTSLocomotive;
            var viewpoints = loco.CabViews[loco.UsingRearCab ? CabViewType.Rear : CabViewType.Front].ViewPointList;

            rotationXRadians = MathHelper.ToRadians(viewpoints[SideLocation].StartDirection.X) - rotationRatio * (viewer.CabYOffsetPixels + viewer.CabExceedsDisplay / 2);
            rotationYRadians = MathHelper.ToRadians(viewpoints[SideLocation].StartDirection.Y) - rotationRatioHorizontal * (-viewer.CabXOffsetPixels + viewer.CabExceedsDisplayHorizontally / 2);
            ;
        }

        private protected override void ToggleLetterboxCab()
        {
            viewer.Settings.Letterbox2DCab = !viewer.Settings.Letterbox2DCab;
            viewer.AdjustCabHeight(viewer.DisplaySize.X, viewer.DisplaySize.Y);
            if (AttachedCar != null)
                Initialize();
        }

        private protected override void Scroll(bool right, GameTime gameTime)
        {
            ScrollRight(right, (float)(500 * gameTime.ElapsedGameTime.TotalSeconds));
        }

        private protected override void SwitchCabView(int direction)
        {
            ShiftView(-direction);
        }

        private protected override void PanVertically(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = (float)(500 * gameTime.ElapsedGameTime.TotalSeconds); // Independent of framerate
            PanUp(panSign > 0, speed);
        }

        private protected override void RotateHorizontally(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
        }

        private protected override void RotateVertically(int rotateSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
        }

        private protected override void PanHorizontally(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
        }

        private protected override void RotateByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers)
        {
        }

        private protected override void CabControlClickCommand(UserCommandArgs commandArgs, KeyModifiers modifiers)
        {
            PointerCommandArgs pointerCommandArgs = commandArgs as PointerCommandArgs;

            if (viewer.PlayerLocomotiveViewer is MSTSLocomotiveViewer mstsLocomotiveViewer && mstsLocomotiveViewer.HasCabRenderer)
            {
                selectedControl = pointedControl ?? mstsLocomotiveViewer.CabRenderer.ControlMap.Values.OfType<CabViewDiscreteRenderer>().Where(c => c.control.CabViewpoint == SideLocation && c.IsMouseWithin(pointerCommandArgs.Position)).FirstOrDefault();
                if (selectedControl?.control.Screens?.Count > 0 && !"all".Equals(selectedControl.control.Screens[0], StringComparison.OrdinalIgnoreCase))
                {
                    if (!(selectedControl.control.Screens.Where(s => s == mstsLocomotiveViewer.CabRenderer.ActiveScreen[selectedControl.control.Display])).Any())
                        selectedControl = null;
                }
                selectedControl?.HandleUserInput(GenericButtonEventType.Pressed, pointerCommandArgs.Position, Vector2.Zero);
            }
        }

        private protected override void CabControlReleaseCommand(UserCommandArgs commandArgs, KeyModifiers modifiers)
        {
            selectedControl?.HandleUserInput(GenericButtonEventType.Released, (commandArgs as PointerCommandArgs).Position, Vector2.Zero);
            selectedControl = null;
        }

        private protected override void CabControlDraggedCommand(UserCommandArgs commandArgs, KeyModifiers modifiers)
        {
            selectedControl?.HandleUserInput(GenericButtonEventType.Down, (commandArgs as PointerCommandArgs).Position, (commandArgs as PointerMoveCommandArgs).Delta);
        }

        private protected override void CabControlPointerMovedCommand(UserCommandArgs commandArgs, KeyModifiers modifiers)
        {
            PointerCommandArgs pointerCommandArgs = commandArgs as PointerCommandArgs;

            if (viewer.PlayerLocomotiveViewer is MSTSLocomotiveViewer mstsLocomotiveViewer && mstsLocomotiveViewer.HasCabRenderer)
            {
                CabViewDiscreteRenderer control = pointedControl;
                pointedControl = mstsLocomotiveViewer.CabRenderer.ControlMap.Values.OfType<CabViewDiscreteRenderer>().Where(c => c.IsMouseWithin(pointerCommandArgs.Position)).FirstOrDefault();
                if (pointedControl != null)
                {
                    if (pointedControl != control)
                        // say what control you have here
                        viewer.Simulator.Confirmer.Message(ConfirmLevel.None, string.IsNullOrEmpty(pointedControl.ControlLabel) ? pointedControl.GetControlName(pointerCommandArgs.Position) : pointedControl.ControlLabel);
                    viewer.RenderProcess.ActualCursor = Cursors.Hand;
                }
                else
                {
                    viewer.RenderProcess.ActualCursor = Cursors.Default;
                }
            }
        }
    }

    public class TracksideCamera : LookAtCamera
    {
        protected const int MaximumDistance = 100;
        protected const float SidewaysScale = MaximumDistance / 10;
        // Heights above the terrain for the camera.
        protected const float CameraAltitude = 2;
        // Height above the coordinate center of target.
        protected const float TargetAltitude = TerrainAltitudeMargin;

        private protected TrainCar lastCheckCar;
        private protected WorldLocation trackCameraLocation;
        private protected float cameraAltitudeOffset;

        public override bool IsUnderground
        {
            get
            {
                // Camera is underground if target (base) is underground or
                // track location is underground. The latter means we switch
                // to cab view instead of putting the camera above the tunnel.
                if (base.IsUnderground)
                    return true;
                if (trackCameraLocation == WorldLocation.None)
                    return false;
                var elevationAtCameraTarget = viewer.Tiles.GetElevation(trackCameraLocation);
                return trackCameraLocation.Location.Y + TerrainAltitudeMargin < elevationAtCameraTarget;
            }
        }

        public TracksideCamera(Viewer viewer)
            : base(viewer)
        {
            Name = Viewer.Catalog.GetString("Trackside");
        }

        public override void Reset()
        {
            base.Reset();
            cameraLocation = cameraLocation.ChangeElevation(-cameraAltitudeOffset);
            cameraAltitudeOffset = 0;
        }

        protected override void OnActivate(bool sameCamera)
        {
            if (sameCamera)
            {
                cameraLocation = new WorldLocation(0, 0, cameraLocation.Location);
            }
            if (AttachedCar == null || AttachedCar.Train != viewer.SelectedTrain)
            {
                if (viewer.SelectedTrain.MUDirection != MidpointDirection.Reverse)
                    AttachedCar = viewer.SelectedTrain.Cars.First();
                else
                    AttachedCar = viewer.SelectedTrain.Cars.Last();
            }
            base.OnActivate(sameCamera);
        }

        private protected override void Zoom(int zoomSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            ZoomIn(zoomSign * GetSpeed(gameTime, commandArgs, viewer) * 2);
        }

        private protected override void PanHorizontally(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = GetSpeed(gameTime, commandArgs, viewer);
            PanRight(panSign * speed);
        }

        private protected override void PanVertically(int panSign, UserCommandArgs commandArgs, GameTime gameTime)
        {
            float speed = panSign * GetSpeed(gameTime, commandArgs, viewer);
            rotationYRadians = -XnaView.MatrixToYAngle();

            cameraAltitudeOffset += speed;
            cameraLocation = cameraLocation.ChangeElevation(speed);

            if (panSign < 0 && cameraAltitudeOffset < 0)
            {
                cameraLocation = cameraLocation.ChangeElevation(-cameraAltitudeOffset);
                cameraAltitudeOffset = 0;
            }
        }

        private protected override void CarFirst()
        {
            AttachedCar = viewer.SelectedTrain.Cars.First();
        }

        private protected override void CarLast()
        {
            AttachedCar = viewer.SelectedTrain.Cars.Last();
        }

        private protected override void CarPrevious()
        {
            List<TrainCar> trainCars = viewer.SelectedTrain.Cars;
            AttachedCar = AttachedCar == trainCars.Last() ? AttachedCar : trainCars[trainCars.IndexOf(AttachedCar) + 1];
        }

        private protected override void CarNext()
        {
            List<TrainCar> trainCars = viewer.SelectedTrain.Cars;
            AttachedCar = AttachedCar == trainCars.First() ? AttachedCar : trainCars[trainCars.IndexOf(AttachedCar) - 1];
        }

        private protected override void ZoomByMouseCommmand(UserCommandArgs commandArgs, GameTime gameTime, KeyModifiers modifiers)
        {
            ZoomByMouse(commandArgs, gameTime, modifiers);
        }

        public override void Update(in ElapsedTime elapsedTime)
        {
            var train = PrepUpdate(out bool trainForwards);

            // Train is close enough if the last car we used is part of the same train and still close enough.
            var trainClose = (lastCheckCar?.Train == train) && (WorldLocation.GetDistance2D(lastCheckCar.WorldPosition.WorldLocation, cameraLocation).Length() < MaximumDistance);

            // Otherwise, let's check out every car and remember which is the first one close enough for next time.
            if (!trainClose)
            {
                foreach (var car in train.Cars)
                {
                    if (WorldLocation.GetDistance2D(car.WorldPosition.WorldLocation, cameraLocation).Length() < MaximumDistance)
                    {
                        lastCheckCar = car;
                        trainClose = true;
                        break;
                    }
                }
            }

            // Switch to new position.
            if (!trainClose || (trackCameraLocation == WorldLocation.None))
            {
                var tdb = trainForwards ? new Traveller(train.FrontTDBTraveller) : new Traveller(train.RearTDBTraveller, true);
                var newLocation = GoToNewLocation(tdb, train, trainForwards).Normalize();

                var newLocationElevation = viewer.Tiles.GetElevation(newLocation);

                cameraLocation = newLocation.SetElevation(Math.Max(tdb.Y, newLocationElevation) + CameraAltitude + cameraAltitudeOffset);
            }

            targetLocation = targetLocation.ChangeElevation(TargetAltitude);

            UpdateListener();

        }

        protected Train PrepUpdate(out bool trainForwards)
        {
            var train = AttachedCar.Train;

            // TODO: What is this code trying to do?
            //if (train != Viewer.PlayerTrain && train.LeadLocomotive == null) train.ChangeToNextCab();
            trainForwards = true;
            if (train.LeadLocomotive != null)
                //TODO: next code line has been modified to flip trainset physics in order to get viewing direction coincident with loco direction when using rear cab.
                // To achieve the same result with other means, without flipping trainset physics, maybe the line should be changed
                trainForwards = (train.LeadLocomotive.SpeedMpS >= 0) ^ train.LeadLocomotive.Flipped ^ train.LeadLocomotive.UsingRearCab;
            else if (viewer.PlayerLocomotive != null && train.IsActualPlayerTrain)
                trainForwards = (viewer.PlayerLocomotive.SpeedMpS >= 0) ^ viewer.PlayerLocomotive.Flipped ^ viewer.PlayerLocomotive.UsingRearCab;

            targetLocation = AttachedCar.WorldPosition.WorldLocation;

            return train;
        }

        private protected WorldLocation GoToNewLocation(Traveller tdb, Train train, bool trainForwards)
        {
            tdb.Move(MaximumDistance * 0.75f);
            trackCameraLocation = tdb.WorldLocation;
            var directionForward = WorldLocation.GetDistance((trainForwards ? train.FirstCar : train.LastCar).WorldPosition.WorldLocation, trackCameraLocation);
            if (StaticRandom.Next(2) == 0)
            {
                // Use swapped -X and Z to move to the left of the track.
                return new WorldLocation(trackCameraLocation.Tile,
                    trackCameraLocation.Location.X - (directionForward.Z / SidewaysScale), trackCameraLocation.Location.Y, trackCameraLocation.Location.Z + (directionForward.X / SidewaysScale));
            }
            else
            {
                // Use swapped X and -Z to move to the right of the track.
                return new WorldLocation(trackCameraLocation.Tile,
                    trackCameraLocation.Location.X + (directionForward.Z / SidewaysScale), trackCameraLocation.Location.Y, trackCameraLocation.Location.Z - (directionForward.X / SidewaysScale));
            }
        }

        protected virtual void PanRight(float speed)
        {
            Vector3 movement = new Vector3(speed, 0, 0);
            xRadians += movement.X;
            MoveCamera(movement);
        }

        protected override void ZoomIn(float speed)
        {
            Vector3 movement = new Vector3(0, 0, speed);
            zRadians += movement.Z;
            MoveCamera(movement);
        }

    }

    public class SpecialTracksideCamera : TracksideCamera
    {
        private const int MaximumSpecialPointDistance = 300;
        private const float PlatformOffsetM = 3.3f;
        private const float CheckIntervalM = 50f; // every 50 meters it is checked wheter there is a near special point
        private const float MaxDistFromRoadCarM = 200.0f; // maximum distance of train traveller to spawned roadcar

        private bool specialPointFound;
        private float distanceRunM; // distance run since last check interval
        private bool firstUpdateLoop = true; // first update loop

        private RoadCar rearRoadCar;
        private bool roadCarFound;

        private readonly float superElevationGaugeOverTwo;

        public SpecialTracksideCamera([NotNull] Viewer viewer)
            : base(viewer)
        {
            superElevationGaugeOverTwo = viewer.Settings.SuperElevationGauge / 1000f / 2;
        }

        protected override void OnActivate(bool sameCamera)
        {
            distanceRunM = 0;
            base.OnActivate(sameCamera);
            firstUpdateLoop = Math.Abs(AttachedCar.Train.SpeedMpS) <= 0.2f || sameCamera;
            if (sameCamera)
            {
                specialPointFound = false;
                trackCameraLocation = WorldLocation.None;
                roadCarFound = false;
                rearRoadCar = null;
            }
        }

        public override void Update(in ElapsedTime elapsedTime)
        {
            var train = PrepUpdate(out bool trainForwards);

            if (roadCarFound)
            {
                // camera location is always behind the near road car, at a distance which increases at increased speed
                if (rearRoadCar != null && rearRoadCar.Travelled < rearRoadCar.Spawner.Length - 10f)
                {
                    var traveller = new Traveller(rearRoadCar.FrontTraveller);
                    traveller.Move(-2.5f - 0.15f * rearRoadCar.Length - rearRoadCar.Speed * 0.5f);
                    trackCameraLocation = traveller.WorldLocation;
                    cameraLocation = traveller.WorldLocation.ChangeElevation(+1.8f);
                }
                else
                    rearRoadCar = null;
            }

            bool trainClose = false;
            // Train is close enough if the last car we used is part of the same train and still close enough.
            if ((lastCheckCar != null) && (lastCheckCar.Train == train))
            {
                float distance = WorldLocation.GetDistance2D(lastCheckCar.WorldPosition.WorldLocation, cameraLocation).Length();
                trainClose = distance < (specialPointFound ? MaximumSpecialPointDistance * 0.8f : MaximumDistance);
                if (!trainClose && specialPointFound && rearRoadCar != null)
                    trainClose = distance < MaximumSpecialPointDistance * 1.5f;
            }

            // Otherwise, let's check out every car and remember which is the first one close enough for next time.
            if (!trainClose)
            {
                // if camera is not close to LastCheckCar, verify if it is still close to another car of the train
                foreach (var car in train.Cars)
                {
                    if (lastCheckCar != null && car == lastCheckCar &&
                        WorldLocation.GetDistance2D(car.WorldPosition.WorldLocation, cameraLocation).Length() < (specialPointFound ? MaximumSpecialPointDistance * 0.8f : MaximumDistance))
                    {
                        trainClose = true;
                        break;
                    }
                    else if (WorldLocation.GetDistance2D(car.WorldPosition.WorldLocation, cameraLocation).Length() <
                        (specialPointFound && rearRoadCar != null && train.SpeedMpS > rearRoadCar.Speed + 10 ? MaximumSpecialPointDistance * 1.5f : MaximumDistance))
                    {
                        lastCheckCar = car;
                        trainClose = true;
                        break;
                    }
                }
                if (!trainClose)
                    lastCheckCar = null;
            }
            if (roadCarFound && rearRoadCar == null)
            {
                roadCarFound = false;
                specialPointFound = false;
                trainClose = false;
            }
            var trySpecial = false;
            distanceRunM += (float)elapsedTime.ClockSeconds * train.SpeedMpS;
            // when camera not at a special point, try every CheckIntervalM meters if there is a new special point nearby
            if (Math.Abs(distanceRunM) >= CheckIntervalM)
            {
                distanceRunM = 0;
                if (!specialPointFound && trainClose)
                    trySpecial = true;
            }
            // Switch to new position.
            if (!trainClose || (trackCameraLocation == WorldLocation.None) || trySpecial)
            {
                specialPointFound = false;
                bool platformFound = false;
                rearRoadCar = null;
                roadCarFound = false;
                Traveller tdb;
                // At first update loop camera location may be also behind train front (e.g. platform at start of activity)
                if (firstUpdateLoop)
                    tdb = trainForwards ? new Traveller(train.RearTDBTraveller) : new Traveller(train.FrontTDBTraveller, true);
                else
                    tdb = trainForwards ? new Traveller(train.FrontTDBTraveller) : new Traveller(train.RearTDBTraveller, true);

                int tcSectionIndex;
                int routeIndex;
                TrackCircuitPartialPathRoute thisRoute = null;
                // search for near platform in fast way, using TCSection data
                if (trainForwards && train.ValidRoutes[Direction.Forward] != null)
                {
                    thisRoute = train.ValidRoutes[Direction.Forward];
                }
                else if (!trainForwards && train.ValidRoutes[Direction.Backward] != null)
                {
                    thisRoute = train.ValidRoutes[Direction.Backward];
                }

                // Search for platform
                if (thisRoute != null)
                {
                    if (firstUpdateLoop)
                    {
                        tcSectionIndex = trainForwards ? train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex : train.PresentPosition[Direction.Forward].TrackCircuitSectionIndex;
                        routeIndex = trainForwards ? train.PresentPosition[Direction.Backward].RouteListIndex : train.PresentPosition[Direction.Forward].RouteListIndex;
                    }
                    else
                    {
                        tcSectionIndex = trainForwards ? train.PresentPosition[Direction.Forward].TrackCircuitSectionIndex : train.PresentPosition[Direction.Backward].TrackCircuitSectionIndex;
                        routeIndex = trainForwards ? train.PresentPosition[Direction.Forward].RouteListIndex : train.PresentPosition[Direction.Backward].RouteListIndex;
                    }
                    if (routeIndex != -1)
                    {
                        float distanceToViewingPoint = 0;
                        TrackCircuitSection TCSection = TrackCircuitSection.TrackCircuitList[tcSectionIndex];
                        float distanceToAdd = TCSection.Length;
                        float incrDistance;
                        if (firstUpdateLoop)
                            incrDistance = trainForwards ? -train.PresentPosition[Direction.Backward].Offset : -TCSection.Length + train.PresentPosition[Direction.Forward].Offset;
                        else
                            incrDistance = trainForwards ? -train.PresentPosition[Direction.Forward].Offset : -TCSection.Length + train.PresentPosition[Direction.Backward].Offset;
                        // scanning route in direction of train, searching for a platform
                        while (incrDistance < MaximumSpecialPointDistance * 0.7f)
                        {
                            foreach (int platformIndex in TCSection.PlatformIndices)
                            {
                                PlatformDetails thisPlatform = Simulator.Instance.SignalEnvironment.PlatformDetailsList[platformIndex];
                                if (thisPlatform.TrackCircuitOffset[SignalLocation.NearEnd, thisRoute[routeIndex].Direction] + incrDistance < MaximumSpecialPointDistance * 0.7f
                                    && (thisPlatform.TrackCircuitOffset[SignalLocation.NearEnd, thisRoute[routeIndex].Direction] + incrDistance > 0 || firstUpdateLoop))
                                {
                                    // platform found, compute distance to viewing point
                                    distanceToViewingPoint = Math.Min(MaximumSpecialPointDistance * 0.7f,
                                        incrDistance + thisPlatform.TrackCircuitOffset[SignalLocation.NearEnd, thisRoute[routeIndex].Direction] + thisPlatform.Length * 0.7f);
                                    if (firstUpdateLoop && Math.Abs(train.SpeedMpS) <= 0.2f)
                                        distanceToViewingPoint = Math.Min(distanceToViewingPoint, train.Length * 0.95f);
                                    tdb.Move(distanceToViewingPoint);
                                    // shortTrav is used to state directions, to correctly identify in which direction (left or right) to move
                                    //the camera from center of track to the platform at its side
                                    Traveller shortTrav;
                                    if (!(RuntimeData.Instance.TrackDB.TrackItems[thisPlatform.PlatformFrontUiD] is PlatformItem platformItem))
                                        continue;
                                    shortTrav = new Traveller(platformItem.Location, Direction.Forward);
                                    var distanceToViewingPoint1 = shortTrav.DistanceTo(tdb.WorldLocation, thisPlatform.Length);
                                    if (distanceToViewingPoint1 == -1) //try other direction
                                    {
                                        shortTrav.ReverseDirection();
                                        distanceToViewingPoint1 = shortTrav.DistanceTo(tdb.WorldLocation, thisPlatform.Length);
                                        if (distanceToViewingPoint1 == -1)
                                            continue;
                                    }
                                    platformFound = true;
                                    specialPointFound = true;
                                    trainClose = false;
                                    lastCheckCar = firstUpdateLoop ^ trainForwards ? train.Cars.First() : train.Cars.Last();
                                    shortTrav.Move(distanceToViewingPoint1);
                                    // moving location to platform at side of track
                                    float deltaX = (PlatformOffsetM + superElevationGaugeOverTwo) * (float)Math.Cos(shortTrav.RotY) *
                                        (((thisPlatform.PlatformSide & PlatformDetails.PlatformSides.Right) == PlatformDetails.PlatformSides.Right) ? 1 : -1);
                                    float deltaZ = -(PlatformOffsetM + superElevationGaugeOverTwo) * (float)Math.Sin(shortTrav.RotY) *
                                        (((thisPlatform.PlatformSide & PlatformDetails.PlatformSides.Right) == PlatformDetails.PlatformSides.Right) ? 1 : -1);
                                    trackCameraLocation = new WorldLocation(tdb.WorldLocation.Tile,
                                        tdb.WorldLocation.Location.X + deltaX, tdb.WorldLocation.Location.Y, tdb.WorldLocation.Location.Z + deltaZ);
                                    break;
                                }
                            }
                            if (platformFound)
                                break;
                            if (routeIndex < thisRoute.Count - 1)
                            {
                                incrDistance += distanceToAdd;
                                routeIndex++;
                                TCSection = thisRoute[routeIndex].TrackCircuitSection;
                                distanceToAdd = TCSection.Length;
                            }
                            else
                                break;
                        }
                    }
                }

                if (!specialPointFound)
                {

                    // Search for near visible spawned car
                    var minDistanceM = 10000.0f;
                    rearRoadCar = null;
                    foreach (RoadCar visibleCar in viewer.World.RoadCars.VisibleCars)
                    {
                        // check for direction
                        if (Math.Abs(visibleCar.FrontTraveller.RotY - train.FrontTDBTraveller.RotY) < 0.5f && visibleCar.Travelled < visibleCar.Spawner.Length - 30)
                        {
                            TrainCar testTrainCar = null;
                            if (visibleCar.Speed < Math.Abs(train.SpeedMpS) ^ trainForwards)
                            {
                                // we want to select an intermediate car so that the car will have the time to reach the head of the train
                                // before the end of its car spawner
                                float maxTrainLengthRecoveredM = (visibleCar.Spawner.Length - 10 - visibleCar.Travelled) / visibleCar.Speed *
                                    (visibleCar.Speed - Math.Abs(train.SpeedMpS));
                                var carNumber = (int)(Math.Max(maxTrainLengthRecoveredM - 40, maxTrainLengthRecoveredM / 2) / 30);
                                testTrainCar = trainForwards ? train.Cars[Math.Min(carNumber, train.Cars.Count - 1)] :
                                    train.Cars[Math.Max(0, train.Cars.Count - 1 - carNumber)];
                            }
                            else
                            {
                                // select first car in direction of movement
                                testTrainCar = trainForwards ? train.FirstCar : train.LastCar;
                            }
                            if (Math.Abs(visibleCar.FrontTraveller.WorldLocation.Location.Y - testTrainCar.WorldPosition.WorldLocation.Location.Y) < 30.0f)
                            {
                                var distanceTo = WorldLocation.GetDistance2D(visibleCar.FrontTraveller.WorldLocation, testTrainCar.WorldPosition.WorldLocation).Length();
                                if (distanceTo < MaxDistFromRoadCarM)
                                {
                                    minDistanceM = distanceTo;
                                    rearRoadCar = visibleCar;
                                    lastCheckCar = testTrainCar;
                                    break;
                                }
                            }
                        }
                    }
                    if (rearRoadCar != null)
                    // readcar found
                    {
                        specialPointFound = true;
                        roadCarFound = true;
                        // CarriesCamera needed to increase distance of following car
                        rearRoadCar.CarriesCamera = true;
                        var traveller = new Traveller(rearRoadCar.FrontTraveller);
                        traveller.Move(-2.5f - 0.15f * rearRoadCar.Length);
                        trackCameraLocation = traveller.WorldLocation;
                    }
                }

                if (!specialPointFound)
                {
                    // try to find near level crossing then
                    Simulation.World.LevelCrossingItem newLevelCrossingItem = Simulation.World.LevelCrossingItem.None;
                    float FrontDist = -1;
                    newLevelCrossingItem = viewer.Simulator.LevelCrossings.SearchNearLevelCrossing(train, MaximumSpecialPointDistance * 0.7f, trainForwards, out FrontDist);
                    if (newLevelCrossingItem != Simulation.World.LevelCrossingItem.None)
                    {
                        specialPointFound = true;
                        trainClose = false;
                        lastCheckCar = trainForwards ? train.Cars.First() : train.Cars.Last();
                        trackCameraLocation = newLevelCrossingItem.Location;
                        Traveller roadTraveller;
                        // decide randomly at which side of the level crossing the camera will be located
                        roadTraveller = new Traveller(RuntimeData.Instance.RoadTrackDB.TrackNodes[newLevelCrossingItem.TrackIndex] as TrackVectorNode,
                            trackCameraLocation, StaticRandom.Next(2) == 0 ? Direction.Forward : Direction.Backward, true);
                        roadTraveller.Move(12.5f);
                        tdb.Move(FrontDist);
                        trackCameraLocation = roadTraveller.WorldLocation;
                    }
                }

                if (!specialPointFound && !trainClose)
                {
                    tdb = trainForwards ? new Traveller(train.FrontTDBTraveller) : new Traveller(train.RearTDBTraveller, true); // return to standard
                    trackCameraLocation = GoToNewLocation(tdb, train, trainForwards);
                }

                if (trackCameraLocation != WorldLocation.None && !trainClose)
                {
                    trackCameraLocation = trackCameraLocation.Normalize();
                    cameraLocation = trackCameraLocation;
                    if (!roadCarFound)
                    {
                        trackCameraLocation = trackCameraLocation.SetElevation(viewer.Tiles.GetElevation(trackCameraLocation));
                        cameraLocation = trackCameraLocation.SetElevation(Math.Max(tdb.Y, trackCameraLocation.Location.Y) + CameraAltitude + cameraAltitudeOffset + (platformFound ? 0.35f : 0.0f));
                    }
                    else
                    {
                        trackCameraLocation = cameraLocation;
                        cameraLocation = cameraLocation.ChangeElevation(1.8f);
                    }
                    distanceRunM = 0f;
                }
            }

            targetLocation = targetLocation.ChangeElevation(TargetAltitude);
            firstUpdateLoop = false;
            UpdateListener();
        }

        protected override void ZoomIn(float speed)
        {
            if (!roadCarFound)
            {
                var movement = new Vector3(0, 0, 0);
                movement.Z += speed;
                zRadians += movement.Z;
                MoveCamera(movement);
            }
            else
            {
                rearRoadCar.ChangeSpeed(speed);
            }
        }
    }
}
