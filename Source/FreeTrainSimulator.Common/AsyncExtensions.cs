using System;
using System.Threading;
using System.Threading.Tasks;

namespace FreeTrainSimulator.Common
{
    public static class AsyncExtensions
    {
        public static async ValueTask<CancellationTokenSource> ResetCancellationTokenSource(this CancellationTokenSource cts, SemaphoreSlim semaphore, bool cancel)
        {
            ArgumentNullException.ThrowIfNull(semaphore, nameof(semaphore));
            try
            {
                await semaphore.WaitAsync().ConfigureAwait(false);
                if (null != cts)
                {
                    if (cancel && !cts.IsCancellationRequested)
                        await cts.CancelAsync().ConfigureAwait(false);
                    cts.Dispose();
                }
                // Create a new cancellation token source so that can cancel all the tokens again 
                return new CancellationTokenSource();
            }
            finally
            {
                _ = semaphore.Release();
            }
        }
    }
}
