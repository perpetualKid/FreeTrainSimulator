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
    [OSCondition(OperatingSystems.Windows)]
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
        public void CheckFilesUsesRecursivePatternsAndIndependentRunState()
        {
            string first = CreateFile("first.wav", "");
            string second = CreateFile(Path.Combine("nested", "second.wav"), "");
            List<string> checkedFiles = new List<string>();

            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { Path.Combine(directory, "*.wav"), first }, false, AdditionType.None, output, null, checkedFiles.Add);

                Assert.AreSequenceEqual(new string[] { first, second }, checkedFiles.ToArray(), SequenceOrder.InAnyOrder);
                Assert.Contains("Files loaded:  2", output.ToString());
                Assert.Contains("Files skipped: 0", output.ToString());
                Assert.Contains("Files failed:  0", output.ToString());
            }

            checkedFiles.Clear();
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { second }, false, AdditionType.None,
                    output, null, checkedFiles.Add);

                Assert.AreSequenceEqual(new string[] { second }, checkedFiles.ToArray());
                Assert.Contains("Files loaded:  1", output.ToString());
            }
        }

        [TestMethod]
        [DataRow("sound.wav", "no runtime WAV checker")]
        [DataRow("texture.ace", "no graphics device")]
        public void CheckFilesReportsUnavailableCapabilitiesAsSkipped(string name, string reason)
        {
            string file = CreateFile(name, "");
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { file }, false, AdditionType.All, output, null, null);

                string report = output.ToString();
                Assert.Contains(reason, report);
                Assert.Contains(file, report);
                Assert.Contains("Files loaded:  0", report);
                Assert.Contains("Files skipped: 1", report);
                Assert.Contains("Warnings:      1", report);
                Assert.IsFalse(report.Contains(": OK", StringComparison.Ordinal));

                output.Write("still open");
                Assert.EndsWith("still open", output.ToString());
            }
        }

        [TestMethod]
        [DataRow(AdditionType.None, 0)]
        [DataRow(AdditionType.Dependent, 0)]
        [DataRow(AdditionType.Referenced, 1)]
        [DataRow(AdditionType.All, 1)]
        public void CheckFilesPreservesSmsReferenceTraversal(AdditionType additions, int expectedWaveChecks)
        {
            string wave = CreateFile("referenced.wav", "");
            string sms = CreateFile("sound.sms", "SIMISA@@@@@@@@@@JINX0t1t______\n" +
                "Tr_SMS ( ScalabiltyGroup ( 0 Streams ( 1 Stream ( Triggers ( 1 Initial_Trigger ( PlayOneShot ( 1 File ( referenced.wav 1 ) ) ) ) ) ) ) )");
            List<string> checkedFiles = new List<string>();

            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { sms }, false, additions, output, null, checkedFiles.Add);

                Assert.HasCount(expectedWaveChecks, checkedFiles);
                if (expectedWaveChecks > 0)
                    Assert.AreEqual(wave, checkedFiles[0]);
                Assert.Contains($"Files loaded:  {1 + expectedWaveChecks}", output.ToString());
                Assert.Contains("Files failed:  0", output.ToString());
            }
        }

        [TestMethod]
        public void CheckFilesReportsReferencedWavWithoutHandler()
        {
            CreateFile("referenced.wav", "");
            string sms = CreateFile("sound.sms", "SIMISA@@@@@@@@@@JINX0t1t______\n" +
                "Tr_SMS ( ScalabiltyGroup ( 0 Streams ( 1 Stream ( Triggers ( 1 Initial_Trigger ( PlayOneShot ( 1 File ( referenced.wav 1 ) ) ) ) ) ) ) )");

            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { sms }, false, AdditionType.Referenced, output, null, null);

                Assert.Contains("no runtime WAV checker", output.ToString());
                Assert.Contains("Files loaded:  1", output.ToString());
                Assert.Contains("Files skipped: 1", output.ToString());
            }
        }

        [TestMethod]
        public void CheckFilesLoadsSignalScriptsWithConfigurationContext()
        {
            string script = CreateFile("sigscr.dat", "SCRIPT CHECKER\nEXTERN FLOAT STATE;\nSTATE = 0;\n");
            string config = CreateFile("sigcfg.dat", "SIMISA@@@@@@@@@@JINX0G0t______\n" +
                "LightTextures ( 0 ) LightsTab ( 0 ) SignalTypes ( 0 ) SignalShapes ( 0 ) ScriptFiles ( 1 ScriptFile ( sigscr.dat ) )");

            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { script, config }, false, AdditionType.Dependent, output, null, null);

                string report = output.ToString();
                Assert.Contains(config + ": OK", report);
                Assert.Contains(script + ": ", report);
                Assert.IsFalse(report.Contains("can not be loaded independently", StringComparison.Ordinal));
                Assert.IsLessThan(report.IndexOf(script, StringComparison.Ordinal), report.IndexOf(config, StringComparison.Ordinal));
                Assert.Contains("Files loaded:  2", report);
                Assert.Contains("Files skipped: 0", report);
                Assert.Contains("Files failed:  0", report);
            }
        }

        [TestMethod]
        public void CheckFilesReportsClassificationAndCallbackFailuresAndContinues()
        {
            string malformed = CreateFile("a.raw", "");
            string failingWave = CreateFile("failing.wav", "");
            string validWave = CreateFile("valid.wav", "");
            List<string> checkedFiles = new List<string>();

            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { malformed, failingWave, validWave }, false, AdditionType.Referenced, output, null, file =>
                    {
                        if (file == failingWave)
                            throw new InvalidDataException("WAV check failed");
                        checkedFiles.Add(file);
                    });

                Assert.AreSequenceEqual(new string[] { validWave }, checkedFiles.ToArray());
                string report = output.ToString();
                Assert.Contains(malformed, report);
                Assert.Contains("WAV check failed", report);
                Assert.Contains("Files loaded:  1", report);
                Assert.Contains("Files failed:  2", report);
                Assert.Contains("Errors:        2", report);
                Assert.IsFalse(report.Contains(failingWave + ": OK", StringComparison.Ordinal));
            }
        }

        [TestMethod]
        public void CheckFilesDoesNotDiscoverDependenciesAfterParserFailure()
        {
            string terrain = CreateFile("invalid.t", "not a terrain file");
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { terrain }, false, AdditionType.All, output, null, null);

                string report = output.ToString();
                Assert.Contains(terrain, report);
                Assert.Contains("Files loaded:  0", report);
                Assert.Contains("Files failed:  1", report);
                Assert.IsFalse(report.Contains("_y.raw", StringComparison.Ordinal));
            }
        }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(true, true)]
        public void CheckFilesPreservesHostListenersAndHonorsVerbosity(bool verbose, bool expectedInformation)
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
                    ContentChecker.CheckFiles(new string[] { wave }, verbose, AdditionType.None, output, null, file => Trace.TraceInformation("WAV inspection information"));

                    Assert.AreSequenceEqual(listeners, Trace.Listeners.Cast<TraceListener>().ToArray());
                    Assert.Contains("WAV inspection information", hostOutput.ToString());
                    Assert.AreEqual(expectedInformation, output.ToString().Contains("WAV inspection information", StringComparison.Ordinal));
                    output.Write("still open");
                    Assert.EndsWith("still open", output.ToString());
                }
                finally
                {
                    Trace.Listeners.Remove(hostListener);
                }
            }
        }

        [TestMethod]
        public void CheckFilesRestoresListenersWhenCancelled()
        {
            string wave = CreateFile("sound.wav", "");
            TraceListener[] listeners = Trace.Listeners.Cast<TraceListener>().ToArray();
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                Assert.ThrowsExactly<OperationCanceledException>(() => ContentChecker.CheckFiles(
                    new string[] { wave }, false, AdditionType.None, output, null, file => throw new OperationCanceledException()));

                Assert.AreSequenceEqual(listeners, Trace.Listeners.Cast<TraceListener>().ToArray());
                output.Write("still open");
                Assert.EndsWith("still open", output.ToString());
            }
        }

        [TestMethod]
        public void CheckFilesReportsMissingInputAndContinues()
        {
            string missing = Path.Combine(directory, "missing.wav");
            string existing = CreateFile("existing.wav", "");
            List<string> checkedFiles = new List<string>();
            using (StringWriter output = new StringWriter(CultureInfo.InvariantCulture))
            {
                ContentChecker.CheckFiles(new string[] { missing, existing }, false, AdditionType.None, output, null, checkedFiles.Add);

                Assert.AreSequenceEqual(new string[] { existing }, checkedFiles.ToArray());
                Assert.Contains(missing, output.ToString());
                Assert.Contains("Files loaded:  1", output.ToString());
                Assert.Contains("Files failed:  1", output.ToString());
                Assert.Contains("Errors:        1", output.ToString());
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