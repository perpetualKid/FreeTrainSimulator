using System;
using System.Diagnostics;

namespace Orts.Common
{
    public sealed class FatalException : Exception
    {
        public FatalException(Exception innerException)
            : base("A fatal error has occurred", innerException)
        {
            Debug.Assert(innerException != null, "The inner exception of a FatalException must not be null.");
        }
    }

    public sealed class IncompatibleSaveException : Exception
    {
        public readonly string SaveFile;
        public readonly string VersionOrBuild;

        public IncompatibleSaveException(string saveFile, string versionOrBuild, Exception innerException)
            : base(null, innerException)
        {
            SaveFile = saveFile;
            VersionOrBuild = versionOrBuild;
        }

        public IncompatibleSaveException(string saveFile, string versionOrBuild)
            : this(saveFile, versionOrBuild, null)
        {
        }
    }

    public sealed class InvalidCommandLine : Exception
    {
        public InvalidCommandLine(string message)
            : base(message)
        {
        }
    }
}
