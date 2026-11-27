using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Orts.Formats.ContentChecking;

namespace Tests.Orts.Formats.ContentChecking
{
    [TestClass]
    public class ContentCheckerTests
    {
        private string directory;

        [TestInitialize]
        public void Initialize()
        {
            directory = Directory.CreateTempSubdirectory(nameof(ContentCheckerTests)).FullName;
        }

        [TestCleanup]
        public void Cleanup()
        {
            Directory.Delete(directory, true);
        }

        [TestMethod]
        public void CheckFiles_UsesRecursivePatternsAndIndependentRunState()
        {
            string first = CreateFile("first.wav", "");
            string second = CreateFile(Path.Combine("nested", "second.wav"), "");
            List<string> checkedFiles = new List<string>();

            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { Path.Combine(directory, "*.wav"), first }, false,
                    AdditionType.None, output, null, checkedFiles.Add);

                CollectionAssert.AreEquivalent(new string[] { first, second }, checkedFiles.ToArray());
                StringAssert.Contains(output.ToString(), "Files loaded:  2");
                StringAssert.Contains(output.ToString(), "Files skipped: 0");
                StringAssert.Contains(output.ToString(), "Files failed:  0");
            }

            checkedFiles.Clear();
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { second }, false, AdditionType.None,
                    output, null, checkedFiles.Add);

                CollectionAssert.AreEqual(new string[] { second }, checkedFiles.ToArray());
                StringAssert.Contains(output.ToString(), "Files loaded:  1");
            }
        }

        [TestMethod]
        [DataRow("sound.wav", "no runtime WAV checker")]
        [DataRow("texture.ace", "no graphics device")]
        public void CheckFiles_ReportsUnavailableCapabilitiesAsSkipped(string name, string reason)
        {
            string file = CreateFile(name, "");
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { file }, false, AdditionType.All, output, null, null);

                string report = output.ToString();
                StringAssert.Contains(report, reason);
                StringAssert.Contains(report, file);
                StringAssert.Contains(report, "Files loaded:  0");
                StringAssert.Contains(report, "Files skipped: 1");
                StringAssert.Contains(report, "Warnings:      1");
                Assert.IsFalse(report.Contains(": OK", StringComparison.Ordinal));

                output.Write("still open");
                StringAssert.EndsWith(output.ToString(), "still open");
            }
        }

        [TestMethod]
        [DataRow(AdditionType.None, 0)]
        [DataRow(AdditionType.Dependent, 0)]
        [DataRow(AdditionType.Referenced, 1)]
        [DataRow(AdditionType.All, 1)]
        public void CheckFiles_PreservesSmsReferenceTraversal(AdditionType additions, int expectedWaveChecks)
        {
            string wave = CreateFile("referenced.wav", "");
            string sms = CreateFile("sound.sms", "SIMISA@@@@@@@@@@JINX0t1t______\n" +
                "Tr_SMS ( ScalabiltyGroup ( 0 Streams ( 1 Stream ( Triggers ( 1 " +
                "Initial_Trigger ( PlayOneShot ( 1 File ( referenced.wav 1 ) ) ) ) ) ) ) )");
            List<string> checkedFiles = new List<string>();

            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { sms }, false, additions, output, null, checkedFiles.Add);

                Assert.HasCount(expectedWaveChecks, checkedFiles);
                if (expectedWaveChecks > 0)
                    Assert.AreEqual(wave, checkedFiles[0]);
                StringAssert.Contains(output.ToString(), $"Files loaded:  {1 + expectedWaveChecks}");
                StringAssert.Contains(output.ToString(), "Files failed:  0");
            }
        }

        [TestMethod]
        public void CheckFiles_ReportsReferencedWavWithoutHandler()
        {
            CreateFile("referenced.wav", "");
            string sms = CreateFile("sound.sms", "SIMISA@@@@@@@@@@JINX0t1t______\n" +
                "Tr_SMS ( ScalabiltyGroup ( 0 Streams ( 1 Stream ( Triggers ( 1 " +
                "Initial_Trigger ( PlayOneShot ( 1 File ( referenced.wav 1 ) ) ) ) ) ) ) )");

            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { sms }, false, AdditionType.Referenced, output, null, null);

                StringAssert.Contains(output.ToString(), "no runtime WAV checker");
                StringAssert.Contains(output.ToString(), "Files loaded:  1");
                StringAssert.Contains(output.ToString(), "Files skipped: 1");
            }
        }

        [TestMethod]
        public void CheckFiles_LoadsSignalScriptsWithConfigurationContext()
        {
            string script = CreateFile("sigscr.dat", "SCRIPT CHECKER\nEXTERN FLOAT STATE;\nSTATE = 0;\n");
            string config = CreateFile("sigcfg.dat", "SIMISA@@@@@@@@@@JINX0G0t______\n" +
                "LightTextures ( 0 ) LightsTab ( 0 ) SignalTypes ( 0 ) SignalShapes ( 0 ) " +
                "ScriptFiles ( 1 ScriptFile ( sigscr.dat ) )");

            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { script, config }, false, AdditionType.Dependent,
                    output, null, null);

                string report = output.ToString();
                StringAssert.Contains(report, config + ": OK");
                StringAssert.Contains(report, script + ": ");
                Assert.IsFalse(report.Contains("can not be loaded independently", StringComparison.Ordinal));
                Assert.IsTrue(report.IndexOf(config, StringComparison.Ordinal) < report.IndexOf(script, StringComparison.Ordinal));
                StringAssert.Contains(report, "Files loaded:  2");
                StringAssert.Contains(report, "Files skipped: 0");
                StringAssert.Contains(report, "Files failed:  0");
            }
        }

        [TestMethod]
        public void CheckFiles_ReportsClassificationAndCallbackFailuresAndContinues()
        {
            string malformed = CreateFile("a.raw", "");
            string failingWave = CreateFile("failing.wav", "");
            string validWave = CreateFile("valid.wav", "");
            List<string> checkedFiles = new List<string>();

            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { malformed, failingWave, validWave }, false,
                    AdditionType.Referenced, output, null, file =>
                    {
                        if (file == failingWave)
                            throw new InvalidDataException("WAV check failed");
                        checkedFiles.Add(file);
                    });

                CollectionAssert.AreEqual(new string[] { validWave }, checkedFiles.ToArray());
                string report = output.ToString();
                StringAssert.Contains(report, malformed);
                StringAssert.Contains(report, "WAV check failed");
                StringAssert.Contains(report, "Files loaded:  1");
                StringAssert.Contains(report, "Files failed:  2");
                StringAssert.Contains(report, "Errors:        2");
                Assert.IsFalse(report.Contains(failingWave + ": OK", StringComparison.Ordinal));
            }
        }

        [TestMethod]
        public void CheckFiles_DoesNotDiscoverDependenciesAfterParserFailure()
        {
            string terrain = CreateFile("invalid.t", "not a terrain file");
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { terrain }, false, AdditionType.All, output, null, null);

                string report = output.ToString();
                StringAssert.Contains(report, terrain);
                StringAssert.Contains(report, "Files loaded:  0");
                StringAssert.Contains(report, "Files failed:  1");
                Assert.IsFalse(report.Contains("_y.raw", StringComparison.Ordinal));
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(true, true)]
        public void CheckFiles_PreservesHostListenersAndHonorsVerbosity(bool verbose, bool expectedInformation)
        {
            string wave = CreateFile("sound.wav", "");
            using (StringWriter hostOutput = new StringWriter(CultureInfo.InvariantCulture))
            using (TextWriterTraceListener hostListener = new TextWriterTraceListener(hostOutput))
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                Trace.Listeners.Add(hostListener);
                try
                {
                    TraceListener[] listeners = Trace.Listeners.Cast<TraceListener>().ToArray();
                    ContentChecker.CheckFiles(new string[] { wave }, verbose, AdditionType.None, output, null,
                        file => Trace.TraceInformation("WAV inspection information"));

                    CollectionAssert.AreEqual(listeners, Trace.Listeners.Cast<TraceListener>().ToArray());
                    StringAssert.Contains(hostOutput.ToString(), "WAV inspection information");
                    Assert.AreEqual(expectedInformation, output.ToString().Contains("WAV inspection information", StringComparison.Ordinal));
                    output.Write("still open");
                    StringAssert.EndsWith(output.ToString(), "still open");
                }
                finally
                {
                    Trace.Listeners.Remove(hostListener);
                }
            }
        }

        [TestMethod]
        public void CheckFiles_RestoresListenersWhenCancelled()
        {
            string wave = CreateFile("sound.wav", "");
            TraceListener[] listeners = Trace.Listeners.Cast<TraceListener>().ToArray();
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                Assert.ThrowsExactly<OperationCanceledException>(() => ContentChecker.CheckFiles(
                    new string[] { wave }, false, AdditionType.None, output, null,
                    file => throw new OperationCanceledException()));

                CollectionAssert.AreEqual(listeners, Trace.Listeners.Cast<TraceListener>().ToArray());
                output.Write("still open");
                StringAssert.EndsWith(output.ToString(), "still open");
            }
        }

        [TestMethod]
        public void CheckFiles_ReportsMissingInputAndContinues()
        {
            string missing = Path.Combine(directory, "missing.wav");
            string existing = CreateFile("existing.wav", "");
            List<string> checkedFiles = new List<string>();
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { missing, existing }, false, AdditionType.None,
                    output, null, checkedFiles.Add);

                CollectionAssert.AreEqual(new string[] { existing }, checkedFiles.ToArray());
                StringAssert.Contains(output.ToString(), missing);
                StringAssert.Contains(output.ToString(), "Files loaded:  1");
                StringAssert.Contains(output.ToString(), "Files failed:  1");
                StringAssert.Contains(output.ToString(), "Errors:        1");
            }
        }

        private string CreateFile(string name, string contents)
        {
            string file = Path.Combine(directory, name);
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllText(file, contents, Encoding.ASCII);
            return file;
        }
    }
}