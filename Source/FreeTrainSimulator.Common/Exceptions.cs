using System;
using System.Diagnostics;
using System.Linq;

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
        private readonly string[] arguments;
        public string ArgumentsList => string.Join("\n", arguments?.Select(d => "\u2022 " + d).ToArray());

        public InvalidCommandLineException(string message)
            : base(message)
        {
        }

        public InvalidCommandLineException(string message, string[] arguments)
            : base(message)
        {
            this.arguments = arguments;
        }

        public InvalidCommandLineException()
        {
        }

        public InvalidCommandLineException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
