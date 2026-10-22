using System;
using System.Runtime.InteropServices;

namespace FreeTrainSimulator.Common
{
    /// <summary>Identifies failures that can be retained while independent resource releases are attempted.</summary>
    public static class CleanupExceptionPolicy
    {
        /// <summary>
        /// Allows managed release failures, not process corruption, exhausted runtime resources or explicitly fatal failures.
        /// This permits cleanup to continue before rethrowing; it does not establish successful release or safe recovery.
        /// </summary>
        public static bool CanContinue(Exception error)
        {
            if (error == null || error is OutOfMemoryException or StackOverflowException or AccessViolationException or SEHException or FatalException)
                return false;

            if (error is AggregateException aggregate)
            {
                foreach (Exception inner in aggregate.InnerExceptions)
                {
                    if (!CanContinue(inner))
                        return false;
                }
            }

            return error.InnerException == null || CanContinue(error.InnerException);
        }
    }
}
