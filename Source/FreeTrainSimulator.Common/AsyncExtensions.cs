using System.Threading;
using System.Threading.Tasks;

namespace FreeTrainSimulator.Common
{
    public static class AsyncExtensions
    {
        public static async ValueTask<CancellationTokenSource> ResetCancellationTokenSource(this CancellationTokenSource cts, SemaphoreSlim semaphore, bool cancel)
        {
            if (null != cts)
            {
                try
                {
                    await semaphore.WaitAsync().ConfigureAwait(false);
                    if (cancel && cts != null && !cts.IsCancellationRequested)
                        await cts.CancelAsync().ConfigureAwait(false);
                    cts?.Dispose();
                }
                finally
                {
                    _ = semaphore.Release();
                }
            }
            // Create a new cancellation token source so that can cancel all the tokens again 
            return new CancellationTokenSource();
        }

    }
}
