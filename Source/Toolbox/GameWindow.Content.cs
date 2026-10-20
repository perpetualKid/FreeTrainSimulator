using System;
using System.Threading.Tasks;

using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Toolbox.PathEditing;

namespace FreeTrainSimulator.Toolbox
{
    public partial class GameWindow
    {
        private readonly ToolboxSession session;

        internal ToolboxSession Session => session;

        internal event EventHandler<PathEditorAvailabilityChangedEventArgs> OnPathEditorChanged;
        internal event EventHandler<UnsavedPathConfirmationEventArgs> UnsavedPathConfirmationRequested;
        internal event EventHandler SaveTrainPathRequested;

        internal PathEditor HostedPathEditor => session?.ExistingPathEditor;
        internal ITrainPathToolingContext HostedTrainPathToolingContext => session?.TrainPathToolingContext;
        internal bool HasUnsavedPathChanges => session?.HasUnsavedPathChanges == true;

        internal Task<bool> LoadPathAsync(PathModelHeader path) => session.LoadPathAsync(path);
        internal Task UnloadPathAsync() => session.LoadPathAsync(null);
        internal Task<bool> ConfirmDiscardUnsavedPathsAsync() => session.ConfirmDiscardUnsavedPathsAsync();

        internal void EditPath() => ObserveGameThreadOperation(session.EditPathAsync());
        internal void SavePath() => ObserveGameThreadOperation(session.RequestSavePathAsync());

        internal Task StopSessionAsync() => InvokeOnGameThreadAsync(session.StopAndDrainAsync);

        // Tool/input commands enter the session; internal completions use raw transport instead.
        internal void InvokeSessionCommand(Action action) => ObserveGameThreadOperation(InvokeSessionCommandAsync(action));

        internal Task InvokeSessionCommandAsync(Action action)
        {
            ArgumentNullException.ThrowIfNull(action);
            return InvokeOnGameThreadAsync(() => session.RunAsync(() =>
            {
                action();
                return Task.CompletedTask;
            }));
        }

        internal void ActivatePathEditingInput()
        {
            SetHostedInputCaptured(false);
            FocusHostedWindow();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                session?.Dispose();
            hostedPointerGesture.Reset();
            if (windowForm != null)
            {
                windowForm.MouseDown -= WindowForm_MouseDown;
                windowForm.MouseUp -= WindowForm_MouseUp;
                windowForm.MouseCaptureChanged -= WindowForm_MouseCaptureChanged;
            }
            loadRouteSemaphore?.Dispose();
            ctsProfileLoading?.Dispose();
            windowManager?.Dispose();
            spriteBatch?.Dispose();
            graphicsDeviceManager?.Dispose();
            windowForm?.Dispose();
            try
            {
                base.Dispose(disposing);
            }
            finally
            {
                if (disposing)
                    ownerControl?.Dispose();
            }
        }
    }
}
