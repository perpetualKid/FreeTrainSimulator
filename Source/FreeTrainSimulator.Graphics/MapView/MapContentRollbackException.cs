using System;

namespace FreeTrainSimulator.Graphics.MapView
{
    /// <summary>
    /// Reports failed release of partially acquired map resources when construction returned no candidate.
    /// The session must retain this evidence at its resource owner before completing the admitted operation.
    /// </summary>
    [Serializable]
    public sealed class MapContentRollbackException : AggregateException
    {
        /// <summary>Creates a failure indicating uncertain release of partially acquired map resources.</summary>
        public MapContentRollbackException() : base("Map construction rollback failed.")
        {
        }

        /// <summary>Creates a rollback failure with the specified diagnostic message.</summary>
        public MapContentRollbackException(string message) : base(message)
        {
        }

        /// <summary>Creates a rollback failure retaining its underlying release error.</summary>
        public MapContentRollbackException(string message, Exception innerException) : base(message, innerException)
        {
        }

        /// <summary>Retains both the construction error and the failed resource-release evidence.</summary>
        public MapContentRollbackException(Exception constructionFailure, Exception releaseFailure) :
            base(constructionFailure, releaseFailure)
        {
        }
    }
}
