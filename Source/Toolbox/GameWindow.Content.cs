using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Common;
using FreeTrainSimulator.Common.Info;
using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.DrawableComponents;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Graphics.Window;
using FreeTrainSimulator.Graphics.Xna;
using FreeTrainSimulator.Models.Content;
using FreeTrainSimulator.Runtime;
using FreeTrainSimulator.Toolbox.PathEditing;
using FreeTrainSimulator.Toolbox.PopupWindows;

using Microsoft.Xna.Framework;

namespace FreeTrainSimulator.Toolbox
{
    public partial class GameWindow
    {
        private readonly ToolboxSession session;
        private bool ownedResourcesReleaseAttempted;
        private ExceptionDispatchInfo ownedResourcesReleaseFailure;

        internal ToolboxSession Session => session;

        internal event EventHandler<PathEditorAvailabilityChangedEventArgs> OnPathEditorChanged;
        internal event EventHandler<UnsavedPathConfirmationEventArgs> UnsavedPathConfirmationRequested;
        internal event EventHandler SaveTrainPathRequested;

        private ToolboxSession CreateSession()
        {
            ToolboxSession created = new ToolboxSession(this);
            created.OnPathEditorChanged += Session_OnPathEditorChanged;
            created.UnsavedPathConfirmationRequested += Session_UnsavedPathConfirmationRequested;
            created.SaveTrainPathRequested += Session_SaveTrainPathRequested;
            return created;
        }

        // Resolve subscribers when raised, not at construction or stop: the shell attaches after startup.
        private void Session_OnPathEditorChanged(object sender, PathEditorAvailabilityChangedEventArgs e)
            => OnPathEditorChanged?.Invoke(this, e);

        private void Session_UnsavedPathConfirmationRequested(object sender, UnsavedPathConfirmationEventArgs e)
        {
            if (UnsavedPathConfirmationRequested == null)
                e.Completion.TrySetResult(false);
            else
                UnsavedPathConfirmationRequested.Invoke(this, e);
        }

        private void Session_SaveTrainPathRequested(object sender, EventArgs e)
            => SaveTrainPathRequested?.Invoke(this, e);

        /// <summary>Checks the independent dispatch owner, including after the render form is lost.</summary>
        internal void VerifySessionOwner()
        {
            if (!ownerControl.IsHandleCreated || ownerControl.IsDisposed || ownerControl.InvokeRequired)
                throw new InvalidOperationException("The Toolbox session must be disposed on its game owner.");
        }

        /// <summary>Creates device-side map hosts on the owner; native preparation remains session-owned.</summary>
        internal ToolboxContent CreateRouteContent(RuntimeDataResolver runtimeData) => 
            XnaMapContentFactory.CreateToolboxContent(this, Components.OfType<MouseInputGameComponent>().FirstOrDefault(),
                new XnaMapInsetHost(Components.OfType<InsetComponent>().FirstOrDefault()),
                new XnaMapTextureHelperHost(Components.OfType<TextureContentComponent>()), runtimeData);

        /// <summary>Attaches a committed map, or releases the current map after its editor has been released.</summary>
        internal void AttachRouteContent(ToolboxContent content)
        {
            if (content != null || contentArea != null)
                ContentArea = content == null ? null : ((IXnaMapShellHost)content.ShellHost).Component as ContentArea;
        }

        /// <summary>Releases an uncommitted device candidate without changing the committed map.</summary>
        internal static void DiscardRouteContent(ToolboxContent content)
            => ((content?.ShellHost as IXnaMapShellHost)?.Component as ContentArea)?.Dispose();

        /// <summary>Creates the editor's platform input wiring while retaining session admission and commit gates.</summary>
        internal PathEditor CreatePathEditor(ToolboxContent content)
        {
            PathEditor editor = new PathEditor(content, userCommandController, action => InvokeOnGameThreadAsync(() =>
            {
                action();
                return Task.CompletedTask;
            }), commit => InvokeOnGameThreadAsync(() => Task.FromResult(!session.IsStopping && commit())), session.RunAsync);
            editor.OnPathChanged += PathEditor_MarkToolWindowDirty;
            editor.OnPathUpdated += PathEditor_MarkToolWindowDirty;
            return editor;
        }

        private void PathEditor_MarkToolWindowDirty(object sender, PathEditorChangedEventArgs e)
            => HostedServices?.TrainPathToolWindow?.MarkDirty();

        /// <summary>Shows route loading status, or closes it when the request has ended.</summary>
        internal void SetRouteLoadingStatus(RouteModelHeader route)
        {
            if (windowManager == null)
                return;
            if (route == null)
                _ = windowManager[ToolboxWindowType.StatusWindow].Close();
            else
            {
                (windowManager[ToolboxWindowType.StatusWindow] as StatusTextWindow).RouteName = route.Name;
                _ = windowManager[ToolboxWindowType.StatusWindow].Open();
            }
        }

        /// <summary>Restores the committed viewport using the current, replaceable settings model.</summary>
        internal void RestoreRouteViewport()
            => ContentArea?.PresetPosition(ToolboxSettings.ContentPosition, ToolboxSettings.ContentScale);

        /// <summary>Presents the existing startup guidance when native content discovery requires analysis.</summary>
        internal static void ShowContentAnalysisRequired()
            => MessageBox.Show($"In an effort to optimize content, {RuntimeInfo.ProductName} will need to analyze existing content files and folders." + Environment.NewLine + Environment.NewLine +
                $"Please close {RuntimeInfo.ApplicationName}, and use the Menu-application to review current content folder settings for further analysis.", "Please read!");

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
            // Session cleanup re-enters the platform; snapshot ownership before any calls.
            Control dispatchControl = ownerControl;
            WindowManager<ToolboxWindowType> ownedWindowManager = windowManager;
            if (!disposing)
            {
                base.Dispose(false);
                return;
            }
            if (ownedResourcesReleaseAttempted)
            {
                ownedResourcesReleaseFailure?.Throw();
                return;
            }

            // A failed session release must leave its dispatch owner alive and fail every retry.
            session?.Dispose();
            ownedResourcesReleaseAttempted = true;
            try
            {
                List<Exception> errors = new List<Exception>();
                void Release(Action release)
                {
                    try
                    {
                        release();
                    }
                    catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                    {
                        errors.Add(error);
                    }
                }

                try
                {
                    hostedPointerGesture.Reset();
                }
                catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                {
                    errors.Add(error);
                }
                if (windowForm != null)
                {
                    try
                    {
                        windowForm.MouseDown -= WindowForm_MouseDown;
                    }
                    catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                    {
                        errors.Add(error);
                    }
                    try
                    {
                        windowForm.MouseUp -= WindowForm_MouseUp;
                    }
                    catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                    {
                        errors.Add(error);
                    }
                    try
                    {
                        windowForm.MouseCaptureChanged -= WindowForm_MouseCaptureChanged;
                    }
                    catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                    {
                        errors.Add(error);
                    }
                }
                try
                {
                    loadRouteSemaphore?.Dispose();
                }
                catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                {
                    errors.Add(error);
                }
                try
                {
                    ctsProfileLoading?.Dispose();
                }
                catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                {
                    errors.Add(error);
                }
                if (ownedWindowManager != null)
                {
                    // Remove direct ownership from MonoGame before disposal; retain any failure here.
                    try
                    {
                        Components.Remove(ownedWindowManager);
                    }
                    catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                    {
                        errors.Add(error);
                    }
                    try
                    {
                        ownedWindowManager.Dispose();
                    }
                    catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                    {
                        errors.Add(error);
                    }
                    windowManager = null;
                }
                // MonoGame aborts its component loop on the first failure. Drain each owner here,
                // removing it first so base cleanup cannot repeat an attempted release.
                HashSet<IDisposable> released = new HashSet<IDisposable>(ReferenceEqualityComparer.Instance);
                if (ownedWindowManager != null)
                    released.Add(ownedWindowManager);

                foreach (IGameComponent component in Components.ToArray())
                {
                    Release(() => Components.Remove(component));
                    if (component is IDisposable resource && released.Add(resource))
                        Release(resource.Dispose);
                }
                Release(Components.Clear);
                Release(() => TextTextureRenderer.ReleaseSharedResources(this));
                try
                {
                    spriteBatch?.Dispose();
                }
                catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                {
                    errors.Add(error);
                }
                try
                {
                    windowForm?.Dispose();
                }
                catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                {
                    errors.Add(error);
                }
                // Devices stay base-owned, after components. The independent transport is always last.
                try
                {
                    base.Dispose(true);
                }
                catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                {
                    errors.Add(error);
                }
                try
                {
                    dispatchControl?.Dispose();
                }
                catch (Exception error) when (CleanupExceptionPolicy.CanContinue(error))
                {
                    errors.Add(error);
                }
                if (errors.Count > 0)
                {
                    ownedResourcesReleaseFailure = ExceptionDispatchInfo.Capture(errors.Count == 1 ? errors[0] : new AggregateException(errors));
                    ownedResourcesReleaseFailure.Throw();
                }
            }
            catch (Exception error)
            {
                ownedResourcesReleaseFailure = ExceptionDispatchInfo.Capture(error);
                throw;
            }
        }
    }
}
