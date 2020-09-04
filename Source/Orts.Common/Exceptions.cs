using System;
using System.Diagnostics;

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
    }

    [Serializable]
    public sealed class InvalidCommandLine : Exception
    {
        public InvalidCommandLine(string message)
            : base(message)
        {
        }
    }
}
