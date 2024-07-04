using System;
using System.Diagnostics;

namespace FreeTrainSimulator.Common
{
    [Serializable]
    public sealed class FatalException : Exception
    {
        public FatalException(Exception innerException)
            : base("A fatal error has occurred", innerException)
        {
            Debug.Assert(innerException != null, "The inner exception of a FatalException must not be null.");
        }

        public FatalException()
            : base("A fatal error has occurred")
        {
        }

        public FatalException(string message) : base(message)
        {
        }

        public FatalException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    [Serializable]
    public sealed class InvalidCommandLineException : Exception
    {
        public InvalidCommandLineException(string message)
            : base(message)
        {
        }

        public InvalidCommandLineException()
        {
        }

        public InvalidCommandLineException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
