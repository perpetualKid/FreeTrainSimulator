using System;
using System.IO;

using FreeTrainSimulator.Models.Handler;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Tests.FreeTrainSimulator
{
    /// <summary>
    /// Assembly-wide test setup. Redirects model persistence to an isolated temporary directory so
    /// tests never read from or write to the real user content store
    /// (<c>%AppData%\Free Train Simulator\Content</c>).
    /// </summary>
    [TestClass]
    public sealed class TestEnvironment
    {
        private static string isolatedContentRoot;

        [AssemblyInitialize]
        public static void Initialize(TestContext context)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));

            isolatedContentRoot = Path.Combine(Path.GetTempPath(), "FreeTrainSimulator", "Tests", Guid.NewGuid().ToString("N"));
            _ = Directory.CreateDirectory(isolatedContentRoot);
            ModelStore.RedirectContentRoot(isolatedContentRoot);
        }

        [AssemblyCleanup]
        public static void Cleanup()
        {
            try
            {
                if (!string.IsNullOrEmpty(isolatedContentRoot) && Directory.Exists(isolatedContentRoot))
                    Directory.Delete(isolatedContentRoot, true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup; leaving isolated temp content behind is acceptable.
            }
        }
    }
}
