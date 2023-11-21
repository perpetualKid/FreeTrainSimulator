using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Orts.Common
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
    public sealed class IncompatibleSaveException : Exception
    {
        public string SaveFile { get; private set; }
        public string Version { get; private set; }

        public IncompatibleSaveException(string saveFile, string version, Exception innerException)
            : base(null, innerException)
        {
            SaveFile = saveFile;
            Version = version;
        }

        public IncompatibleSaveException(string saveFile, string version)
            : this(saveFile, version, null)
        {
        }

        public IncompatibleSaveException()
        {
        }

        public IncompatibleSaveException(string message) : base(message)
        {
        }

        public IncompatibleSaveException(string message, Exception innerException) : base(message, innerException)
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
