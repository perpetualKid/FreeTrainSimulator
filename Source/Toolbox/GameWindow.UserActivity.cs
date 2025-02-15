using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Toolbox.PopupWindows;

using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FreeTrainSimulator.Toolbox
{
    public partial class GameWindow : Game
    {
        #region public declarations


        #endregion

        #region private declarations
        private static readonly Vector2 moveLeft = new Vector2(1, 0);
        private static readonly Vector2 moveRight = new Vector2(-1, 0);
        private static readonly Vector2 moveUp = new Vector2(0, 1);
        private static readonly Vector2 moveDown = new Vector2(0, -1);

        #endregion

        private const int zoomAmplifier = 3;

        public void ChangeScreenMode()
        {
            SetScreenMode(currentScreenMode.Next());
        }

        public void CloseWindow()
        {
            PrepareExitApplication();
        }

        internal void PrepareExitApplication()
        {
            windowManager[ToolboxWindowType.QuitWindow].Open();
        }

        private void QuitWindow_OnPrintScreen(object sender, EventArgs e)
        {
            PrintScreen();
        }

        private void QuitWindow_OnWindowClosed(object sender, EventArgs e)
        {
        }

        private void QuitWindow_OnQuitGame(object sender, EventArgs e)
        {
            if (null != ctsRouteLoading && !ctsRouteLoading.IsCancellationRequested)
                ctsRouteLoading.Cancel();
            Task.Run(SaveSettings).Wait();
            waitOnExit = false;
            Exit();
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

        private static int MovementAmplifier(UserCommandArgs commandArgs)
        {
            int amplifier = 5;
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
            int amplifier = zoomAmplifier;
            if ((modifiers & KeyModifiers.Control) == KeyModifiers.Control)
                amplifier = 1;
            else if ((modifiers & KeyModifiers.Shift) == KeyModifiers.Shift)
                amplifier = 5;
            return amplifier;
        }

        private static int ZoomAmplifier(UserCommandArgs commandArgs)
        {
            return commandArgs is ModifiableKeyCommandArgs modifiableKeyCommand ? ZoomAmplifier(modifiableKeyCommand.AdditionalModifiers) : zoomAmplifier;
        }

        private void ZoomIn(UserCommandArgs commandArgs)
        {
            Zoom(ZoomAmplifier(commandArgs));
        }

        private void ZoomOut(UserCommandArgs commandArgs)
        {
            Zoom(-ZoomAmplifier(commandArgs));
        }

        private long nextUpdate;
        private void Zoom(int steps)
        {
            if (Environment.TickCount64 > nextUpdate)
            {
                contentArea?.UpdateScale(steps);
                nextUpdate = Environment.TickCount64 + 30;
            }
        }

        private void ResetZoomAndLocation()
        {
            contentArea?.ResetSize(Window.ClientBounds.Size, 60);
        }

        internal void ShowAboutWindow()
        {
            windowManager[ToolboxWindowType.AboutWindow].Open();
        }

        internal void PrintScreen()
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.DefaultExt = "png";
                dialog.FileName = $"{RuntimeInfo.ApplicationName} {DateTime.Now.ToString("yyyy-MM-dd hh-mm-ss", CultureInfo.CurrentCulture)}";
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
