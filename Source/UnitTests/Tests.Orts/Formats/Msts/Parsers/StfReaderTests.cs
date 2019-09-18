using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Orts.Formats.Msts.Parsers;
using Tests.Orts.Shared;

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
    #endregion

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

    #region Common test utilities
    class StfTokenReaderCommon
    {
        //#region Value itself
        //public static void OnNoValueWarnAndReturnDefault<T, nullableT>
        //    (T niceDefault, T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = ")";
        //    T result = default(T);

        //    var reader = Create.Reader(inputString);
        //    AssertWarnings.Matching("Cannot parse|expecting.*found.*[)]", () => { result = codeDoingReading(reader, default(nullableT)); });
        //    Assert.Equal(niceDefault, result);
        //    Assert.Equal(")", reader.ReadItem());

        //    reader = Create.Reader(inputString);
        //    AssertWarnings.Matching("expecting.*found.*[)]", () => { result = codeDoingReading(reader, someDefault); });
        //    Assert.Equal(resultDefault, result);
        //    Assert.Equal(")", reader.ReadItem());
        //}

        //public static void ReturnValue<T>
        //    (T[] testValues, ReadValueCode<T> codeDoingReading)
        //{
        //    string[] inputValues = testValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
        //    ReturnValue<T>(testValues, inputValues, codeDoingReading);
        //}

        //public static void ReturnValue<T>
        //    (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string inputString = String.Join(" ", inputValues);
        //    var reader = Create.Reader(inputString);
        //    foreach (T testValue in testValues)
        //    {
        //        T result = codeDoingReading(reader);
        //        Assert.Equal(testValue, result);
        //    }
        //}

        //public static void WithCommaReturnValue<T>
        //    (T[] testValues, ReadValueCode<T> codeDoingReading)
        //{
        //    string[] inputValues = testValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
        //    WithCommaReturnValue(testValues, inputValues, codeDoingReading);
        //}

        //public static void WithCommaReturnValue<T>
        //    (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string inputString = String.Join(" ", inputValues);
        //    var reader = Create.Reader(inputString);
        //    foreach (T testValue in testValues)
        //    {
        //        T result = codeDoingReading(reader);
        //        Assert.Equal(testValue, result);
        //    }
        //}

        //public static void ForEmptyStringReturnNiceDefault<T, nullableT>
        //    (T niceDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string[] tokenValues = new string[] { "", "" };
        //    string emptyQuotedString = "\"\"";
        //    string inputString = emptyQuotedString + " " + emptyQuotedString;
        //    var reader = Create.Reader(inputString);

        //    T result = codeDoingReading(reader, default(nullableT));
        //    Assert.Equal(niceDefault, result);

        //    result = codeDoingReading(reader, someDefault);
        //    Assert.Equal(niceDefault, result);
        //}

        //public static void ForNonNumbersReturnNiceDefaultAndWarn<T, nullableT>
        //    (T niceDefault, T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string[] testValues = new string[] { "noint", "sometoken", "(" };
        //    string inputString = String.Join(" ", testValues);

        //    var reader = Create.Reader(inputString);
        //    foreach (string testValue in testValues)
        //    {
        //        T result = default(T);
        //        AssertWarnings.Matching("Cannot parse", () => { result = codeDoingReading(reader, default(nullableT)); });
        //        Assert.Equal(niceDefault, result);
        //    }

        //    reader = Create.Reader(inputString);
        //    foreach (string testValue in testValues)
        //    {
        //        T result = default(T);
        //        AssertWarnings.Matching("Cannot parse", () => { result = codeDoingReading(reader, someDefault); });
        //        Assert.Equal(resultDefault, result);

        //    }
        //}
        //#endregion

        #region Value in blocks
        public static void OnEofWarnAndReturnDefault<T, nullableT>
            (T resultDefault, ref nullableT someValue, ReadValueCodeByRef<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader(string.Empty);
//            AssertWarnings.Matching("Unexpected end of file", () => codeDoingReading(reader, ref someValue));
            //            AssertWarnings.Matching("Unexpected end of file", () => codeDoingReading(reader, ref someValue) );
            //Assert.Equal(default(T), result);
            //AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, ref someDefault); });
            Assert.AreEqual(resultDefault, someValue);
        }

        //public static void OnEofWarnAndReturnDefault<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = "";
        //    var reader = Create.Reader(inputString);
        //    T result = default(T);
        //    AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, default(nullableT)); });
        //    Assert.Equal(default(T), result);
        //    AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, someDefault); });
        //    Assert.Equal(resultDefault, result);
        //}

        //public static void ForNoOpenWarnAndReturnDefault<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = "noblock";
        //    T result = default(T);

        //    var reader = Create.Reader(inputString);
        //    AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, default(nullableT)); });
        //    Assert.Equal(default(T), result);

        //    reader = Create.Reader(inputString);
        //    AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, someDefault); });
        //    Assert.Equal(resultDefault, result);
        //}

        //public static void OnBlockEndReturnDefaultOrWarn<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    OnBlockEndReturnNiceDefaultAndWarn(resultDefault, someDefault, codeDoingReading);
        //    OnBlockEndReturnGivenDefault(resultDefault, someDefault, codeDoingReading);
        //}

        //public static void OnBlockEndReturnNiceDefaultAndWarn<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = ")";
        //    T result = default(T);

        //    var reader = Create.Reader(inputString);
        //    AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, default(nullableT)); });
        //    Assert.Equal(default(T), result);
        //}

        //public static void OnBlockEndReturnGivenDefault<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = ")";
        //    T result = default(T);

        //    var reader = Create.Reader(inputString);
        //    result = codeDoingReading(reader, someDefault);
        //    Assert.Equal(resultDefault, result);
        //    Assert.Equal(")", reader.ReadItem());
        //}

        //public static void OnEmptyBlockEndReturnGivenDefault<T, nullableT>
        //    (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    var inputString = "()a";
        //    T result = default(T);

        //    var reader = Create.Reader(inputString);
        //    result = codeDoingReading(reader, someDefault);
        //    Assert.Equal(resultDefault, result);
        //    Assert.Equal("a", reader.ReadItem());
        //}

        //public static void ReturnValueInBlock<T>
        //    (T[] testValues, ReadValueCode<T> codeDoingReading)
        //{
        //    string[] inputValues = testValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
        //    ReturnValueInBlock<T>(testValues, inputValues, codeDoingReading);
        //}

        //public static void ReturnValueInBlock<T>
        //    (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string[] tokenValues = inputValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "({0})", value)).ToArray();
        //    string inputString = String.Join(" ", tokenValues);
        //    var reader = Create.Reader(inputString);

        //    foreach (T testValue in testValues)
        //    {
        //        T result = codeDoingReading(reader);
        //        Assert.Equal(testValue, result);
        //    }
        //}

        //public static void ReturnValueInBlockAndSkipRestOfBlock<T>
        //    (T[] testValues, ReadValueCode<T> codeDoingReading)
        //{
        //    string[] inputValues = testValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
        //    ReturnValueInBlockAndSkipRestOfBlock<T>(testValues, inputValues, codeDoingReading);
        //}

        //public static void ReturnValueInBlockAndSkipRestOfBlock<T>
        //    (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        //{
        //    AssertWarnings.NotExpected();
        //    string[] tokenValues = inputValues.Select(
        //        value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "({0} dummy_token(nested_token))", value)
        //        ).ToArray();
        //    string inputString = String.Join(" ", tokenValues);
        //    var reader = Create.Reader(inputString);

        //    foreach (T testValue in testValues)
        //    {
        //        T result = codeDoingReading(reader);
        //        Assert.Equal(testValue, result);
        //    }
        //}

        #endregion

        public delegate T ReadValueCode<T, nullableT>(STFReader reader, nullableT defaultValue);
        public delegate T ReadValueCode<T>(STFReader reader);
        public delegate void ReadValueCodeByRef<T, nullableT>(STFReader reader, ref nullableT defaultResult);

    }
    #endregion

    #region Vector2
    [TestClass]
    public class ReadingVector2BlockTests
    {
        static readonly Vector2 SOMEDEFAULT = new Vector2(1.1f, 1.2f);
        static readonly Vector2[] SOMEDEFAULTS = new Vector2[] { new Vector2(1.3f, 1.5f), new Vector2(-2f, 1e6f) };
        static readonly string[] STRINGDEFAULTS = new string[] { "1.3 1.5 ignore", "-2 1000000" };

        [TestMethod]
        public void OnEofWarnAndReturnDefault()
        {
            Vector2 defaultResult = SOMEDEFAULT;
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<Vector2, Vector2>
                (SOMEDEFAULT, ref defaultResult, (STFReader reader, ref Vector2 x) => reader.ReadVector2Block(STFReader.Units.None, ref x));
        }

        //[Fact]
        //public static void ForNoOpenReturnDefaultAndWarn()
        //{
        //    StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<Vector2, Vector2>
        //        (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector2Block(STFReader.Units.None, ref x));
        //}

        //[Fact]
        //public static void OnBlockEndReturnDefaultWhenGiven()
        //{
        //    StfTokenReaderCommon.OnBlockEndReturnGivenDefault<Vector2, Vector2>
        //        (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector2Block(STFReader.Units.None, ref x));
        //}

        //[Fact]
        //public static void ReturnValueInBlock()
        //{
        //    Vector2 zero = Vector2.Zero;
        //    StfTokenReaderCommon.ReturnValueInBlock<Vector2>
        //        (SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadVector2Block(STFReader.Units.None, ref zero));
        //}

        //[Fact]
        //public static void ReturnValueInBlockAndSkipRestOfBlock()
        //{
        //    Vector2 zero = Vector2.Zero;
        //    StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<Vector2>
        //        (SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadVector2Block(STFReader.Units.None, ref zero));
        //}
    }
    #endregion

}
