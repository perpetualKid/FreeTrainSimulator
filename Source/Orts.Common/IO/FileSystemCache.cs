using System;
using System.IO;
using System.Threading.Tasks;

namespace Orts.Common.IO
{
    public static class FileSystemCache
    {
        private static string[] files;
        private static string[] directories;
        private static Task initTask;

        public static void Initialize(DirectoryInfo root)
        {
            if (!root.Exists)
                throw new FileNotFoundException($"Root folder for {root.FullName} does not exist");
            initTask = Task.WhenAll(ReadDirectories(root.FullName), ReadFiles(root.FullName));
        }

        public static bool FileExists(string fileName)
        {
            if (!initTask.IsCompleted)
                initTask.Wait();
            fileName = Path.GetFullPath(fileName);
            return Array.BinarySearch(files, fileName, StringComparer.InvariantCultureIgnoreCase) > 0;
        }

        public static bool DirectoryExists(string directoryName)
        {
            if (!initTask.IsCompleted)
                initTask.Wait();
            directoryName = Path.GetFullPath(directoryName);
            return Array.BinarySearch(directories, directoryName, StringComparer.InvariantCultureIgnoreCase) > 0;
        }

        private static Task ReadDirectories(string root)
        {
            directories = Directory.GetDirectories(root, "*", SearchOption.AllDirectories);
            Array.Sort(directories);
            return Task.CompletedTask;
        }

        private static Task ReadFiles(string root)
        {
            files = Directory.GetFiles(root, "*.*", SearchOption.AllDirectories);
            Array.Sort(files);
            return Task.CompletedTask;
        }

    }
}
