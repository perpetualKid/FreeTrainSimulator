using System;
using System.IO;
using System.Windows.Forms;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using Orts.Common;
using Orts.Common.Info;
using Orts.Common.Input;

namespace Orts.TrackViewer
{
    public partial class GameWindow : Game
    {
        private static readonly Vector2 moveLeft = new Vector2(1, 0);
        private static readonly Vector2 moveRight = new Vector2(-1, 0);
        private static readonly Vector2 moveUp = new Vector2(0, 1);
        private static readonly Vector2 moveDown = new Vector2(0, -1);

        public void ChangeScreenMode()
        {
            SetScreenMode(currentScreenMode.Next());
        }

        public void CloseWindow()
        {
            ExitApplication();
        }

        internal bool ExitApplication()
        {
            if (MessageBox.Show(Catalog.GetString($"Do you want to quit {RuntimeInfo.ApplicationName} now?"), Catalog.GetString("Quit"), MessageBoxButtons.OKCancel) == DialogResult.OK)
            {
                SaveSettings();
                if (null != ctsRouteLoading && !ctsRouteLoading.IsCancellationRequested)
                    ctsRouteLoading.Cancel();
                Exit();
                return true;
            }
            return false;
        }

        public void MouseDragging(UserCommandArgs userCommandArgs)
        {
            if (userCommandArgs is PointerMoveCommandArgs mouseMoveCommandArgs)
            {
                contentArea?.UpdatePosition(mouseMoveCommandArgs.Delta);
            }
        }

        public void MouseWheel(UserCommandArgs userCommandArgs, KeyModifiers modifiers)
        {
            if (userCommandArgs is ScrollCommandArgs mouseWheelCommandArgs)
            {
                contentArea?.UpdateScaleAt(mouseWheelCommandArgs.Position, Math.Sign(mouseWheelCommandArgs.Delta) * ZoomAmplifier(modifiers));
            }
        }

        private void MoveByKeyLeft(UserCommandArgs commandArgs)
        {
            contentArea?.UpdatePosition(moveLeft * MovementAmplifier(commandArgs));
        }

        private void MoveByKeyRight(UserCommandArgs commandArgs)
        {
            contentArea?.UpdatePosition(moveRight * MovementAmplifier(commandArgs));
        }

        private void MoveByKeyUp(UserCommandArgs commandArgs)
        {
            contentArea?.UpdatePosition(moveUp * MovementAmplifier(commandArgs));
        }

        private void MoveByKeyDown(UserCommandArgs commandArgs)
        {
            contentArea?.UpdatePosition(moveDown * MovementAmplifier(commandArgs));
        }

        private static float MovementAmplifier(UserCommandArgs commandArgs)
        {
            float amplifier = 5;
            if (commandArgs is ModifiableKeyCommandArgs modifiableKeyCommand)
            {
                if ((modifiableKeyCommand.AdditionalModifiers & KeyModifiers.Control) == KeyModifiers.Control)
                    amplifier = 1;
                else if ((modifiableKeyCommand.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                    amplifier = 10;
            }
            return amplifier;
        }

        private static int ZoomAmplifier(KeyModifiers modifiers)
        {
            int amplifier = 3;
            if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
                amplifier = 1;
            else if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                amplifier = 5;
            return amplifier;
        }

        private static int ZoomAmplifier(UserCommandArgs commandArgs)
        {
            int amplifier = 3;
            if (commandArgs is ModifiableKeyCommandArgs modifiableKeyCommand)
            {
                if ((modifiableKeyCommand.AdditionalModifiers & KeyModifiers.Control) == KeyModifiers.Control)
                    amplifier = 1;
                else if ((modifiableKeyCommand.AdditionalModifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                    amplifier = 5;
            }
            return amplifier;
        }

        private void ZoomIn(UserCommandArgs commandArgs)
        {
            Zoom(ZoomAmplifier(commandArgs));
        }

        private void ZoomOut(UserCommandArgs commandArgs)
        {
            Zoom(-ZoomAmplifier(commandArgs));
        }

        private DateTime nextUpdate;
        private void Zoom(int steps)
        {
            if (DateTime.UtcNow > nextUpdate)
            {
                contentArea?.UpdateScale(steps);
                nextUpdate = DateTime.UtcNow.AddMilliseconds(30);
            }
        }

        private void ResetZoomAndLocation()
        {
            contentArea?.ResetSize(Window.ClientBounds.Size, 60);
        }

        internal void PrintScreen()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.DefaultExt = "png";
                dialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
                dialog.Filter = $"{Catalog.GetString("Image files (*.png)")}|*.png";
                if (dialog.ShowDialog() == DialogResult.OK)
                {
                    byte[] backBuffer = new byte[graphicsDeviceManager.PreferredBackBufferWidth * graphicsDeviceManager.PreferredBackBufferHeight * 4];
                    GraphicsDevice graphicsDevice = graphicsDeviceManager.GraphicsDevice;
                    using (RenderTarget2D screenshot = new RenderTarget2D(graphicsDevice, graphicsDeviceManager.PreferredBackBufferWidth, graphicsDeviceManager.PreferredBackBufferHeight, false, graphicsDevice.PresentationParameters.BackBufferFormat, DepthFormat.None))
                    {
                        graphicsDevice.GetBackBufferData(backBuffer);
                        screenshot.SetData(backBuffer);
                        using (FileStream stream = File.OpenWrite(dialog.FileName))
                        {
                            screenshot.SaveAsPng(stream, graphicsDeviceManager.PreferredBackBufferWidth, graphicsDeviceManager.PreferredBackBufferHeight);
                        }
                    }
                }
            }
        }

    }
}
