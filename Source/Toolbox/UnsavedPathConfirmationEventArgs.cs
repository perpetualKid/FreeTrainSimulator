using System;
using System.Threading.Tasks;

namespace FreeTrainSimulator.Toolbox
{
    internal sealed class UnsavedPathConfirmationEventArgs : EventArgs
    {
        public TaskCompletionSource<bool> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public static Task<bool> RequestAsync(bool unsavedChanges, object sender, EventHandler<UnsavedPathConfirmationEventArgs> confirmationRequested)
        {
            if (!unsavedChanges)
                return Task.FromResult(true);
            if (confirmationRequested == null)
                return Task.FromResult(false);

            UnsavedPathConfirmationEventArgs args = new();
            confirmationRequested(sender, args);
            return args.Completion.Task;
        }
    }
}