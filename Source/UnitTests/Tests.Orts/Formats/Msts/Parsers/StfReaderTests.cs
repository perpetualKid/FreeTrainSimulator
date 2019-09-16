using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Orts.Formats.Msts.Parsers;

namespace Tests.Orts.Formats.Msts.Parsers
{
    [TestClass]
    public class StfReaderTests
    {
        [TestMethod]
        public void TestMethod1()
        {
        }
    }

    [TestClass]
    public class StfExceptionTests
    {
        [TestMethod]
        public void ConstructorTest()
        {
            var reader = Create.Reader("sometoken");
            //Assert there is no exception thrown
        }

        /// <summary>
        /// Test that we can correctly assert a throw of an exception
        /// </summary>
        [TestMethod]
        public void BeThrowable()
        {
            string filename = "somefile";
            string message = "some message";
            AssertStfException.Throws(new STFException(new STFReader(new MemoryStream(), filename, Encoding.ASCII, true), message), message);
        }

#if NEW_READER
        /// <summary>
        /// Test constructors of the exception class
        /// </summary>
        [Fact]
        public static void BeConstructable()
        {
            Assert.DoesNotThrow(() => new STFException("filename", 1, "some message"));
        }

        /// <summary>
        /// Test that we can correctly assert a throw of an exception
        /// </summary>
        [Fact]
        public static void BeThrowable()
        {
            string filename = "somefile";
            int lineNumber = 2;
            string message = "some message";
            Tests.Msts.Parsers.StfReader.AssertStfException.Throws(() => { throw new STFException(filename, lineNumber, message); }, message);
        }
#endif

    }

    #region Test utilities
    class Create
    {
        public static STFReader Reader(string source)
        {
            return Reader(source, "some.stf", false);
        }

        public static STFReader Reader(string source, string fileName)
        {
            return Reader(source, fileName, false);
        }

        public static STFReader Reader(string source, bool useTree)
        {
            return Reader(source, "some.stf", useTree);
        }

        public static STFReader Reader(string source, string fileName, bool useTree)
        {
            return new STFReader(new MemoryStream(Encoding.ASCII.GetBytes(source)), fileName, Encoding.ASCII, useTree);
        }

#if NEW_READER
        public static STFReader Reader(string source, MSTS.Parsers.IStreamReaderFactory factory)
        {
            return Reader(source, "some.stf", factory);
        }

        public static STFReader Reader(string source, string fileName, MSTS.Parsers.IStreamReaderFactory factory)
        {
            STFReader.NextStreamReaderFactory = factory;
            return Create.Reader(source, fileName, false);
        }
#endif
    }

    struct TokenTester
    {
        public string InputString;
        public string[] ExpectedTokens;
        public int[] ExpectedLineNumbers;

        public TokenTester(string input, string[] output)
        {
            InputString = input;
            ExpectedTokens = output;
            ExpectedLineNumbers = new int[0];
        }

        public TokenTester(string input, int[] lineNumbers)
        {
            InputString = input;
            ExpectedTokens = new string[0];
            ExpectedLineNumbers = lineNumbers;
        }
    }

    /// <summary>
    /// Class to help assert not only the type of exception being thrown, but also the message being generated
    /// </summary>
    static class AssertStfException
    {
        /// <summary>
        /// Run the testcode, make sure an exception is called and test the exception
        /// </summary>
        /// <param name="testCode">Code that will be executed</param>
        /// <param name="pattern">The pattern that the exception message should match</param>
        public static void Throws(STFException exception, string pattern)
        {
            Assert.IsNotNull(exception);
            Assert.IsTrue(Regex.IsMatch(exception.Message, pattern), exception.Message + " does not match pattern: " + pattern);
        }
    }

#if NEW_READER
    class StreamReaderStore : IStreamReaderFactory
    {
        public StreamReaderStore()
        {
            storedStreamReaders = new Dictionary<string, StreamReader>();
            storedSimisSignatures = new Dictionary<string, string>();
            ShouldReadSimisSignature = false;
        }

        public void AddReaderFromMemory(string name, string source)
        {
            var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(source));
            var reader = new StreamReader(memoryStream, Encoding.ASCII);
            storedStreamReaders[name] = reader;
            storedSimisSignatures[name] = ShouldReadSimisSignature ? reader.ReadLine() : null;
        }

        public StreamReader GetStreamReader(string name, out string simisSignature)
        {
            Assert.True(storedStreamReaders.ContainsKey(name), "requested file:" + name + "is not available");
            simisSignature = storedSimisSignatures[name];
            return storedStreamReaders[name];
        }

        Dictionary<string, StreamReader> storedStreamReaders;
        Dictionary<string, string> storedSimisSignatures;

        public bool ShouldReadSimisSignature { get; set; }
    }
#endif
    #endregion

}
