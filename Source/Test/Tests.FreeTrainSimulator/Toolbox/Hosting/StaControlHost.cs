using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using FreeTrainSimulator.Toolbox.Hosting;

namespace Tests.FreeTrainSimulator.Toolbox.Hosting
{
    internal sealed class StaControlHost : IAsyncDisposable
    {
        private readonly TaskCompletionSource ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<Action, Task> dispatch;
        private Action exitThread;
        private Task stopTask;
        private bool threadStarted;

        public Form Target { get; private set; }

        public Control DispatchControl { get; private set; }

        public int OwnerThreadId { get; private set; }

        private StaControlHost()
        {
        }

        public static async Task<StaControlHost> StartAsync()
        {
            StaControlHost host = new StaControlHost();
            bool ownershipTransferred = false;
            try
            {
                Thread thread = new Thread(host.Run)
                {
                    IsBackground = true,
                    Name = "Toolbox.Tests.ControlOwner",
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                host.threadStarted = true;
                await host.ready.Task.ConfigureAwait(false);
                ownershipTransferred = true;
                return host;
            }
            finally
            {
                if (!ownershipTransferred)
                    await host.DisposeAsync().ConfigureAwait(false);
            }
        }

        public Task OnOwnerAsync(Action action) => dispatch(action);

        public ValueTask DisposeAsync() => new ValueTask(stopTask ??= StopAsync());

        private async Task StopAsync()
        {
            if (!threadStarted)
                return;

            if (ready.Task.IsCompletedSuccessfully && !stopped.Task.IsCompleted)
                await OnOwnerAsync(exitThread).ConfigureAwait(false);

            await stopped.Task.ConfigureAwait(false);
        }

        private void Run()
        {
            try
            {
                using (ApplicationContext context = new ApplicationContext())
                using (Control pump = new Control())
                using (DispatchControl = new GameThreadControl())
                using (Target = new Form())
                {
                    OwnerThreadId = Environment.CurrentManagedThreadId;
                    _ = pump.Handle;
                    _ = DispatchControl.Handle;
                    _ = Target.Handle;
                    dispatch = action => pump.InvokeAsync(action, CancellationToken.None);
                    exitThread = context.ExitThread;
                    pump.BeginInvoke(() => ready.SetResult());
                    Application.Run(context);
                }
            }
            catch (InvalidOperationException exception)
            {
                PublishFailure(exception);
            }
            catch (System.ComponentModel.Win32Exception exception)
            {
                PublishFailure(exception);
            }
            finally
            {
                stopped.TrySetResult();
            }
        }

        private void PublishFailure(Exception exception)
        {
            if (!ready.TrySetException(exception))
                stopped.TrySetException(exception);
        }
    }
}
