using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;
using Orts.Formats.Msts.Parsers;
using Tests.Orts.Shared;

namespace Tests.Orts.Formats.Msts.Parsers
{
    // General note on 'using new STFreader'
    // In production code we want to use something like
    // using (var reader = new STFReader()) {
    //    ...
    // } 
    // This makes sure the reader is disposed of correctly.
    // During testing, however, this has an unwanted side-effect. If a unit tests fails, and not everything has been read
    // the rest of the code in the test is not executed. But the reader.Dispose will be called anyway at the end of the using block.
    // reader.Dispose, however, will give a warning when it is called when the reader is not yet at the end of the file. This warning will 
    // subsequently be catched, and give an failed assert. It is this failed assert that turns up in the test runner then, and not the initial failed assert
    // This makes that the cause of the fail is not clear from the output window.
    // Obviously, it is possible to refactor the tests such that the the asserts are either done after the dispose, 
    // or the asserts are done when all reading is done anyway (possibly by really having only one assert). 
    // Since most unit tests do not actually open a file, this is no big issue.

    [TestClass]
    public class StfReaderTests
    {
        #region constructor/destructor
        /// <summary>
        /// Test constructor
        /// </summary>
        [TestMethod]
        public void ConstructableFromStreamTest()
        {
            AssertWarnings.NotExpected();
            new STFReader(new MemoryStream(Encoding.ASCII.GetBytes("")), "emptyFile.stf", Encoding.ASCII, true);
        }

        /// <summary>
        /// Test that Dispose is implemented (but not that it is fully functional)
        /// </summary>
        [TestMethod]
        public void DisposableTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("");
            reader.Dispose();
        }

        [TestMethod]
        public void DisposeBeforeEOFWarnTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("lasttoken");
            AssertWarnings.Matching("Expected.*end", () => reader.Dispose());
        }

        [TestMethod]
        public void ThrowInConstructorOnMissingFileTest()
        {
            Assert.ThrowsException<FileNotFoundException>(() => new STFReader("somenonexistingfile", false));
        }

        [TestMethod]
        public void StreamConstructorHasNullSimisSignatureTest()
        {
            AssertWarnings.NotExpected();
            string firstToken = "firsttoken";
            var reader = Create.Reader(firstToken);
            Assert.IsNull(null, reader.SimisSignature);
        }
        #endregion

        #region Tree
        [TestMethod]
        public void CallingTreeThrowSomethingTest()
        {
            AssertWarnings.Expected(); // there might be a debug.assert
            var reader = Create.Reader("wagon(Lights");
            reader.ReadItem();
            Assert.ThrowsException<AssertFailedException>(() => { var dummy = reader.Tree; });
        }

        [TestMethod]
        public void BuildTreeStringTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("wagon(Lights)engine", true);

            reader.ReadItem();
            Assert.AreEqual("wagon", reader.Tree.ToLower());
            reader.ReadItem();
            reader.ReadItem();
            Assert.AreEqual("wagon(lights", reader.Tree.ToLower());
            reader.ReadItem();
            reader.ReadItem();
            Assert.AreEqual("engine", reader.Tree.ToLower());
        }
        #endregion

        #region Comments/skip
        [TestClass]
        public class PreprocessingTests
        {
            [TestMethod]
            public void SkipBlockOnCommentTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("comment(a)" + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void SkipBlockOnCommentOtherCaseTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("Comment(a)" + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void WarnOnMissingBlockAfterCommentTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("comment a " + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void SkipBlockOnSkipTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("skip(a)" + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void SkipBlockOnSkipOtherCaseTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("Skip(a)" + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void WarnOnMissingBlockAfterSkipTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("skip a " + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void SkipBlockOnTokenStartingWithHashTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("#token(a)" + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void SkipSingleItemOnTokenStartingWithHashTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("#token a " + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void WarnOnEofAfterHashTokenTest()
            {
                AssertWarnings.NotExpected();
                AssertWarnings.Matching("a # marker.*EOF", () =>
                {
                    var reader = Create.Reader("#sometoken");
                    reader.ReadItem();
                });
            }

            [TestMethod]
            public void SkipBlockOnTokenStartingWithUnderscoreTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("_token(a)" + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void SkipSingleItemOnTokenStartingWithUnderscoreTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("_token a " + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void SkipBlockDisregardsNestedCommentTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("comment(a comment( c) skip _underscore #hash)" + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }

            [TestMethod]
            public void SkipBlockDisregardsNestedIncludeTest()
            {
                AssertWarnings.NotExpected();
                string someFollowingToken = "b";
                var reader = Create.Reader("comment(a include c )" + someFollowingToken);
                Assert.AreEqual(someFollowingToken, reader.ReadItem());
            }
        }
        #endregion

        [TestMethod]
        public void ContainClosingBracketTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("wagon(Lights())engine", true);

            reader.ReadItem(); // 'wagon'
            reader.ReadItem(); // '('
            reader.ReadItem(); // 'Lights'
            reader.ReadItem(); // '('
            reader.ReadItem(); // ')'
            Assert.AreEqual("wagon()", reader.Tree.ToLower());
            reader.ReadItem(); // ')'
            Assert.AreEqual(")", reader.Tree.ToLower());
            reader.ReadItem(); // ')'
            Assert.AreEqual("engine", reader.Tree.ToLower());
        }

        [TestMethod]
        public void PeekPastWhiteSpaceTest()
        {   //testing only what is really needed right now.
            var reader = Create.Reader("a  )  ");
            Assert.IsFalse(')' == reader.PeekPastWhitespace());
            Assert.IsFalse(-1 == reader.PeekPastWhitespace());

            reader.ReadItem();
            Assert.AreEqual(')', reader.PeekPastWhitespace());
            reader.ReadItem();
            Assert.AreEqual(-1, reader.PeekPastWhitespace());
        }

        #region Tokenizer
        [TestMethod]
        public void ReadSingleItemTest()
        {
            AssertWarnings.NotExpected();
            string item = "sometoken";
            var reader = Create.Reader(item);
            Assert.AreEqual(item, reader.ReadItem());
        }

        [TestMethod]
        public void ReadSingleItemsTest()
        {
            AssertWarnings.NotExpected();
            var singleItems = new string[] { "a", "b", "c", "(", ")", "aa" };
            foreach (string item in singleItems)
            {
                var reader = Create.Reader(item);
                Assert.AreEqual(item, reader.ReadItem());
            }
        }

        [TestMethod]
        public void WhiteSpaceSeparateTokensTest()
        {
            AssertWarnings.NotExpected();
            var tokenTesters = new List<TokenTester>
            {
                new TokenTester("a b", new string[] { "a", "b" }),
                new TokenTester("a   b", new string[] { "a", "b" }),
                new TokenTester("a\nb", new string[] { "a", "b" }),
                new TokenTester("a \n\t b", new string[] { "a", "b" }),
                new TokenTester("aa bb", new string[] { "aa", "bb" }),
                new TokenTester("aa b\nc", new string[] { "aa", "b", "c" })
            };

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.InputString);
                foreach (string expectedToken in tokenTester.ExpectedTokens)
                {
                    Assert.AreEqual(expectedToken, reader.ReadItem());
                }
            }
        }

        [TestMethod]
        public void RecognizeSpecialCharsTest()
        {
            AssertWarnings.NotExpected();
            List<TokenTester> tokenTesters = new List<TokenTester>
            {
                new TokenTester("(a", new string[] { "(", "a" }),
                new TokenTester(")a", new string[] { ")", "a" }),
                new TokenTester("a(", new string[] { "a", "(" }),
                new TokenTester("aa ( (", new string[] { "aa", "(", "(" }),
                new TokenTester("(\ncc\n(", new string[] { "(", "cc", "(" })
            };

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.InputString);
                foreach (string expectedToken in tokenTester.ExpectedTokens)
                {
                    Assert.AreEqual(expectedToken, reader.ReadItem());
                }
            }
        }

        [TestMethod]
        public void RecognizeLiteralStringsTest()
        {
            AssertWarnings.NotExpected();
            List<TokenTester> tokenTesters = new List<TokenTester>
            {
                new TokenTester("\"a\"", new string[] { "a" }),
                new TokenTester("\"aa\"", new string[] { "aa" }),
                new TokenTester("\"a a\"", new string[] { "a a" }),
                new TokenTester("\"a a\"b", new string[] { "a a", "b" }),
                new TokenTester("\"a a\" b", new string[] { "a a", "b" }),
                new TokenTester("\"a a\"\nb", new string[] { "a a", "b" }),
                new TokenTester("\"a\na\"\nb", new string[] { "a\na", "b" }),
                new TokenTester("\"a\ta\"\nb", new string[] { "a\ta", "b" }),
                new TokenTester("\"\\\"\"b", new string[] { "\"", "b" })
            };

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.InputString);
                foreach (string expectedToken in tokenTester.ExpectedTokens)
                {
                    Assert.AreEqual(expectedToken, reader.ReadItem());
                }
            }
        }

        [TestMethod]
        public void RecognizeEscapeCharInLiteralStringsTest()
        {
            AssertWarnings.NotExpected();
            List<TokenTester> tokenTesters = new List<TokenTester>
            {
                new TokenTester(@"""a\na"" b", new string[] { "a\na", "b" }),
                new TokenTester(@"""c\tc"" d", new string[] { "c\tc", "d" })
            };

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.InputString);
                foreach (string expectedToken in tokenTester.ExpectedTokens)
                {
                    Assert.AreEqual(expectedToken, reader.ReadItem());
                }
            }
        }

        [TestMethod]
        public void IncompleteLiteralStringAtEOFWarnAndGiveResultTest()
        {
            AssertWarnings.NotExpected();
            string tokenToBeRead = "sometoken";
            string inputString = "\"" + tokenToBeRead;
            string returnedItem = "needadefault";
            AssertWarnings.Matching("unexpected.*EOF.*started.*double-quote", () =>
            {
                var reader = Create.Reader(inputString);
                returnedItem = reader.ReadItem();
            });
            Assert.AreEqual(tokenToBeRead, returnedItem);
        }

        [TestMethod]
        public void AllowTrailingDoubleQuoteTest()
        {   // This fixes bug 1197917, even though it is a workaround for bad files
            AssertWarnings.NotExpected();
            string tokenToBeRead = "sometoken";
            string followingToken = "following";
            string inputString = String.Format(" {0}\" {1}", tokenToBeRead, followingToken);
            var reader = Create.Reader(inputString);
            Assert.AreEqual(tokenToBeRead, reader.ReadItem());
            Assert.AreEqual(followingToken, reader.ReadItem());
        }

        [TestMethod]
        public void FinalWhiteSpaceBeAtEOFTest()
        {
            AssertWarnings.NotExpected();
            var inputStrings = new string[] { " " }; //, "\n  \n\t" };
            foreach (string inputString in inputStrings)
            {
                var reader = Create.Reader("sometoken" + inputString);
                {
                    reader.ReadItem();
                    Assert.IsTrue(reader.Eof);
                }
            }
        }

        [TestMethod]
        public void EOFKeepBeingAtEOFTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("lasttoken");
            reader.ReadItem();
            reader.ReadItem();
            Assert.IsTrue(reader.Eof);
        }

        [TestMethod]
        public void EOFKeepReturningEmptyStringTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("lasttoken");
            reader.ReadItem();
            Assert.AreEqual(string.Empty, reader.ReadItem());
            Assert.AreEqual(string.Empty, reader.ReadItem());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Old STFReader has a different way of reading lineNumbers and will fail here</remarks>
        [TestMethod]
        public void StoreSourceLineNumberOfLastReadTokenTest()
        {
            List<TokenTester> tokenTesters = new List<TokenTester>
            {
                new TokenTester("a b", new int[] { 1, 1 }),
                new TokenTester("a\nb", new int[] { 2, 2 }),
                //new TokenTester("a\nb", new int[] { 1, 2 }),
                //new TokenTester("a\nb\nc", new int[] { 1, 2, 3 }),
                //new TokenTester("a b\n\nc", new int[] { 1, 1, 3 }),
                //new TokenTester("a(b(\nc)\nc)", new int[] { 1, 1, 1, 1, 2, 2, 3, 3 }),
            };

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.InputString);
                foreach (int expectedLineNumber in tokenTester.ExpectedLineNumbers)
                {
                    reader.ReadItem();
                    Assert.AreEqual(expectedLineNumber, reader.LineNumber);
                }
            }
        }

        [TestMethod]
        public void StoreLastSourceLineNumberTest()
        {
            AssertWarnings.NotExpected();
            List<TokenTester> tokenTesters = new List<TokenTester>
            {
                new TokenTester("a b", new int[2] { 1, 1 }),
                new TokenTester("a\nb", new int[2] { 1, 2 }),
                new TokenTester("a\nb\nc", new int[3] { 1, 2, 3 }),
                new TokenTester("a b\n\nc", new int[3] { 1, 1, 3 }),
                new TokenTester("a(b(\nc)\nc)", new int[8] { 1, 1, 1, 1, 2, 2, 3, 3 })
            };

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.InputString);
                foreach (int expectedLineNumber in tokenTester.ExpectedLineNumbers)
                {
                    reader.ReadItem();
                }
                int lastLineNumber = tokenTester.ExpectedLineNumbers[tokenTester.ExpectedLineNumbers.Length - 1];
                Assert.AreEqual(lastLineNumber, reader.LineNumber);
                reader.ReadItem();
                Assert.AreEqual(lastLineNumber, reader.LineNumber);
            }
        }

        [TestMethod]
        public void StoreFileNameTest()
        {
            AssertWarnings.NotExpected();
            string[] fileNames = new string[] { "test1", "otherfile.stf" };
            string someThreeItemInput = "a b c";
            foreach (string fileName in fileNames)
            {
                var reader = Create.Reader(someThreeItemInput, fileName);
                reader.ReadItem();
                Assert.AreEqual(fileName, reader.FileName);
                reader.ReadItem();
                Assert.AreEqual(fileName, reader.FileName);
                reader.ReadItem();
                Assert.AreEqual(fileName, reader.FileName);
            }
        }

        #region Concatenation
        [TestMethod]
        public void ConcatenateTwoLiteralTokensTest()
        {
            AssertWarnings.NotExpected();
            string inputString = "\"a\" + \"b\"";
            var reader = Create.Reader(inputString);
            Assert.AreEqual("ab", reader.ReadItem());
        }

        [TestMethod]
        public void NotConcatenateAfterNormalTokenTest()
        {
            AssertWarnings.NotExpected();
            string inputString = "a + b";
            var reader = Create.Reader(inputString);
            Assert.AreEqual("a", reader.ReadItem());
        }

        [TestMethod]
        public void ConcatenateThreeLiteralTokens()
        {
            AssertWarnings.NotExpected();
            string inputString = "\"a\" + \"b\" + \"c\"";
            var reader = Create.Reader(inputString);
            Assert.AreEqual("abc", reader.ReadItem());
        }

        [TestMethod]
        public void WarnOnNormalTokenAfterConcatenation()
        {
            AssertWarnings.NotExpected();
            string result = String.Empty;
            string inputString = "\"a\" + b";

            AssertWarnings.Matching("started.*double.*quote.*next.*must", () =>
            {
                var reader = Create.Reader(inputString);
                result = reader.ReadItem();
            });
            Assert.AreEqual("a", result);
        }
        #endregion

        #endregion

        #region Block handling
        [TestMethod]
        public void SkipRestOfBlockAtBlockCloseTest()
        {
            AssertWarnings.NotExpected();
            string someTokenAfterBlock = "a";
            var reader = Create.Reader(")" + someTokenAfterBlock);
            reader.SkipRestOfBlock();
            Assert.AreEqual(someTokenAfterBlock, reader.ReadItem());
        }

        [TestMethod]
        public void SkipRestOfBlockBeforeBlockCloseTest()
        {
            AssertWarnings.NotExpected();
            string someTokenAfterBlock = "a";
            var reader = Create.Reader("b)" + someTokenAfterBlock);
            reader.SkipRestOfBlock();
            Assert.AreEqual(someTokenAfterBlock, reader.ReadItem());
        }

        [TestMethod]
        public void SkipRestOfBlockForNestedblocksTest()
        {
            AssertWarnings.NotExpected();
            string someTokenAfterBlock = "a";
            var reader = Create.Reader("b(c))" + someTokenAfterBlock);
            reader.SkipRestOfBlock();
            Assert.AreEqual(someTokenAfterBlock, reader.ReadItem());
        }

        [TestMethod]
        public void OnInclompleteRestOfBlockJustReturnTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("(b)");
            reader.SkipRestOfBlock();
            Assert.IsTrue(reader.Eof);
        }

        [TestMethod]
        public void SkipBlockTest()
        {
            string someTokenAfterBlock = "a";
            var reader = Create.Reader("(c)" + someTokenAfterBlock);
            reader.SkipBlock();
            Assert.AreEqual(someTokenAfterBlock, reader.ReadItem());
        }

        [TestMethod]
        public void SkipNestedBlockTest()
        {
            string someTokenAfterBlock = "a";
            var reader = Create.Reader("(c(b)d)" + someTokenAfterBlock);
            reader.SkipBlock();
            Assert.AreEqual(someTokenAfterBlock, reader.ReadItem());
        }

        [TestMethod]
        public void SkipBlockGettingImmediateCloseWarningTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader(")");
            AssertWarnings.Matching("Found a close.*expected block of data", () => reader.SkipBlock());
            Assert.AreEqual(")", reader.ReadItem());
        }

        [TestMethod]
        public void SkipBlockNotStartingWithOpenThrowTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("a");
            Assert.ThrowsException<STFException>(() => reader.SkipBlock(), "expected an open block");
        }

        [TestMethod]
        public void IncompleteSkipBlockJustReturnTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("(a");
            reader.SkipBlock();
            Assert.IsTrue(reader.Eof);
        }

        [TestMethod]
        public void NotAtEndOfBlockAfterNormalTokenTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("sometoken sometoken2");
            Assert.IsFalse(reader.EndOfBlock());
            reader.ReadItem();
            Assert.IsFalse(reader.EndOfBlock());
        }

        [TestMethod]
        public void AtEndOfBlockAtEOFTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("");
            Assert.IsTrue(reader.EndOfBlock());
        }

        [TestMethod]
        public void AtEndOfBlockAtCloseTest()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader(") nexttoken");
            Assert.IsTrue(reader.EndOfBlock());
        }

        [TestMethod]
        public void EndOfBlockConsumeCloseMarkerTest()
        {
            AssertWarnings.NotExpected();
            string followuptoken = "sometoken";
            var reader = Create.Reader(")" + followuptoken + " " + followuptoken); // should be at EOF

            Assert.IsTrue(reader.EndOfBlock());
            Assert.AreEqual(followuptoken, reader.ReadItem());
            Assert.IsFalse(reader.EndOfBlock());
        }

        #endregion

        #region MustMatch
        [TestMethod]
        public void MatchSimpleStringsTest()
        {
            AssertWarnings.NotExpected();
            string matchingToken = "a";
            string someTokenAfterMatch = "b";

            var reader = Create.Reader(matchingToken + " " + someTokenAfterMatch);
            reader.MustMatch(matchingToken);
            Assert.AreEqual(someTokenAfterMatch, reader.ReadItem());
        }

        [TestMethod]
        public void MatchOpenBracketTest()
        {
            AssertWarnings.NotExpected();
            string matchingToken = "(";
            string someTokenAfterMatch = "b";

            var reader = Create.Reader(matchingToken + someTokenAfterMatch);
            reader.MustMatch(matchingToken);
            Assert.AreEqual(someTokenAfterMatch, reader.ReadItem());
        }

        [TestMethod]
        public void MatchCloseBracketTest()
        {
            AssertWarnings.NotExpected();
            string matchingToken = ")";
            string someTokenAfterMatch = "b";

            var reader = Create.Reader(matchingToken + someTokenAfterMatch);
            reader.MustMatch(matchingToken);
            Assert.AreEqual(someTokenAfterMatch, reader.ReadItem());
        }

        [TestMethod]
        public void MatchWhileIgnoringCaseTest()
        {
            AssertWarnings.NotExpected();
            string matchingToken = "somecase";
            string matchingTokenOtherCase = "SomeCase";
            string someTokenAfterMatch = "b";

            var reader = Create.Reader(matchingTokenOtherCase + " " + someTokenAfterMatch);
            reader.MustMatch(matchingToken);
            Assert.AreEqual(someTokenAfterMatch, reader.ReadItem());
        }

        [TestMethod]
        public void WarnOnSingleMissingMatchTest()
        {
            AssertWarnings.NotExpected();
            string tokenToMatch = "a";
            string someotherToken = "b";
            var reader = Create.Reader(someotherToken + " " + tokenToMatch);
            AssertWarnings.Matching("not found.*instead", () => reader.MustMatch(tokenToMatch));
        }

        [TestMethod]
        public void ThrowOnDoubleMissingMatchTest()
        {
            AssertWarnings.Expected();  // we first expect a warning, only after that an error
            string tokenToMatch = "a";
            string someotherToken = "b";
            var reader = Create.Reader(someotherToken + " " + someotherToken);
            Assert.ThrowsException<STFException>(() => reader.MustMatch(tokenToMatch), "not found.*instead");
        }

        [TestMethod]
        public void WarnOnEofDuringMatchTest()
        {
            AssertWarnings.NotExpected();
            string tokenToMatch = "a";
            var reader = Create.Reader("");
            AssertWarnings.Matching("Unexpected end of file instead", () => reader.MustMatch(tokenToMatch));
        }
        #endregion
    }

    #region TokenProcessor and Parseblock/File
    [TestClass]
    public class TokenProcessingTests
    {
        [TestMethod]
        public void DefineTokenProcessorTest()
        {
            string sometoken = "sometoken";
            int called = 0;
            var tokenProcessor = new STFReader.TokenProcessor(sometoken, () => { called++; });
            Assert.AreEqual(sometoken, tokenProcessor.Token);
            tokenProcessor.Processor.Invoke();
            Assert.AreEqual(1, called);
        }

        [TestMethod]
        public void ParseABlockTest()
        {
            string source = "block1 block2()block1()";
            var reader = Create.Reader(source);
            int called1 = 0;
            int called2 = 0;
            reader.ParseBlock(new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
                    new STFReader.TokenProcessor("block2", () => { called2++; })
                });
            Assert.AreEqual(2, called1);
            Assert.AreEqual(1, called2);
        }

        [TestMethod]
        public void ParseABlockWithBreakOutTest()
        {
            string source = "block1 block2()block1()";
            var reader = Create.Reader(source);
            int called1 = 0;
            int called2 = 0;
            reader.ParseBlock(() => called2 == 1, new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
                    new STFReader.TokenProcessor("block2", () => { called2++; })
                });
            Assert.AreEqual(1, called1);
            Assert.AreEqual(1, called2);
        }

        [TestMethod]
        public void ParseABlockAndThenContinueTest()
        {
            string followingtoken = "sometoken";
            string source = "block1())" + followingtoken;
            var reader = Create.Reader(source);
            int called1 = 0;
            reader.ParseBlock(new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
            });
            Assert.AreEqual(followingtoken, reader.ReadItem());
        }

        [TestMethod]
        public void ParseAFileTest()
        {
            string source = "block1 block2()block1()";
            var reader = Create.Reader(source);
            int called1 = 0;
            int called2 = 0;
            reader.ParseFile(new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
                    new STFReader.TokenProcessor("block2", () => { called2++; })
                });
            Assert.AreEqual(2, called1);
            Assert.AreEqual(1, called2);
        }

        [TestMethod]
        public void ParseAFileWithBreakOutTest()
        {
            string source = "block1 block2()block1()";
            var reader = Create.Reader(source);
            int called1 = 0;
            int called2 = 0;
            reader.ParseFile(() => called2 == 1, new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
                    new STFReader.TokenProcessor("block2", () => { called2++; })
                });
            Assert.AreEqual(1, called1);
            Assert.AreEqual(1, called2);
        }

        [TestMethod]
        public void ParseAFileTillEOFTest()
        {
            string followingtoken = "block2";
            string source = "block1())" + followingtoken;
            var reader = Create.Reader(source);
            int called1 = 0;
            int called2 = 0;
            reader.ParseFile(new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
                    new STFReader.TokenProcessor("block2", () => { called2++; })
            });
            Assert.AreEqual(1, called2);
            Assert.IsTrue(reader.Eof);
        }
    }
    #endregion

    [TestClass]
    public class StfReaderBlockTests
    {
        #region bool
        // ReadBool is not supported.
        [TestClass]
        public class ReadBoolBlockTests
        {
            static readonly bool SOMEDEFAULT = false;
            static readonly bool[] SOMEDEFAULTS1 = new bool[] { false, true, false, true, true, true, true };
            static readonly string[] STRINGDEFAULTS1 = new string[] { "false", "true", "0", "1", "1.1", "-2.9e3", "non" };

            [TestMethod]
            public void EofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault<bool, bool>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadBoolBlock(x));
            }

            [TestMethod]
            public void NoOpenReturnDefaultAndWarnTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<bool, bool>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadBoolBlock(x));
            }

            [TestMethod]
            public void OnBlockEndReturnGivenDefaultTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnGivenDefault<bool, bool>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadBoolBlock(x));
            }

            [TestMethod]
            public void ReturnStringValueInBlockTest()
            {
                string[] inputValues = { "true", "false" };
                bool[] expectedValues = { true, false };
                StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(false));
            }

            [TestMethod]
            public void ReturnIntValueInBlockTest()
            {
                string[] inputValues = { "0", "1", "-2" };
                bool[] expectedValues = { false, true, true };
                StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(false));
            }

            [TestMethod]
            public void ReturnDefaultValueOtherwiseInBlockTest()
            {
                bool[] expectedValues;
                string[] inputValues = { "0.1", "1.1", "something", "()" };
                bool expectedValue = false;
                expectedValues = new bool[] { expectedValue, expectedValue, expectedValue, expectedValue };
                StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(expectedValue));

                expectedValue = true;
                expectedValues = new bool[] { expectedValue, expectedValue, expectedValue, expectedValue };
                StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(expectedValue));
            }

            [TestMethod]
            public void ReturnStringValueInBlockAndSkipRestOfBlockTest()
            {
                string[] inputValues = { "true next", "false next" };
                bool[] expectedValues = { true, false };
                StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(false));
            }

            [TestMethod]
            public void ReturnIntValueInBlockAndSkipRestOfBlockTest()
            {
                string[] inputValues = { "0 next", "1 some", "-2 thing" };
                bool[] expectedValues = { false, true, true };
                StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(false));
            }

            [TestMethod]
            public void ReturnDefaultValueOtherwiseInBlockAndSkipRestOfBlockTest()
            {
                string[] inputValues = { "0.1 x", "1.1 y", "something z", "()" };
                bool[] expectedValues = { false, false, false, false };
                StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(false));
            }
            [TestMethod]
            public void EmptyBlockReturnGivenDefaultTest()
            {
                StfTokenReaderCommon.OnEmptyBlockEndReturnGivenDefault<bool, bool>
                   (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadBoolBlock(x));
            }

            [TestMethod]
            public void NonBoolOrIntInBlockReturnFalseWithoutWarningTest()
            {
                AssertWarnings.NotExpected();
                string[] testValues = new string[] { "(bool)", "(something)" };
                string inputString = String.Join(" ", testValues);

                bool expectedResult = false;
                var reader = Create.Reader(inputString);
                foreach (string testValue in testValues)
                {
                    bool result = !expectedResult;
                    result = reader.ReadBoolBlock(expectedResult);
                    Assert.AreEqual(expectedResult, result);
                }

                expectedResult = true;
                reader = Create.Reader(inputString);
                foreach (string testValue in testValues)
                {
                    bool result = !expectedResult;
                    result = reader.ReadBoolBlock(expectedResult);
                    Assert.AreEqual(expectedResult, result);
                }
            }
        }
        #endregion

        #region double
        [TestClass]
        public class ReadDoubleTests
        {
            static readonly double NICEDEFAULT = default;
            static readonly double SOMEDEFAULT = -2;
            static readonly double[] SOMEDEFAULTS = new double[] { 3, 5, -6 };

            [TestMethod]
            public void OnNoValueWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnNoValueWarnAndReturnDefault<double, double?>
                    (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDouble(x));
            }

            [TestMethod]
            public void ReturnValueTest()
            {
                StfTokenReaderCommon.ReturnValue<double>(SOMEDEFAULTS, reader => reader.ReadDouble(null));
            }

            [TestMethod]
            public void WithCommaReturnValueTest()
            {
                StfTokenReaderCommon.WithCommaReturnValue<double>(SOMEDEFAULTS, reader => reader.ReadDouble(null));
            }

            [TestMethod]
            public void ForEmptyStringReturnNiceDefaultTest()
            {
                StfTokenReaderCommon.ForEmptyStringReturnNiceDefault<double, double?>
                    (NICEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDouble(x));
            }

            [TestMethod]
            public void ForNonNumbersReturnZeroAndWarnTest()
            {
                StfTokenReaderCommon.ForNonNumbersReturnNiceDefaultAndWarn<double, double?>
                    (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDouble(x));
            }

        }

        [TestClass]
        public class ReadDoubleBlockTests
        {
            static readonly double SOMEDEFAULT = -2;
            static readonly double[] SOMEDEFAULTS = new double[] { 3, 5, -6 };

            [TestMethod]
            public void EofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault<double, double?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDoubleBlock(x));
            }

            [TestMethod]
            public void ForNoOpenReturnDefaultAndWarnTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<double, double?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDoubleBlock(x));
            }

            [TestMethod]
            public void BlockEndReturnDefaultOrWarnTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<double, double?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDoubleBlock(x));
            }

            [TestMethod]
            public void ReturnValueInBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlock<double>(SOMEDEFAULTS, reader => reader.ReadDoubleBlock(null));
            }

            [TestMethod]
            public void ReturnValueInBlockAndSkipRestOfBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<double>(SOMEDEFAULTS, reader => reader.ReadDoubleBlock(null));
            }
        }
        #endregion

        #region float
        [TestClass]
        public class ReadFloatTests
        {
            static readonly float NICEDEFAULT = default;
            static readonly float SOMEDEFAULT = 2.1f;
            static readonly float[] SOMEDEFAULTS = new float[] { 1.1f, 4.5e6f, -0.01f };

            [TestMethod]
            public void OnNoValueWarnAndReturnDefault()
            {
                StfTokenReaderCommon.OnNoValueWarnAndReturnDefault<float, float?>
                    (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloat(STFReader.Units.None, x));
            }

            [TestMethod]
            public static void ReturnValueTest()
            {
                StfTokenReaderCommon.ReturnValue<float>(SOMEDEFAULTS, reader => reader.ReadFloat(STFReader.Units.None, null));
            }

            [TestMethod]
            public void WithCommaReturnValueTest()
            {
                StfTokenReaderCommon.WithCommaReturnValue<float>(SOMEDEFAULTS, reader => reader.ReadFloat(STFReader.Units.None, null));
            }

            [TestMethod]
            public void ForEmptyStringReturnNiceDefaultTest()
            {
                StfTokenReaderCommon.ForEmptyStringReturnNiceDefault<float, float?>
                    (NICEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloat(STFReader.Units.None, x));
            }

            [TestMethod]
            public void ForNonNumbersReturnZeroAndWarnTest()
            {
                StfTokenReaderCommon.ForNonNumbersReturnNiceDefaultAndWarn<float, float?>
                    (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloat(STFReader.Units.None, x));
            }

        }

        [TestClass]
        public class ReadFloatBlockTests
        {
            static readonly float SOMEDEFAULT = 2.1f;
            static readonly float[] SOMEDEFAULTS = new float[] { 1.1f, 4.5e6f, -0.01f };

            [TestMethod]
            public void OnEofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault<float, float?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloatBlock(STFReader.Units.None, x));
            }

            [TestMethod]
            public void ForNoOpenReturnDefaultAndWarnTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<float, float?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloatBlock(STFReader.Units.None, x));
            }

            [TestMethod]
            public void OnBlockEndReturnDefaultOrWarnTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<float, float?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloatBlock(STFReader.Units.None, x));
            }

            [TestMethod]
            public void ReturnValueInBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlock<float>
                    (SOMEDEFAULTS, reader => reader.ReadFloatBlock(STFReader.Units.None, null));
            }

            [TestMethod]
            public void ReturnValueInBlockAndSkipRestOfBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<float>
                    (SOMEDEFAULTS, reader => reader.ReadFloatBlock(STFReader.Units.None, null));
            }
        }
        #endregion

        #region hex
        [TestClass]
        public class ReadHexTests
        {
            static readonly uint NICEDEFAULT = default;
            static readonly uint SOMEDEFAULT = 2;
            static readonly uint[] SOMEDEFAULTS = new uint[] { 3, 5, 129 };
            static readonly string[] STRINGDEFAULTS = new string[] { "0000003", "00000005", "00000081" };

            [TestMethod]
            public void NoValueWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnNoValueWarnAndReturnDefault<uint, uint?>
                    (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHex(x));
            }

            [TestMethod]
            public void ReturnValueTest()
            {
                StfTokenReaderCommon.ReturnValue<uint>(SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadHex(null));
            }

            [TestMethod]
            public void WithCommaReturnValueTest()
            {
                StfTokenReaderCommon.WithCommaReturnValue<uint>(SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadHex(null));
            }

            [TestMethod]
            public void ForEmptyStringReturnNiceDefaultTest()
            {
                StfTokenReaderCommon.ForEmptyStringReturnNiceDefault<uint, uint?>
                    (NICEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHex(x));
            }

            [TestMethod]
            public void ForNonNumbersReturnZeroAndWarnTest()
            {
                StfTokenReaderCommon.ForNonNumbersReturnNiceDefaultAndWarn<uint, uint?>
                    (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHex(x));
            }
        }

        [TestClass]
        public class ReadHexBlockTests
        {
            static readonly uint SOMEDEFAULT = 4;
            static readonly uint[] SOMEDEFAULTS = new uint[] { 3, 5, 20 };
            static readonly string[] STRINGDEFAULTS = new string[] { "00000003", "00000005", "00000014" };

            [TestMethod]
            public void EofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault<uint, uint?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHexBlock(x));
            }

            [TestMethod]
            public void ForNoOpenReturnDefaultAndWarnTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<uint, uint?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHexBlock(x));
            }

            [TestMethod]
            public void OnBlockEndReturnDefaultOrWarnTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<uint, uint?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHexBlock(x));
            }

            [TestMethod]
            public void ReturnValueInBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlock<uint>(SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadHexBlock(null));
            }

            [TestMethod]
            public void ReturnValueInBlockAndSkipRestOfBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<uint>(SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadHexBlock(null));
            }
        }
        #endregion

        #region int
        [TestClass]
        public class ReadIntTests
        {
            static readonly int NICEDEFAULT = default;
            static readonly int SOMEDEFAULT = -2;
            static readonly int[] SOMEDEFAULTS = new int[] { 3, 5, -6 };

            [TestMethod]
            public void OnNoValueWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnNoValueWarnAndReturnDefault<int, int?>
                    (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadInt(x));
            }

            [TestMethod]
            public void ReturnValueTest()
            {
                StfTokenReaderCommon.ReturnValue<int>(SOMEDEFAULTS, reader => reader.ReadInt(null));
            }

            [TestMethod]
            public void ReturnValueWithSignTest()
            {
                string[] inputs = { "+2", "-3" };
                int[] expected = { 2, -3 };
                StfTokenReaderCommon.ReturnValue<int>(expected, inputs, reader => reader.ReadInt(null));
            }

            [TestMethod]
            public void WithCommaReturnValueTest()
            {
                StfTokenReaderCommon.WithCommaReturnValue<int>(SOMEDEFAULTS, reader => reader.ReadInt(null));
            }

            [TestMethod]
            public void ForEmptyStringReturnNiceDefaultTest()
            {
                StfTokenReaderCommon.ForEmptyStringReturnNiceDefault<int, int?>
                    (NICEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadInt(x));
            }

            [TestMethod]
            public void ForNonNumbersReturnZeroAndWarnTest()
            {
                StfTokenReaderCommon.ForNonNumbersReturnNiceDefaultAndWarn<int, int?>
                    (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadInt(x));
            }

        }

        [TestClass]
        public class ReadIntBlockTests
        {
            static readonly int SOMEDEFAULT = -2;
            static readonly int[] SOMEDEFAULTS = new int[] { 3, 5, -6 };

            [TestMethod]
            public void OnEofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault<int, int?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadIntBlock(x));
            }

            [TestMethod]
            public void ForNoOpenReturnDefaultAndWarnTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<int, int?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadIntBlock(x));
            }

            [TestMethod]
            public void OnBlockEndReturnDefaultOrWarnTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<int, int?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadIntBlock(x));
            }

            [TestMethod]
            public void ReturnValueInBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlock<int>(SOMEDEFAULTS, reader => reader.ReadIntBlock(null));
            }

            [TestMethod]
            public void ReturnValueInBlockAndSkipRestOfBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<int>(SOMEDEFAULTS, reader => reader.ReadIntBlock(null));
            }

        }
        #endregion

        #region uint
        [TestClass]
        public class ReadUIntTests
        {
            static readonly uint NICEDEFAULT = default;
            static readonly uint SOMEDEFAULT = 2;
            static readonly uint[] SOMEDEFAULTS = new uint[] { 3, 5, 200 };

            [TestMethod]
            public void OnNoValueWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnNoValueWarnAndReturnDefault<uint, uint?>
                    (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUInt(x));
            }

            [TestMethod]
            public void ReturnValueTest()
            {
                StfTokenReaderCommon.ReturnValue<uint>(SOMEDEFAULTS, reader => reader.ReadUInt(null));
            }

            [TestMethod]
            public void WithCommaReturnValueTest()
            {
                StfTokenReaderCommon.WithCommaReturnValue<uint>(SOMEDEFAULTS, reader => reader.ReadUInt(null));
            }

            [TestMethod]
            public void ForEmptyStringReturnNiceDefaultTest()
            {
                StfTokenReaderCommon.ForEmptyStringReturnNiceDefault<uint, uint?>
                    (NICEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUInt(x));
            }

            [TestMethod]
            public void ForNonNumbersReturnZeroAndWarnTest()
            {
                StfTokenReaderCommon.ForNonNumbersReturnNiceDefaultAndWarn<uint, uint?>
                    (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUInt(x));
            }
        }

        [TestClass]
        public class ReadUIntBlockTests
        {
            static readonly uint SOMEDEFAULT = 4;
            static readonly uint[] SOMEDEFAULTS = new uint[] { 3, 5, 20 };

            [TestMethod]
            public void OnEofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault<uint, uint?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUIntBlock(x));
            }

            [TestMethod]
            public void ForNoOpenReturnDefaultAndWarnTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<uint, uint?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUIntBlock(x));
            }

            [TestMethod]
            public void OnBlockEndReturnDefaultOrWarnTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<uint, uint?>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUIntBlock(x));
            }

            [TestMethod]
            public void ReturnValueInBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlock<uint>(SOMEDEFAULTS, reader => reader.ReadUIntBlock(null));
            }

            [TestMethod]
            public void ReturnValueInBlockAndSkipRestOfBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<uint>(SOMEDEFAULTS, reader => reader.ReadUIntBlock(null));
            }
        }
        #endregion

        #region string
        [TestClass]
        public class ReadStringTests
        {
            [TestMethod]
            public static void ReturnValueTest()
            {
                AssertWarnings.NotExpected();
                string[] testValues = new string[] { "token", "somestring", "lights" };
                string inputString = string.Join(" ", testValues);
                var reader = Create.Reader(inputString);

                foreach (string testValue in testValues)
                {
                    Assert.AreEqual(testValue, reader.ReadString());
                }
            }

            [TestMethod]
            public void SkipValueStartingWithUnderscoreTest()
            {
                AssertWarnings.NotExpected();
                string underscoreToken = "_underscore";
                string toBeSkippedToken = "tobeskippedtoken";
                string followingToken = "followingtoken";
                string inputString = underscoreToken + " " + toBeSkippedToken + " " + followingToken;
                var reader = Create.Reader(inputString);
                Assert.AreEqual(underscoreToken, reader.ReadString());
                Assert.AreEqual(toBeSkippedToken, reader.ReadString());
                Assert.AreEqual(followingToken, reader.ReadString());
            }
        }

        [TestClass]
        public class ReadStringBlockTests
        {
            static readonly string SOMEDEFAULT = "a";
            static readonly string[] SOMEDEFAULTS = new string[] { "ss", "SomeThing", "da" };

            [TestMethod]
            public void OnEofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault<string, string>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadStringBlock(x));
            }

            [TestMethod]
            public void ForNoOpenWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<string, string>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadStringBlock(x));
            }

            [TestMethod]
            public void OnBlockEndReturnDefaultOrWarnTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<string, string>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadStringBlock(x));
            }

            [TestMethod]
            public void ReturnValueInBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlock<string>(SOMEDEFAULTS, reader => reader.ReadStringBlock(null));
            }

            [TestMethod]
            public void ReturnValueInBlockAndSkipRestOfBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<string>(SOMEDEFAULTS, reader => reader.ReadStringBlock(null));
            }
        }
        #endregion

        #region Vector2
        [TestClass]
        public class ReadVector2BlockTests
        {
            static readonly Vector2 SOMEDEFAULT = new Vector2(1.1f, 1.2f);
            static readonly Vector2[] SOMEDEFAULTS = new Vector2[] { new Vector2(1.3f, 1.5f), new Vector2(-2f, 1e6f) };
            static readonly string[] STRINGDEFAULTS = new string[] { "1.3 1.5 ignore", "-2 1000000" };

            [TestMethod]
            public void OnEofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault
                    (SOMEDEFAULT, SOMEDEFAULT, (STFReader reader, ref Vector2 x) => reader.ReadVector2Block(STFReader.Units.None, ref x));
            }

            [TestMethod]
            public void ForNoOpenReturnDefaultAndWarnTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault
                    (SOMEDEFAULT, SOMEDEFAULT, (STFReader reader, ref Vector2 x) => reader.ReadVector2Block(STFReader.Units.None, ref x));
            }

            [TestMethod]
            public void OnBlockEndReturnDefaultWhenGivenTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnGivenDefault
                    (SOMEDEFAULT, SOMEDEFAULT, (STFReader reader, ref Vector2 x) => reader.ReadVector2Block(STFReader.Units.None, ref x));
            }

            [TestMethod]
            public void ReturnValueInBlockTest()
            {
                Vector2 zero = Vector2.Zero;
                StfTokenReaderCommon.ReturnValueInBlock(SOMEDEFAULTS, STRINGDEFAULTS, (STFReader reader, ref Vector2 x) => reader.ReadVector2Block(STFReader.Units.None, ref zero));
            }

            [TestMethod]
            public void ReturnValueInBlockAndSkipRestOfBlockTest()
            {
                Vector2 zero = Vector2.Zero;
                StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock(SOMEDEFAULTS, STRINGDEFAULTS, (STFReader reader, ref Vector2 x) => reader.ReadVector2Block(STFReader.Units.None, ref zero));
            }
        }
        #endregion

        #region Vector3
        [TestClass]
        public class ReadVector3LegacyBlockTests
        {
            static readonly Vector3 SOMEDEFAULT = new Vector3(1.1f, 1.2f, 1.3f);
            static readonly Vector3[] SOMEDEFAULTS = new Vector3[] { new Vector3(1.3f, 1.5f, 1.8f), new Vector3(1e-3f, -2f, -1e3f) };
            static readonly string[] STRINGDEFAULTS = new string[] { "1.3 1.5 1.8 ignore", "0.001, -2, -1000" };


            [TestMethod]
            public void OnEofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault<Vector3, Vector3>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector3Block(STFReader.Units.None, x));
            }

            [TestMethod]
            public void ForNoOpenReturnDefaultAndWarnTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<Vector3, Vector3>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector3Block(STFReader.Units.None, x));
            }

            [TestMethod]
            public void OnBlockEndReturnDefaultWhenGivenTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnGivenDefault<Vector3, Vector3>
                    (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector3Block(STFReader.Units.None, x));
            }

            [TestMethod]
            public void ReturnValueInBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlock<Vector3>
                    (SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadVector3Block(STFReader.Units.None, Vector3.Zero));
            }

            [TestMethod]
            public void ReturnValueInBlockAndSkipRestOfBlockTest()
            {
                StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<Vector3>
                    (SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadVector3Block(STFReader.Units.None, Vector3.Zero));
            }
        }

        [TestClass]
        public class ReadVector3BlockTests
        {
            static readonly Vector3 SOMEDEFAULT = new Vector3(1.1f, 1.2f, 1.3f);
            static readonly Vector3[] SOMEDEFAULTS = new Vector3[] { new Vector3(1.3f, 1.5f, 1.8f), new Vector3(1e-3f, -2f, -1e3f) };
            static readonly string[] STRINGDEFAULTS = new string[] { "1.3 1.5 1.8 ignore", "0.001, -2, -1000" };


            [TestMethod]
            public void OnEofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault
                    (SOMEDEFAULT, SOMEDEFAULT, (STFReader reader, ref Vector3 x) => reader.ReadVector3Block(STFReader.Units.None, ref x));
            }

            [TestMethod]
            public void ForNoOpenReturnDefaultAndWarnTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault
                    (SOMEDEFAULT, SOMEDEFAULT, (STFReader reader, ref Vector3 x) => reader.ReadVector3Block(STFReader.Units.None, ref x));
            }

            [TestMethod]
            public void OnBlockEndReturnDefaultWhenGivenTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnGivenDefault
                    (SOMEDEFAULT, SOMEDEFAULT, (STFReader reader, ref Vector3 x) => reader.ReadVector3Block(STFReader.Units.None, ref x));
            }

            [TestMethod]
            public void ReturnValueInBlockTest()
            {
                Vector3 zero = Vector3.Zero;
                StfTokenReaderCommon.ReturnValueInBlock
                    (SOMEDEFAULTS, STRINGDEFAULTS, (STFReader reader, ref Vector3 x) => reader.ReadVector3Block(STFReader.Units.None, ref zero));
            }

            [TestMethod]
            public void ReturnValueInBlockAndSkipRestOfBlockTest()
            {
                Vector3 zero = Vector3.Zero;
                StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock
                    (SOMEDEFAULTS, STRINGDEFAULTS, (STFReader reader, ref Vector3 x) => reader.ReadVector3Block(STFReader.Units.None, ref zero));
            }
        }
        #endregion

        #region Vector4
        [TestClass]
        public class ReadVector4BlockTests
        {
            static readonly Vector4 SOMEDEFAULT = new Vector4(1.1f, 1.2f, 1.3f, 1.4f);
            static readonly Vector4[] SOMEDEFAULTS = new Vector4[] { new Vector4(1.3f, 1.5f, 1.7f, 1.9f) };
            static readonly string[] STRINGDEFAULTS = new string[] { "1.3 1.5 1.7 1.9 ignore" };


            [TestMethod]
            public void OnEofWarnAndReturnDefaultTest()
            {
                StfTokenReaderCommon.OnEofWarnAndReturnDefault
                    (SOMEDEFAULT, SOMEDEFAULT, (STFReader reader, ref Vector4 x) => reader.ReadVector4Block(STFReader.Units.None, ref x));
            }

            [TestMethod]
            public void ForNoOpenReturnDefaultAndWarnTest()
            {
                StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault
                    (SOMEDEFAULT, SOMEDEFAULT, (STFReader reader, ref Vector4 x) => reader.ReadVector4Block(STFReader.Units.None, ref x));
            }

            [TestMethod]
            public void OnBlockEndReturnDefaultWhenGivenTest()
            {
                StfTokenReaderCommon.OnBlockEndReturnGivenDefault
                    (SOMEDEFAULT, SOMEDEFAULT, (STFReader reader, ref Vector4 x) => reader.ReadVector4Block(STFReader.Units.None, ref x));
            }

            [TestMethod]
            public void ReturnValueInBlockTest()
            {
                Vector4 vector4 = Vector4.Zero;
                StfTokenReaderCommon.ReturnValueInBlock
                    (SOMEDEFAULTS, STRINGDEFAULTS, (STFReader reader, ref Vector4 x) => reader.ReadVector4Block(STFReader.Units.None, ref vector4));
            }

            [TestMethod]
            public void ReturnValueInBlockAndSkipRestOfBlockTest()
            {
                Vector4 vector4 = Vector4.Zero;
                StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock
                    (SOMEDEFAULTS, STRINGDEFAULTS, (STFReader reader, ref Vector4 x) => reader.ReadVector4Block(STFReader.Units.None, ref vector4));
            }
        }
        #endregion

    }

    [TestClass]
    public class StfReaderIntegrationTests
    {
        [TestMethod]
        public void EncodingAscii()
        {
            var reader = new STFReader(new MemoryStream(Encoding.ASCII.GetBytes("TheBlock()")), "", Encoding.ASCII, false);
            reader.MustMatch("TheBlock");
        }

        [TestMethod]
        public void EncodingUtf16()
        {
            var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("TheBlock()")), "", Encoding.Unicode, false);
            reader.MustMatch("TheBlock");
        }

        [TestMethod]
        public void EmptyFile()
        {
            AssertWarnings.Expected(); // many warnings will result!
            using (var reader = new STFReader(new MemoryStream(Encoding.ASCII.GetBytes("")), "EmptyFile.stf", Encoding.ASCII, false))
            {
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual("EmptyFile.stf", reader.FileName);
                Assert.AreEqual(1, reader.LineNumber);
                Assert.AreEqual(null, reader.SimisSignature);
                // Note, the Debug.Assert() in reader.Tree is already captured by AssertWarnings.Expected.
                // For the rest, we do not care which exception is being thrown.
                Assert.ThrowsException<AssertFailedException>(() => reader.Tree);

                // All of the following will execute successfully at EOF..., although they might give warnings.
                reader.MustMatch("ANYTHING GOES");
                reader.ParseBlock(new STFReader.TokenProcessor[0]);
                reader.ParseFile(new STFReader.TokenProcessor[0]);
                Assert.AreEqual(-1, reader.PeekPastWhitespace());
                Assert.AreEqual(false, reader.ReadBoolBlock(false));
                Assert.AreEqual(0, reader.ReadDouble(null));
                Assert.AreEqual(0, reader.ReadDoubleBlock(null));
                Assert.AreEqual(0, reader.ReadFloat(STFReader.Units.None, null));
                Assert.AreEqual(0, reader.ReadFloatBlock(STFReader.Units.None, null));
                Assert.AreEqual(0U, reader.ReadHex(null));
                Assert.AreEqual(0U, reader.ReadHexBlock(null));
                Assert.AreEqual(0, reader.ReadInt(null));
                Assert.AreEqual(0, reader.ReadIntBlock(null));
                Assert.AreEqual("", reader.ReadItem());
                Assert.AreEqual("", reader.ReadString());
                Assert.AreEqual(null, reader.ReadStringBlock(null));
                Assert.AreEqual(0U, reader.ReadUInt(null));
                Assert.AreEqual(0U, reader.ReadUIntBlock(null));
                Vector2 vector2 = Vector2.Zero;
                reader.ReadVector2Block(STFReader.Units.None, ref vector2);
                Assert.AreEqual(Vector2.Zero, vector2);
                Vector3 vector3 = Vector3.Zero;
                reader.ReadVector3Block(STFReader.Units.None, ref vector3);
                Assert.AreEqual(Vector3.Zero, vector3);
                Vector4 vector4 = Vector4.Zero;
                reader.ReadVector4Block(STFReader.Units.None, ref vector4);
                Assert.AreEqual(Vector4.Zero, vector4);
            }
        }

        [TestMethod]
        public void EmptyBlock()
        {
            AssertWarnings.Expected();
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("TheBlock()")), "EmptyBlock.stf", Encoding.Unicode, false))
            {
                Assert.IsFalse(reader.Eof, "STFReader.Eof");
                Assert.IsFalse(reader.EOF(), "STFReader.EOF()");
                Assert.IsFalse(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual("EmptyBlock.stf", reader.FileName);
                Assert.AreEqual(1, reader.LineNumber);
                Assert.AreEqual(null, reader.SimisSignature);
                Assert.ThrowsException<STFException>(() => reader.MustMatch("Something Else"));
                // We can't rewind the STFReader and it has advanced forward now. :(
            }
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("TheBlock()")), "", Encoding.Unicode, false))
            {
                reader.MustMatch("TheBlock");
                Assert.IsFalse(reader.Eof, "STFReader.Eof");
                Assert.IsFalse(reader.EOF(), "STFReader.EOF()");
                Assert.IsFalse(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
                reader.VerifyStartOfBlock(); // Same as reader.MustMatch("(");
                Assert.IsFalse(reader.Eof, "STFReader.Eof");
                Assert.IsFalse(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
                reader.MustMatch(")");
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("TheBlock()")), "", Encoding.Unicode, false))
            {
                var called = 0;
                var not_called = 0;
                reader.ParseBlock(new[] {
                    new STFReader.TokenProcessor("theblock", () => { called++; }),
                    new STFReader.TokenProcessor("TheBlock", () => { not_called++; })
                });
                Assert.IsTrue(called == 1, "TokenProcessor for theblock must be called exactly once: called = " + called);
                Assert.IsTrue(not_called == 0, "TokenProcessor for TheBlock must not be called: not_called = " + not_called);
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("TheBlock()")), "", Encoding.Unicode, false))
            {
                reader.MustMatch("TheBlock");
                reader.SkipBlock();
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void NumericFormats()
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("1.123456789 1e9 1.1e9 2.123456 2e9 2.1e9 00ABCDEF 123456 -123456 234567")), "", Encoding.Unicode, false))
            {
                Assert.AreEqual(1.123456789, reader.ReadDouble(null));
                Assert.AreEqual(1e9, reader.ReadDouble(null));
                Assert.AreEqual(1.1e9, reader.ReadDouble(null));
                Assert.AreEqual(2.123456f, reader.ReadFloat(STFReader.Units.None, null));
                Assert.AreEqual(2e9, reader.ReadFloat(STFReader.Units.None, null));
                Assert.AreEqual(2.1e9f, reader.ReadFloat(STFReader.Units.None, null));
                Assert.AreEqual((uint)0xABCDEF, reader.ReadHex(null));
                Assert.AreEqual(123456, reader.ReadInt(null));
                Assert.AreEqual(-123456, reader.ReadInt(null));
                Assert.AreEqual(234567U, reader.ReadUInt(null));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void StringFormats()
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("Item1 \"Item2\" \"Item\" + \"3\" String1 \"String2\" \"String\" + \"3\"")), "", Encoding.Unicode, false))
            {
                Assert.AreEqual("Item1", reader.ReadItem());
                Assert.AreEqual("Item2", reader.ReadItem());
                Assert.AreEqual("Item3", reader.ReadItem());
                Assert.AreEqual("String1", reader.ReadString());
                Assert.AreEqual("String2", reader.ReadString());
                Assert.AreEqual("String3", reader.ReadString());
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void BlockNumericFormats()
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("(true ignored) (false ignored) (1.123456789 ignored) (1e9 ignored) (1.1e9 ignored) (2.123456 ignored) (2e9 ignored) (2.1e9 ignored) (00ABCDEF ignored) (123456 ignored) (-123456 ignored) (234567 ignored)")), "", Encoding.Unicode, false))
            {
                Assert.AreEqual(true, reader.ReadBoolBlock(false));
                Assert.AreEqual(false, reader.ReadBoolBlock(true));
                Assert.AreEqual(1.123456789, reader.ReadDoubleBlock(null));
                Assert.AreEqual(1e9, reader.ReadDoubleBlock(null));
                Assert.AreEqual(1.1e9, reader.ReadDoubleBlock(null));
                Assert.AreEqual(2.123456f, reader.ReadFloatBlock(STFReader.Units.None, null));
                Assert.AreEqual(2e9, reader.ReadFloatBlock(STFReader.Units.None, null));
                Assert.AreEqual(2.1e9f, reader.ReadFloatBlock(STFReader.Units.None, null));
                Assert.AreEqual((uint)0xABCDEF, reader.ReadHexBlock(null));
                Assert.AreEqual(123456, reader.ReadIntBlock(null));
                Assert.AreEqual(-123456, reader.ReadIntBlock(null));
                Assert.AreEqual(234567U, reader.ReadUIntBlock(null));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void BlockStringFormats()
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("(String1 ignored) (\"String2\" ignored) (\"String\" + \"3\" ignored)")), "", Encoding.Unicode, false))
            {
                Assert.AreEqual("String1", reader.ReadStringBlock(null));
                Assert.AreEqual("String2", reader.ReadStringBlock(null));
                Assert.AreEqual("String3", reader.ReadStringBlock(null));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void BlockVectorFormats()
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("(1.1 1.2 ignored) (1.1 1.2 1.3 ignored) (1.1 1.2 1.3 1.4 ignored)")), "", Encoding.Unicode, false))
            {
                Vector2 vector2 = Vector2.Zero;
                reader.ReadVector2Block(STFReader.Units.None, ref vector2);
                Assert.AreEqual(new Vector2(1.1f, 1.2f), vector2);
                Vector3 vector3 = Vector3.Zero;
                reader.ReadVector3Block(STFReader.Units.None, ref vector3);
                Assert.AreEqual(new Vector3(1.1f, 1.2f, 1.3f), vector3);
                Vector4 vector4 = Vector4.Zero;
                reader.ReadVector4Block(STFReader.Units.None, ref vector4);
                Assert.AreEqual(new Vector4(1.1f, 1.2f, 1.3f, 1.4f), vector4);
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
                Assert.IsTrue(reader.EOF(), "STFReader.EOF()");
                Assert.IsTrue(reader.EndOfBlock(), "STFReader.EndOfBlock()");
                Assert.AreEqual(1, reader.LineNumber);
            }
        }

        [TestMethod]
        public void Units()
        {
            AssertWarnings.NotExpected();
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes("1.1 1.2 1.3km 1.4 1.5km")), "", Encoding.Unicode, false))
            {
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(1.10000f, reader.ReadFloat(STFReader.Units.None, null)));
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(1.20000f, reader.ReadFloat(STFReader.Units.Distance, null)));
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(1300.00000f, reader.ReadFloat(STFReader.Units.Distance, null)));
                float result4 = 0;
                AssertWarnings.Matching("", () =>
                {
                    result4 = reader.ReadFloat(STFReader.Units.Distance | STFReader.Units.Compulsory, null);
                });
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(1.40000f, result4));
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(1500.00000f, reader.ReadFloat(STFReader.Units.Distance | STFReader.Units.Compulsory, null)));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
            }
        }

        /* All conversion factors have been sourced from:
        *   - https://en.wikipedia.org/wiki/Conversion_of_units
        *   - Windows 8.1 Calculator utility
        * 
        * In any case where there is disagreement or only 1 source available, the
        * chosen source is specified. In all other cases, all sources exist and agree.
        * 
        *********************************************************************
        * DO NOT CHANGE ANY OF THESE WITHOUT CONSULTING OTHER TEAM MEMBERS! *
        *********************************************************************/

        const double BarToPascal = 100000;
        const double CelsiusToKelvin = 273.15;
        const double DayToSecond = 86400;
        const double FahrenheitToKelvinA = 459.67;
        const double FahrenheitToKelvinB = 5 / 9;
        const double FeetToMetre = 0.3048;
        const double GallonUSToCubicMetre = 0.003785411784; // (fluid; Wine)
        const double HorsepowerToWatt = 745.69987158227022; // (imperial mechanical hoursepower)
        const double HourToSecond = 3600;
        const double InchOfMercuryToPascal = 3386.389; // (conventional)
        const double InchToMetre = 0.0254;
        const double KilometrePerHourToMetrePerSecond = 1 / 3.6;
        const double LitreToCubicMetre = 0.001;
        const double MilePerHourToMetrePerSecond = 0.44704;
        const double MileToMetre = 1609.344;
        const double MinuteToSecond = 60;
        const double PascalToPSI = 0.0001450377438972831; // Temporary while pressure values are returned in PSI instead of Pascal.
        const double PoundForceToNewton = 4.4482216152605; // Conversion_of_units
        const double PoundToKG = 0.453592926; //0.45359237;
        const double PSIToPascal = 6894.757;
        const double TonLongToKG = 1016.0469088;
        const double TonneToKG = 1000;
        const double TonShortToKG = 907.18474;

        private void UnitConversionTest(string input, double output, STFReader.Units unit)
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes(input)), "", Encoding.Unicode, false))
            {
                Assert.IsTrue(DynamicPrecisionEqualityComparer.Float.Equals(output, reader.ReadFloat(unit, null)));
                Assert.IsTrue(reader.Eof, "STFReader.Eof");
            }
        }

        /* Base unit categories available in MSTS:
         *     (Constants)   pi
         *     Current       a ka ma
         *     Energy        j nm
         *     Force         n kn lbf
         *     Length        mm cm m km " in ' ft mil
         *     Mass          g kg t tn ton lb lbs
         *     Power         kw hp
         *     Pressure      pascal mbar bar psi
         *     Rotation      deg rad
         *     Temperature   k c f
         *     Time          us ms s min h d
         *     Velocity      kmh mph
         *     Voltage       v kv mv
         *     Volume        l gal
         */

        /* Base units for current available in MSTS:
         *     a         Amp
         *     ka        Kilo-amp
         *     ma        Mega-amp
         */

        [TestMethod]
        public void UnitConversionBaseMstsCurrent()
        {
            UnitConversionTest("1.2a", 1.2, STFReader.Units.Current);
            // TODO not implemented yet: UnitConversionTest("1.2ka", 1.2 * 1000, STFReader.Units.Current);
            // TODO not implemented yet: UnitConversionTest("1.2ma", 1.2 * 1000 * 1000, STFReader.Units.Current);
        }

        /* Base units for energy available in MSTS:
 *     j         Joule
 *     nm        Newton meter
 */
        [TestMethod]
        public void UnitConversionBaseMstsEnergy()
        {
            // TODO not implemented yet: UnitConversionTest("1.2j", 1.2, STFReader.Units.Energy);
            // TODO not implemented yet: UnitConversionTest("1.2nm", 1.2, STFReader.Units.Energy);
        }

        /* Base units for force available in MSTS:
         *     n         Newton
         *     kn        Kilo-newton
         *     lbf       Pound-force
         */
        [TestMethod]
        public void UnitConversionBaseMstsForce()
        {
            UnitConversionTest("1.2n", 1.2, STFReader.Units.Force);
            UnitConversionTest("1.2kn", 1.2 * 1000, STFReader.Units.Force);
            UnitConversionTest("1.2lbf", 1.2 * PoundForceToNewton, STFReader.Units.Force);
        }

        /* Base units for length available in MSTS:
         *     mm        Millimeter
         *     cm        Centimeter
         *     m         Meter
         *     km        Kilometer
         *     "         Inch
         *     in        Inch
         *     '         Foot
         *     ft        Foot
         *     mil       Mile
         */
        [TestMethod]
        public void UnitConversionBaseMstsLength()
        {
            UnitConversionTest("1.2mm", 1.2 / 1000, STFReader.Units.Distance);
            UnitConversionTest("1.2cm", 1.2 / 100, STFReader.Units.Distance);
            UnitConversionTest("1.2m", 1.2, STFReader.Units.Distance);
            UnitConversionTest("1.2km", 1.2 * 1000, STFReader.Units.Distance);
            // TODO not implemented yet: UnitConversionTest("1.2\"", 1.2 * InchToMetre, STFReader.Units.Distance);
            UnitConversionTest("1.2in", 1.2 * InchToMetre, STFReader.Units.Distance);
            // TODO not implemented yet: UnitConversionTest("1.2\'", 1.2 * FeetToMetre, STFReader.Units.Distance);
            UnitConversionTest("1.2ft", 1.2 * FeetToMetre, STFReader.Units.Distance);
            // TODO not implemented yet: UnitConversionTest("1.2mil", 1.2 * MileToMetre, STFReader.Units.Distance);
        }

        /* Base units for mass available in MSTS:
         *     g         Gram
         *     kg        Kilogram
         *     t         Tonne
         *     tn        Short/US ton
         *     ton       Long/UK ton
         *     lb        Pound
         *     lbs       Pound
         */
        [TestMethod]
        public void UnitConversionBaseMstsMass()
        {
            // TODO not implemented yet: UnitConversionTest("1.2g", 1.2 / 1000, STFReader.Units.Mass);
            UnitConversionTest("1.2kg", 1.2, STFReader.Units.Mass);
            UnitConversionTest("1.2t", 1.2 * TonneToKG, STFReader.Units.Mass);
            // TODO not implemented yet: UnitConversionTest("1.2tn", 1.2 * TonShortToKG, STFReader.Units.Mass);
            // TODO not implemented yet: UnitConversionTest("1.2ton", 1.2 * TonLongToKG, STFReader.Units.Mass);
            UnitConversionTest("1.2lb", 1.2 * PoundToKG, STFReader.Units.Mass);
            // TODO not implemented yet: UnitConversionTest("1.2lbs", 1.2 * PoundToKG, STFReader.Units.Mass);
        }

        /* Base units for power available in MSTS:
         *     kw        Kilowatt
         *     hp        Horsepower
         */
        [TestMethod]
        public void UnitConversionBaseMstsPower()
        {
            UnitConversionTest("1.2kw", 1.2 * 1000, STFReader.Units.Power);
            UnitConversionTest("1.2hp", 1.2 * HorsepowerToWatt, STFReader.Units.Power);
        }

        /* Base units for pressure available in MSTS:
         *     pascal    Pascal
         *     mbar      Millibar
         *     bar       Bar
         *     psi       psi
         */
        [TestMethod]
        public void UnitConversionBaseMstsPressure()
        {
            // TODO not implemented yet: UnitConversionTest("1.2pascal", 1.2, STFReader.Units.PressureDefaultInHg);
            // TODO not implemented yet: UnitConversionTest("1.2pascal", 1.2, STFReader.Units.PressureDefaultPSI);
            // TODO not implemented yet: UnitConversionTest("1.2mbar", 1.2 / 1000 * BarToPascal, STFReader.Units.PressureDefaultInHg);
            // TODO not implemented yet: UnitConversionTest("1.2mbar", 1.2 / 1000 * BarToPascal, STFReader.Units.PressureDefaultPSI);
            // TODO not using SI units: UnitConversionTest("1.2bar", 1.2 * BarToPascal, STFReader.Units.PressureDefaultInHg);
            // TODO not using SI units: UnitConversionTest("1.2bar", 1.2 * BarToPascal, STFReader.Units.PressureDefaultPSI);
            // TODO not using SI units: UnitConversionTest("1.2psi", 1.2 * PSIToPascal, STFReader.Units.PressureDefaultInHg);
            // TODO not using SI units: UnitConversionTest("1.2psi", 1.2 * PSIToPascal, STFReader.Units.PressureDefaultPSI);
        }

        /* Base units for rotation available in MSTS:
         *     deg       Degree
         *     rad       Radian
         */
        [TestMethod]
        public void UnitConversionBaseMstsRotation()
        {
            // TODO not implemented yet: UnitConversionTest("1.2deg", 1.2, STFReader.Units.Rotation);
            // TODO not implemented yet: UnitConversionTest("1.2rad", 1.2 * Math.PI / 180, STFReader.Units.Rotation);
        }

        /* Base units for temperature available in MSTS:
         *     k         Kelvin
         *     c         Celsius
         *     f         Fahrenheit
         */
        [TestMethod]
        public void UnitConversionBaseMstsTemperature()
        {
            // TODO not implemented yet: UnitConversionTest("1.2k", 1.2, STFReader.Units.Temperature);
            // TODO not implemented yet: UnitConversionTest("1.2c", 1.2 + CelsiusToKelvin, STFReader.Units.Temperature);
            // TODO not implemented yet: UnitConversionTest("1.2f", (1.2 + FahrenheitToKelvinA) * FahrenheitToKelvinB, STFReader.Units.Temperature);
        }

        /* Base units for time available in MSTS:
         *     us        Microsecond
         *     ms        Millisecond
         *     s         Second
         *     min       Minute
         *     h         Hour
         *     d         Day
         */
        [TestMethod]
        public void UnitConversionBaseMstsTime()
        {
            // TODO not implemented yet: UnitConversionTest("1.2us", 1.2 / 1000000, STFReader.Units.Time);
            // TODO not implemented yet: UnitConversionTest("1.2us", 1.2 / 1000000, STFReader.Units.TimeDefaultM);
            // TODO not implemented yet: UnitConversionTest("1.2us", 1.2 / 1000000, STFReader.Units.TimeDefaultH);
            // TODO not implemented yet: UnitConversionTest("1.2ms", 1.2 / 1000, STFReader.Units.Time);
            // TODO not implemented yet: UnitConversionTest("1.2ms", 1.2 / 1000, STFReader.Units.TimeDefaultM);
            // TODO not implemented yet: UnitConversionTest("1.2ms", 1.2 / 1000, STFReader.Units.TimeDefaultH);
            UnitConversionTest("1.2s", 1.2, STFReader.Units.Time);
            UnitConversionTest("1.2s", 1.2, STFReader.Units.TimeDefaultM);
            UnitConversionTest("1.2s", 1.2, STFReader.Units.TimeDefaultH);
            // TODO not implemented yet: UnitConversionTest("1.2min", 1.2 * MinuteToSecond, STFReader.Units.Time);
            // TODO not implemented yet: UnitConversionTest("1.2min", 1.2 * MinuteToSecond, STFReader.Units.TimeDefaultM);
            // TODO not implemented yet: UnitConversionTest("1.2min", 1.2 * MinuteToSecond, STFReader.Units.TimeDefaultH);
            UnitConversionTest("1.2h", 1.2 * HourToSecond, STFReader.Units.Time);
            UnitConversionTest("1.2h", 1.2 * HourToSecond, STFReader.Units.TimeDefaultM);
            UnitConversionTest("1.2h", 1.2 * HourToSecond, STFReader.Units.TimeDefaultH);
            // TODO not implemented yet: UnitConversionTest("1.2d", 1.2 * DayToSecond, STFReader.Units.Time);
            // TODO not implemented yet: UnitConversionTest("1.2d", 1.2 * DayToSecond, STFReader.Units.TimeDefaultM);
            // TODO not implemented yet: UnitConversionTest("1.2d", 1.2 * DayToSecond, STFReader.Units.TimeDefaultH);
        }

        /* Base units for velocity available in MSTS:
         *     kmh       Kilometers/hour
         *     mph       Miles/hour
         */
        [TestMethod]
        public void UnitConversionBaseMstsVelocity()
        {
            UnitConversionTest("1.2kmh", 1.2 * KilometrePerHourToMetrePerSecond, STFReader.Units.Speed);
            UnitConversionTest("1.2kmh", 1.2 * KilometrePerHourToMetrePerSecond, STFReader.Units.SpeedDefaultMPH);
            UnitConversionTest("1.2mph", 1.2 * MilePerHourToMetrePerSecond, STFReader.Units.Speed);
            UnitConversionTest("1.2mph", 1.2 * MilePerHourToMetrePerSecond, STFReader.Units.SpeedDefaultMPH);
        }

        /* Base units for voltage available in MSTS:
         *     v         Volt
         *     kv        Kilovolt
         *     mv        Megavolt
         */
        [TestMethod]
        public void UnitConversionBaseMstsVoltage()
        {
            UnitConversionTest("1.2v", 1.2, STFReader.Units.Voltage);
            UnitConversionTest("1.2kv", 1.2 * 1000, STFReader.Units.Voltage);
            // TODO not implemented yet: UnitConversionTest("1.2mv", 1.2 * 1000 * 1000, STFReader.Units.Voltage);
        }

        /* Base units for volume available in MSTS:
         *     l         Liter
         *     gal       Gallon (US)
         */
        [TestMethod]
        public void UnitConversionBaseMstsVolume()
        {
            // TODO not using SI units: UnitConversionTest("1.2l", 1.2 * LitreToCubicMetre, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2l", 1.2 * LitreToCubicMetre, STFReader.Units.VolumeDefaultFT3);
            // TODO not using SI units: UnitConversionTest("1.2gal", 1.2 * GallonUSToCubicMetre, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2gal", 1.2 * GallonUSToCubicMetre, STFReader.Units.VolumeDefaultFT3);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsArea()
        {
            // TODO not implemented yet: UnitConversionTest("1.2mm^2", 1.2 / 1000 / 1000, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2cm^2", 1.2 / 100 / 100, STFReader.Units.AreaDefaultFT2);
            UnitConversionTest("1.2m^2", 1.2, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2km^2", 1.2 * 1000 * 1000, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2\"^2", 1.2 * InchToMetre * InchToMetre, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2in^2", 1.2 * InchToMetre * InchToMetre, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2\'^2", 1.2 * FeetToMetre * FeetToMetre, STFReader.Units.AreaDefaultFT2);
            UnitConversionTest("1.2ft^2", 1.2 * FeetToMetre * FeetToMetre, STFReader.Units.AreaDefaultFT2);
            // TODO not implemented yet: UnitConversionTest("1.2mil^2", 1.2 * MileToMetre * MileToMetre, STFReader.Units.AreaDefaultFT2);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsDamping()
        {
            UnitConversionTest("1.2n/m/s", 1.2, STFReader.Units.Resistance);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsMassRate()
        {
            // TODO not using SI units: UnitConversionTest("1.2lb/h", 1.2 * PoundToKG / HourToSecond, STFReader.Units.MassRateDefaultLBpH);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsStiffness()
        {
            UnitConversionTest("1.2n/m", 1.2, STFReader.Units.Stiffness);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsVelocity()
        {
            UnitConversionTest("1.2m/s", 1.2, STFReader.Units.Speed);
            UnitConversionTest("1.2m/s", 1.2, STFReader.Units.SpeedDefaultMPH);
            UnitConversionTest("1.2km/h", 1.2 * 1000 / HourToSecond, STFReader.Units.Speed);
            UnitConversionTest("1.2km/h", 1.2 * 1000 / HourToSecond, STFReader.Units.SpeedDefaultMPH);
        }

        [TestMethod]
        public void UnitConversionDerivedMstsVolume()
        {
            // TODO not implemented yet: UnitConversionTest("1.2mm^3", 1.2 * 1000 / 1000 / 1000, STFReader.Units.Volume);
            // TODO not implemented yet: UnitConversionTest("1.2cm^3", 1.2 / 100 / 100 / 100, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2m^3", 1.2, STFReader.Units.Volume);
            // TODO not implemented yet: UnitConversionTest("1.2km^3", 1.2 * 1000 * 1000 * 1000, STFReader.Units.Volume);
            // TODO not implemented yet: UnitConversionTest("1.2\"^3", 1.2 * InchToMetre * InchToMetre * InchToMetre, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2in^3", 1.2 * InchToMetre * InchToMetre * InchToMetre, STFReader.Units.Volume);
            // TODO not implemented yet: UnitConversionTest("1.2\'^3", 1.2 * FeetToMetre * FeetToMetre * FeetToMetre, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2ft^3", 1.2 * FeetToMetre * FeetToMetre * FeetToMetre, STFReader.Units.Volume);
            // TODO not implemented yet: UnitConversionTest("1.2mil^3", 1.2 * MileToMetre * MileToMetre * MileToMetre, STFReader.Units.Volume);
        }

        /* Default unit for pressure in MSTS is UNKNOWN.
         */
        [TestMethod]
        public void UnitConversionDefaultMstsPressure()
        {
            //UnitConversionTest("1.2", UNKNOWN, STFReader.Units.Pressure);
            UnitConversionTest("1.2", 1.2 * InchOfMercuryToPascal * PascalToPSI, STFReader.Units.PressureDefaultInHg);
            UnitConversionTest("1.2", 1.2, STFReader.Units.PressureDefaultPSI);
        }

        /* Default unit for time in MSTS is seconds.
         */
        [TestMethod]
        public void UnitConversionDefaultMstsTime()
        {
            UnitConversionTest("1.2", 1.2, STFReader.Units.Time);
            UnitConversionTest("1.2", 1.2 * MinuteToSecond, STFReader.Units.TimeDefaultM);
            UnitConversionTest("1.2", 1.2 * HourToSecond, STFReader.Units.TimeDefaultH);
        }

        /* Default unit for velocity in MSTS is UNKNOWN.
         */
        [TestMethod]
        public void UnitConversionDefaultMstsVelocity()
        {
            //UnitConversionTest("1.2", UNKNOWN, STFReader.Units.Speed);
            UnitConversionTest("1.2", 1.2 * MilePerHourToMetrePerSecond, STFReader.Units.SpeedDefaultMPH);
        }

        /* Default unit for volume in MSTS is UNKNOWN.
         */
        [TestMethod]
        public void UnitConversionDefaultMstsVolume()
        {
            //UnitConversionTest("1.2", UNKNOWN, STFReader.Units.Volume);
            // TODO not using SI units: UnitConversionTest("1.2", 1.2 * FeetToMetre * FeetToMetre * FeetToMetre, STFReader.Units.VolumeDefaultFT3);
        }

        /* The following units are currently accepted by Open Rails but have no meaning in MSTS:
         *     amps
         *     btu/lb
         *     degc
         *     degf
         *     gals
         *     g-uk
         *     g-us
         *     hz
         *     inhg
         *     inhg/s
         *     kj/kg
         *     kmph
         *     kpa
         *     kpa/s
         *     kph
         *     ns/m
         *     rpm
         *     rps
         *     t-uk
         *     t-us
         *     w
         */

        [TestMethod]
        public void ParentheticalCommentsCanGoAnywhere()
        {
            // Also testing for bugs 1274713, 1221696, 1377393
            var part1 =
                "Wagon(\n" +
                "    Lights(\n" +
                "        Light( 1 )\n" +
                "";
            var part2 =
                "        Light( 2 )\n" +
                "";
            var part3 =
                "    )\n" +
                "    Sound( test.sms )\n" +
                ")";
            var middles = new[] {
                "        #(Comment)\n",
                "        # (Comment)\n",
                "        Skip( ** comment ** ) \n",
                "        Skip ( ** comment ** ) \n"
            };
            foreach (var middle in middles)
                ParenthicalCommentSingle(part1 + middle + part2 + part3);
            //foreach (var middle in middles)
            //    ParenthicalCommentSingle(part1 + part2 + middle + part3);
        }

        private void ParenthicalCommentSingle(string inputString)
        {
            using (var reader = new STFReader(new MemoryStream(Encoding.Unicode.GetBytes(inputString)), "", Encoding.Unicode, true))
            {
                reader.ReadItem();
                Assert.AreEqual("wagon", reader.Tree.ToLower());
                reader.MustMatch("(");

                reader.ReadItem();
                Assert.AreEqual("wagon(lights", reader.Tree.ToLower());
                reader.MustMatch("(");

                reader.ReadItem();
                Assert.AreEqual("wagon(lights(light", reader.Tree.ToLower());
                reader.MustMatch("(");
                Assert.AreEqual(1, reader.ReadInt(null));
                reader.SkipRestOfBlock();
                Assert.AreEqual("wagon(lights()", reader.Tree.ToLower());

                reader.ReadItem();
                Assert.AreEqual("wagon(lights(light", reader.Tree.ToLower());
                reader.MustMatch("(");
                Assert.AreEqual(2, reader.ReadInt(null));
                reader.SkipRestOfBlock();
                Assert.AreEqual("wagon(lights()", reader.Tree.ToLower());

                reader.ReadItem();
                reader.ReadItem();
                Assert.AreEqual("wagon(sound", reader.Tree.ToLower());
                Assert.AreEqual("test.sms", reader.ReadStringBlock(""));
                reader.SkipRestOfBlock();

                Assert.IsTrue(reader.Eof, "STFReader.Eof");
            }
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
            Assert.ThrowsException<STFException>(() => throw new STFException(new STFReader(new MemoryStream(), filename, Encoding.ASCII, true), message), message);
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
        #region Value itself
        public static void OnNoValueWarnAndReturnDefault<T, nullableT>
            (T niceDefault, T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = ")";
            T result = default;

            var reader = Create.Reader(inputString);
            AssertWarnings.Matching("Cannot parse|expecting.*found.*[)]", () => { result = codeDoingReading(reader, default); });
            Assert.AreEqual(niceDefault, result);
            Assert.AreEqual(")", reader.ReadItem());

            reader = Create.Reader(inputString);
            AssertWarnings.Matching("expecting.*found.*[)]", () => { result = codeDoingReading(reader, someDefault); });
            Assert.AreEqual(resultDefault, result);
            Assert.AreEqual(")", reader.ReadItem());
        }

        public static void ReturnValue<T>
            (T[] testValues, ReadValueCode<T> codeDoingReading)
        {
            string[] inputValues = testValues.Select(
                value => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
            ReturnValue<T>(testValues, inputValues, codeDoingReading);
        }

        public static void ReturnValue<T>
            (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string inputString = string.Join(" ", inputValues);
            var reader = Create.Reader(inputString);
            foreach (T testValue in testValues)
            {
                T result = codeDoingReading(reader);
                Assert.AreEqual(testValue, result);
            }
        }

        public static void WithCommaReturnValue<T>
            (T[] testValues, ReadValueCode<T> codeDoingReading)
        {
            string[] inputValues = testValues.Select(
                value => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
            WithCommaReturnValue(testValues, inputValues, codeDoingReading);
        }

        public static void WithCommaReturnValue<T>
            (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string inputString = string.Join(" ", inputValues);
            var reader = Create.Reader(inputString);
            foreach (T testValue in testValues)
            {
                T result = codeDoingReading(reader);
                Assert.AreEqual(testValue, result);
            }
        }

        public static void ForEmptyStringReturnNiceDefault<T, nullableT>
            (T niceDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string[] tokenValues = new string[] { "", "" };
            string emptyQuotedString = "\"\"";
            string inputString = emptyQuotedString + " " + emptyQuotedString;
            var reader = Create.Reader(inputString);

            T result = codeDoingReading(reader, default);
            Assert.AreEqual(niceDefault, result);

            result = codeDoingReading(reader, someDefault);
            Assert.AreEqual(niceDefault, result);
        }

        public static void ForNonNumbersReturnNiceDefaultAndWarn<T, nullableT>
            (T niceDefault, T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string[] testValues = new string[] { "noint", "sometoken", "(" };
            string inputString = string.Join(" ", testValues);

            var reader = Create.Reader(inputString);
            foreach (string testValue in testValues)
            {
                T result = default;
                AssertWarnings.Matching("Cannot parse", () => { result = codeDoingReading(reader, default); });
                Assert.AreEqual(niceDefault, result);
            }

            reader = Create.Reader(inputString);
            foreach (string testValue in testValues)
            {
                T result = default;
                AssertWarnings.Matching("Cannot parse", () => { result = codeDoingReading(reader, someDefault); });
                Assert.AreEqual(resultDefault, result);
            }
        }
        #endregion

        #region Value in blocks
        private static T Wrapper<T>(STFReader innerReader, T input, ReadValueCodeByRef<T> codeDoingReading)
        {
            codeDoingReading(innerReader, ref input);
            return input;
        }


        public static void OnEofWarnAndReturnDefault<T>(T resultDefault, T someValue, ReadValueCodeByRef<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader(string.Empty);
            T innerValue = someValue;
            T result = default;
            AssertWarnings.Matching("Unexpected end of file", () => result = Wrapper(reader, default, codeDoingReading));
            Assert.AreEqual(default, result);
            AssertWarnings.Matching("Unexpected end of file", () => { result = Wrapper(reader, innerValue, codeDoingReading); });
            Assert.AreEqual(resultDefault, result);
        }

        public static void OnEofWarnAndReturnDefault<T, nullableT>
            (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = "";
            var reader = Create.Reader(inputString);
            T result = default;
            AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, default); });
            Assert.AreEqual(default, result);
            AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, someDefault); });
            Assert.AreEqual(resultDefault, result);
        }

        public static void ForNoOpenWarnAndReturnDefault<T>(T resultDefault, T someDefault, ReadValueCodeByRef<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = "noblock";
            T result = default;

            var reader = Create.Reader(inputString);
            AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = Wrapper(reader, default, codeDoingReading); });
            Assert.AreEqual(default, result);

            reader = Create.Reader(inputString);
            AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = Wrapper(reader, someDefault, codeDoingReading); });
            Assert.AreEqual(resultDefault, result);
        }

        public static void ForNoOpenWarnAndReturnDefault<T, nullableT>
            (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = "noblock";
            T result = default;

            var reader = Create.Reader(inputString);
            AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, default); });
            Assert.AreEqual(default, result);

            reader = Create.Reader(inputString);
            AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, someDefault); });
            Assert.AreEqual(resultDefault, result);
        }

        public static void OnBlockEndReturnDefaultOrWarn<T, nullableT>
            (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            OnBlockEndReturnNiceDefaultAndWarn(resultDefault, someDefault, codeDoingReading);
            OnBlockEndReturnGivenDefault(resultDefault, someDefault, codeDoingReading);
        }

        public static void OnBlockEndReturnNiceDefaultAndWarn<T, nullableT>
            (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = ")";
            T result = default;

            var reader = Create.Reader(inputString);
            AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, default); });
            Assert.AreEqual(default, result);
        }

        public static void OnBlockEndReturnGivenDefault<T, nullableT>
            (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = ")";
            T result = default;

            var reader = Create.Reader(inputString);
            result = codeDoingReading(reader, someDefault);
            Assert.AreEqual(resultDefault, result);
            Assert.AreEqual(")", reader.ReadItem());
        }

        public static void OnBlockEndReturnGivenDefault<T>(T resultDefault, T someDefault, ReadValueCodeByRef<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = ")";
            var reader = Create.Reader(inputString);
            T result = default;
            result = Wrapper(reader, default, codeDoingReading);
            Assert.AreEqual(resultDefault, someDefault);
            Assert.AreEqual(")", reader.ReadItem());
        }

        public static void OnEmptyBlockEndReturnGivenDefault<T, nullableT>
            (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = "()a";
            T result = default;

            var reader = Create.Reader(inputString);
            result = codeDoingReading(reader, someDefault);
            Assert.AreEqual(resultDefault, result);
            Assert.AreEqual("a", reader.ReadItem());
        }

        public static void ReturnValueInBlock<T>
            (T[] testValues, ReadValueCode<T> codeDoingReading)
        {
            string[] inputValues = testValues.Select(
                value => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
            ReturnValueInBlock<T>(testValues, inputValues, codeDoingReading);
        }

        public static void ReturnValueInBlock<T> (T[] testValues, ReadValueCodeByRef<T> codeDoingReading)
        {
            string[] inputValues = testValues.Select(
                value => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
            ReturnValueInBlock<T>(testValues, inputValues, codeDoingReading);
        }

        public static void ReturnValueInBlock<T>
            (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string[] tokenValues = inputValues.Select(
                value => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0})", value)).ToArray();
            string inputString = string.Join(" ", tokenValues);
            var reader = Create.Reader(inputString);

            foreach (T testValue in testValues)
            {
                T result = codeDoingReading(reader);
                Assert.AreEqual(testValue, result);
            }
        }

        public static void ReturnValueInBlock<T> (T[] testValues, string[] inputValues, ReadValueCodeByRef<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string[] tokenValues = inputValues.Select(
                value => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0})", value)).ToArray();
            string inputString = string.Join(" ", tokenValues);
            var reader = Create.Reader(inputString);

            foreach (T testValue in testValues)
            {
                T result = Wrapper(reader, testValue, codeDoingReading);
                Assert.AreEqual(testValue, result);
            }
        }

        public static void ReturnValueInBlockAndSkipRestOfBlock<T>
            (T[] testValues, ReadValueCode<T> codeDoingReading)
        {
            string[] inputValues = testValues.Select(
                value => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
            ReturnValueInBlockAndSkipRestOfBlock<T>(testValues, inputValues, codeDoingReading);
        }

        public static void ReturnValueInBlockAndSkipRestOfBlock<T>
            (T[] testValues, ReadValueCodeByRef<T> codeDoingReading)
        {
            string[] inputValues = testValues.Select(
                value => string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
            ReturnValueInBlockAndSkipRestOfBlock<T>(testValues, inputValues, codeDoingReading);
        }

        public static void ReturnValueInBlockAndSkipRestOfBlock<T>
            (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string[] tokenValues = inputValues.Select(
                value => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0} dummy_token(nested_token))", value)
                ).ToArray();
            string inputString = string.Join(" ", tokenValues);
            var reader = Create.Reader(inputString);

            foreach (T testValue in testValues)
            {
                T result = codeDoingReading(reader);
                Assert.AreEqual(testValue, result);
            }
        }

        public static void ReturnValueInBlockAndSkipRestOfBlock<T>(T[] testValues, string[] inputValues, ReadValueCodeByRef<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string[] tokenValues = inputValues.Select(
                value => string.Format(System.Globalization.CultureInfo.InvariantCulture, "({0} dummy_token(nested_token))", value)
                ).ToArray();
            string inputString = string.Join(" ", tokenValues);
            var reader = Create.Reader(inputString);

            foreach (T testValue in testValues)
            {
                T result = Wrapper(reader, testValue, codeDoingReading);
                Assert.AreEqual(testValue, result);
            }
        }

        #endregion

        public delegate T ReadValueCode<T, nullableT>(STFReader reader, nullableT defaultValue);
        public delegate T ReadValueCode<T>(STFReader reader);
        public delegate void ReadValueCodeByRef<T>(STFReader reader, ref T defaultResult);

    }
    #endregion
}
