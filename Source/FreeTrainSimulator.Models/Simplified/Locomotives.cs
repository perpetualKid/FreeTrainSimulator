using System.IO;

using Orts.Formats.Msts.Files;

namespace Orts.Models.Simplified
{
    public class Locomotive : ContentBase
    {
        public static Locomotive Missing { get; } = GetLocomotive(Unknown);

        public static Locomotive Any { get; } = new Locomotive(catalog.GetString("- Any Locomotive -"), null);

        public string Name { get; private set; }
        public string Description { get; private set; }
        public string FilePath { get; private set; }

        public static Locomotive GetLocomotive(string fileName)
        {
            Locomotive result = null;

            if (File.Exists(fileName))
            {
                try
                {
                    EngineFile engFile = new EngineFile(fileName);
                    if (!string.IsNullOrEmpty(engFile.CabViewFile))
                        result = new Locomotive(engFile, fileName);
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    result = new Locomotive($"<{catalog.GetString("load error:")} {System.IO.Path.GetFileNameWithoutExtension(fileName)}>", fileName);
                }
            }
            else
            {
                result = new Locomotive($"<{catalog.GetString("missing:")} {System.IO.Path.GetFileNameWithoutExtension(fileName)}>", fileName);
            }
            return result;
        }

        private Locomotive(EngineFile engine, string fileName)
        {
            Name = engine.Name?.Trim();
            Description = engine.Description?.Trim();
            if (string.IsNullOrEmpty(Name))
                Name = $"<{catalog.GetString("unnamed:")} {System.IO.Path.GetFileNameWithoutExtension(fileName)}>";
            if (string.IsNullOrEmpty(Description))
                Description = null;
            FilePath = fileName;
        }

        private Locomotive(string name, string filePath)
        {
            Name = name;
            FilePath = filePath;
        }

        public override string ToString()
        {
            return Name;
        }

        public override bool Equals(object obj)
        {
            return obj is Locomotive locomotive && (locomotive.Name == Name || locomotive.FilePath == null || FilePath == null);
        }

        public override int GetHashCode()
        {
            return Name.GetHashCode(System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
