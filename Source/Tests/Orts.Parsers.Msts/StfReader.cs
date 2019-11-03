// COPYRIGHT 2013, 2014, 2015 by the Open Rails project.
// 
// This file is part of Open Rails.
// 
// Open Rails is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// Open Rails is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with Open Rails.  If not, see <http://www.gnu.org/licenses/>.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using Orts.Formats.Msts.Parsers;
using Xunit;

#region Unit tests
namespace Orts.Tests.Orts.Parsers.Msts.StfReader
{
    // NEW_READER compilation flag is set for those tests that can be performed (compiled) only for the new STFReader, 
    // but not on the old reader. The new reader should also pass all tests that compile on the old reader.
    // This means in this file NEW_READER flag adds a number of tests, but it should also work if the flag is not set.

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

    #region General
    public class Should
    {
        /// <summary>
        /// Test constructor
        /// </summary>
        [Fact]
        public static void BeConstructableFromStream()
        {
            AssertWarnings.NotExpected();
            new STFReader(new MemoryStream(Encoding.ASCII.GetBytes("")), "emptyFile.stf", Encoding.ASCII, true);
        }

        /// <summary>
        /// Test that Dispose is implemented (but not that it is fully functional)
        /// </summary>
        [Fact]
        public static void BeDisposable()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("");
            reader.Dispose();
        }

        [Fact]
        public static void OnDisposeBeforeEOFWarn()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("lasttoken");
            AssertWarnings.Matching("Expected.*end", () => reader.Dispose());
        }

        [Fact]
        public static void ThrowInConstructorOnMissingFile()
        {
            Assert.Throws<DirectoryNotFoundException>(() => new STFReader("somenonexistingfile", false));
        }

        [Fact]
        public static void OnStreamConstructorHasNullSimisSignature()
        {
            AssertWarnings.NotExpected();
            string firstToken = "firsttoken";
            var reader = Create.Reader(firstToken);
            Assert.Equal(null, reader.SimisSignature);
        }

#if NEW_READER
        [Fact]
        public static void BeConstructableFromFilename()
        {
            AssertWarnings.Activate();
            var store = new StreamReaderStore();
            string name = "someName";
            string inputToken = "someToken";
            store.AddReaderFromMemory(name, inputToken);

            STFReader.NextStreamReaderFactory = store;
            var reader = new STFReader(name, true);
            string actualToken = reader.ReadItem();
            Assert.Equal(inputToken, actualToken);
            Assert.Equal(actualToken, reader.Tree); // to be sure the constructor initializes everything correctly            
        }

        [Fact]
        public static void OnFileConstructorHasNullSimisSignatureByDefault()
        {
            AssertWarnings.Activate();

            var store = new StreamReaderStore();
            store.ShouldReadSimisSignature = false;
            string name = "someName";
            string firstToken = "firsttoken";
            store.AddReaderFromMemory(name, firstToken);

            STFReader.NextStreamReaderFactory = store;
            var reader = new STFReader(name, false);

            Assert.Equal(firstToken, reader.ReadItem());
            Assert.Equal(1, reader.LineNumber);
            Assert.Equal(null, reader.SimisSignature);
        }

        [Fact]
        public static void OnFileConstructorHasSimisSignature()
        {
            AssertWarnings.Activate();

            var store = new StreamReaderStore();
            store.ShouldReadSimisSignature = true;
            string name = "someName";
            string signature = "SimisSignature";
            string firstToken = "firsttoken";
            store.AddReaderFromMemory(name, signature + "\n" + firstToken);

            STFReader.NextStreamReaderFactory = store;
            var reader = new STFReader(name, false);

            Assert.Equal(firstToken, reader.ReadItem());
            Assert.Equal(2, reader.LineNumber);
            Assert.Equal(signature, reader.SimisSignature);
        }
#endif
    }
    #endregion

    #region Tokenizer
    public class OnReadingItemShould
    {
        [Fact]
        public static void ReadSingleItem()
        {
            AssertWarnings.NotExpected();
            string item = "sometoken";
            var reader = Create.Reader(item);
            Assert.Equal(item, reader.ReadItem());
        }

        [Fact]
        public static void ReadSingleItems()
        {
            AssertWarnings.NotExpected();
            var singleItems = new string[] { "a", "b", "c", "(", ")", "aa" };
            foreach (string item in singleItems)
            {
                var reader = Create.Reader(item);
                Assert.Equal(item, reader.ReadItem());
            }
        }

        [Fact]
        public static void LetWhiteSpaceSeparateTokens()
        {
            AssertWarnings.NotExpected();
            var tokenTesters = new List<TokenTester>();
            tokenTesters.Add(new TokenTester("a b", new string[] { "a", "b" }));
            tokenTesters.Add(new TokenTester("a   b", new string[] { "a", "b" }));
            tokenTesters.Add(new TokenTester("a\nb", new string[] { "a", "b" }));
            tokenTesters.Add(new TokenTester("a \n\t b", new string[] { "a", "b" }));
            tokenTesters.Add(new TokenTester("aa bb", new string[] { "aa", "bb" }));
            tokenTesters.Add(new TokenTester("aa b\nc", new string[] { "aa", "b", "c" }));

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.inputString);
                foreach (string expectedToken in tokenTester.expectedTokens)
                {
                    Assert.Equal(expectedToken, reader.ReadItem());
                }
            }
        }

        [Fact]
        public static void RecognizeSpecialChars()
        {
            AssertWarnings.NotExpected();
            List<TokenTester> tokenTesters = new List<TokenTester>();
            tokenTesters.Add(new TokenTester("(a", new string[] { "(", "a" }));
            tokenTesters.Add(new TokenTester(")a", new string[] { ")", "a" }));
            tokenTesters.Add(new TokenTester("a(", new string[] { "a", "(" }));
            tokenTesters.Add(new TokenTester("aa ( (", new string[] { "aa", "(", "(" }));
            tokenTesters.Add(new TokenTester("(\ncc\n(", new string[] { "(", "cc", "(" }));

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.inputString);
                foreach (string expectedToken in tokenTester.expectedTokens)
                {
                    Assert.Equal(expectedToken, reader.ReadItem());
                }
            }
        }

        [Fact]
        public static void RecognizeLiteralStrings()
        {
            AssertWarnings.NotExpected();
            List<TokenTester> tokenTesters = new List<TokenTester>();
            tokenTesters.Add(new TokenTester("\"a\"", new string[] { "a" }));
            tokenTesters.Add(new TokenTester("\"aa\"", new string[] { "aa" }));
            tokenTesters.Add(new TokenTester("\"a a\"", new string[] { "a a" }));
            tokenTesters.Add(new TokenTester("\"a a\"b", new string[] { "a a", "b" }));
            tokenTesters.Add(new TokenTester("\"a a\" b", new string[] { "a a", "b" }));
            tokenTesters.Add(new TokenTester("\"a a\"\nb", new string[] { "a a", "b" }));
            tokenTesters.Add(new TokenTester("\"a\na\"\nb", new string[] { "a\na", "b" }));
            tokenTesters.Add(new TokenTester("\"a\ta\"\nb", new string[] { "a\ta", "b" }));
            tokenTesters.Add(new TokenTester("\"\\\"\"b", new string[] { "\"", "b" }));

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.inputString);
                foreach (string expectedToken in tokenTester.expectedTokens)
                {
                    Assert.Equal(expectedToken, reader.ReadItem());
                }
            }
        }

        [Fact]
        public static void RecognizeEscapeCharInLiteralStrings()
        {
            AssertWarnings.NotExpected();
            List<TokenTester> tokenTesters = new List<TokenTester>();
            tokenTesters.Add(new TokenTester(@"""a\na"" b", new string[] { "a\na", "b" }));
            tokenTesters.Add(new TokenTester(@"""c\tc"" d", new string[] { "c\tc", "d" }));

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.inputString);
                foreach (string expectedToken in tokenTester.expectedTokens)
                {
                    Assert.Equal(expectedToken, reader.ReadItem());
                }
            }
        }

        [Fact]
        public static void OnIncompleteLiteralStringAtEOFWarnAndGiveResult()
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
            Assert.Equal(tokenToBeRead, returnedItem);
        }

        [Fact]
        public static void AllowTrailingDoubleQuote()
        {   // This fixes bug 1197917, even though it is a workaround for bad files
            AssertWarnings.NotExpected();
            string tokenToBeRead = "sometoken";
            string followingToken = "following";
            string inputString = String.Format(" {0}\" {1}", tokenToBeRead, followingToken);
            var reader = Create.Reader(inputString);
            Assert.Equal(tokenToBeRead, reader.ReadItem());
            Assert.Equal(followingToken, reader.ReadItem());
        }

        [Fact]
        public static void AtFinalWhiteSpaceBeAtEOF()
        {
            AssertWarnings.NotExpected();
            var inputStrings = new string[] { " ", "\n  \n\t" };
            foreach (string inputString in inputStrings)
            {
                var reader = Create.Reader("sometoken" + inputString);
                {
                    reader.ReadItem();
                    Assert.True(reader.Eof);
                }
            }
        }

        [Fact]
        public static void AtEOFKeepBeingAtEOF()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("lasttoken");
            reader.ReadItem();
            reader.ReadItem();
            Assert.True(reader.Eof);
        }

        [Fact]
        public static void AtEOFKeepReturningEmptyString()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("lasttoken");
            reader.ReadItem();
            Assert.Equal(String.Empty, reader.ReadItem());
            Assert.Equal(String.Empty, reader.ReadItem());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <remarks>Old STFReader has a different way of reading lineNumbers and will fail here</remarks>
        [Fact]
        public static void StoreSourceLineNumberOfLastReadToken()
        {
            //AssertWarnings.Activate();
            List<TokenTester> tokenTesters = new List<TokenTester>();
            //tokenTesters.Add(new TokenTester("a b", new int[] { 1, 1 }));
            tokenTesters.Add(new TokenTester("a\nb", new int[] { 1, 2 }));
            //tokenTesters.Add(new TokenTester("a\nb\nc", new int[] { 1, 2, 3 }));
            //tokenTesters.Add(new TokenTester("a b\n\nc", new int[] { 1, 1, 3 }));
            //tokenTesters.Add(new TokenTester("a(b(\nc)\nc)", new int[] { 1, 1, 1, 1, 2, 2, 3, 3 }));

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.inputString);
                foreach (int expectedLineNumber in tokenTester.expectedLineNumbers)
                {
                    reader.ReadItem();
                    Assert.Equal(expectedLineNumber, reader.LineNumber);
                }
            }
        }

        [Fact]
        public static void StoreLastSourceLineNumber()
        {
            AssertWarnings.NotExpected();
            List<TokenTester> tokenTesters = new List<TokenTester>();
            tokenTesters.Add(new TokenTester("a b", new int[2] { 1, 1 }));
            tokenTesters.Add(new TokenTester("a\nb", new int[2] { 1, 2 }));
            tokenTesters.Add(new TokenTester("a\nb\nc", new int[3] { 1, 2, 3 }));
            tokenTesters.Add(new TokenTester("a b\n\nc", new int[3] { 1, 1, 3 }));
            tokenTesters.Add(new TokenTester("a(b(\nc)\nc)", new int[8] { 1, 1, 1, 1, 2, 2, 3, 3 }));

            foreach (var tokenTester in tokenTesters)
            {
                var reader = Create.Reader(tokenTester.inputString);
                foreach (int expectedLineNumber in tokenTester.expectedLineNumbers)
                {
                    reader.ReadItem();
                }
                int lastLineNumber = tokenTester.expectedLineNumbers[tokenTester.expectedLineNumbers.Count() - 1];
                Assert.Equal(lastLineNumber, reader.LineNumber);
                reader.ReadItem();
                Assert.Equal(lastLineNumber, reader.LineNumber);
            }
        }

        [Fact]
        public static void StoreFileName()
        {
            AssertWarnings.NotExpected();
            string[] fileNames = new string[] { "test1", "otherfile.stf" };
            string someThreeItemInput = "a b c";
            foreach (string fileName in fileNames)
            {
                var reader = Create.Reader(someThreeItemInput, fileName);
                reader.ReadItem();
                Assert.Equal(fileName, reader.FileName);
                reader.ReadItem();
                Assert.Equal(fileName, reader.FileName);
                reader.ReadItem();
                Assert.Equal(fileName, reader.FileName);
            }
        }

    }

    #region Concatenation
    public class OnReadingItemWithPlusShould
    {
        [Fact]
        public static void ConcatenateTwoLiteralTokens()
        {
            AssertWarnings.NotExpected();
            string inputString = "\"a\" + \"b\"";
            var reader = Create.Reader(inputString);
            Assert.Equal("ab", reader.ReadItem());
        }

        [Fact]
        public static void NotConcatenateAfterNormalToken()
        {
            AssertWarnings.NotExpected();
            string inputString = "a + b";
            var reader = Create.Reader(inputString);
            Assert.Equal("a", reader.ReadItem());
        }

        [Fact]
        public static void ConcatenateThreeLiteralTokens()
        {
            AssertWarnings.NotExpected();
            string inputString = "\"a\" + \"b\" + \"c\"";
            var reader = Create.Reader(inputString);
            Assert.Equal("abc", reader.ReadItem());
        }

        [Fact]
        public static void WarnOnNormalTokenAfterConcatenation()
        {
            AssertWarnings.NotExpected();
            string result = String.Empty;
            string inputString = "\"a\" + b";

            AssertWarnings.Matching("started.*double.*quote.*next.*must", () =>
            {
                var reader = Create.Reader(inputString);
                result = reader.ReadItem();
            });
            Assert.Equal("a", result);
        }
    }
    #endregion
    #endregion

    #region Tree
    public class OnNotUsingTreeShould
    {
        [Fact]
        public static void OnCallingTreeThrowSomething()
        {
            AssertWarnings.Expected(); // there might be a debug.assert
            var reader = Create.Reader("wagon(Lights");
            reader.ReadItem();
            Exception exception = Record.Exception(() => { var dummy = reader.Tree; });
            Assert.NotNull(exception);
        }
    }

    public class OnUsingTreeShould
    {
        [Fact]
        public static void BuildTreeString()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("wagon(Lights)engine", true);

            reader.ReadItem();
            Assert.Equal("wagon", reader.Tree.ToLower());
            reader.ReadItem();
            reader.ReadItem();
            Assert.Equal("wagon(lights", reader.Tree.ToLower());
            reader.ReadItem();
            reader.ReadItem();
            Assert.Equal("engine", reader.Tree.ToLower());
        }

        [Fact]
        public static void ContainClosingBracket()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("wagon(Lights())engine", true);

            reader.ReadItem(); // 'wagon'
            reader.ReadItem(); // '('
            reader.ReadItem(); // 'Lights'
            reader.ReadItem(); // '('
            reader.ReadItem(); // ')'
            Assert.Equal("wagon()", reader.Tree.ToLower());
            reader.ReadItem(); // ')'
            Assert.Equal(")", reader.Tree.ToLower());
            reader.ReadItem(); // ')'
            Assert.Equal("engine", reader.Tree.ToLower());
        }
    }
    #endregion

    #region Block handling
    public class BlockHandlingShould
    {
        [Fact]
        public static void SkipRestOfBlockAtBlockClose()
        {
            AssertWarnings.NotExpected();
            string someTokenAfterBlock = "a";
            var reader = Create.Reader(")" + someTokenAfterBlock);
            reader.SkipRestOfBlock();
            Assert.Equal(someTokenAfterBlock, reader.ReadItem());
        }

        [Fact]
        public static void SkipRestOfBlockBeforeBlockClose()
        {
            AssertWarnings.NotExpected();
            string someTokenAfterBlock = "a";
            var reader = Create.Reader("b)" + someTokenAfterBlock);
            reader.SkipRestOfBlock();
            Assert.Equal(someTokenAfterBlock, reader.ReadItem());
        }

        [Fact]
        public static void SkipRestOfBlockForNestedblocks()
        {
            AssertWarnings.NotExpected();
            string someTokenAfterBlock = "a";
            var reader = Create.Reader("b(c))" + someTokenAfterBlock);
            reader.SkipRestOfBlock();
            Assert.Equal(someTokenAfterBlock, reader.ReadItem());
        }

        [Fact]
        public static void OnInclompleteRestOfBlockJustReturn()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("(b)");
            reader.SkipRestOfBlock();
            Assert.True(reader.Eof);
        }

        [Fact]
        public static void SkipBlock()
        {
            string someTokenAfterBlock = "a";
            var reader = Create.Reader("(c)" + someTokenAfterBlock);
            reader.SkipBlock();
            Assert.Equal(someTokenAfterBlock, reader.ReadItem());
        }

        [Fact]
        public static void SkipNestedBlock()
        {
            string someTokenAfterBlock = "a";
            var reader = Create.Reader("(c(b)d)" + someTokenAfterBlock);
            reader.SkipBlock();
            Assert.Equal(someTokenAfterBlock, reader.ReadItem());
        }

        [Fact]
        public static void OnSkipBlockGettingImmediateCloseWarn()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader(")");
            AssertWarnings.Matching("Found a close.*expected block of data", () => reader.SkipBlock());
            Assert.Equal(")", reader.ReadItem());
        }

        [Fact]
        public static void OnSkipBlockNotStartingWithOpenThrow()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("a");
            AssertStfException.Throws(() => reader.SkipBlock(), "expected an open block");
        }

        [Fact]
        public static void OnIncompleteSkipBlockJustReturn()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("(a");
            reader.SkipBlock();
            Assert.True(reader.Eof);
        }

        [Fact]
        public static void NotBeAtEndOfBlockAfterNormalToken()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("sometoken sometoken2");
            Assert.False(reader.EndOfBlock());
            reader.ReadItem();
            Assert.False(reader.EndOfBlock());
        }

        [Fact]
        public static void BeAtEndOfBlockAtEOF()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader("");
            Assert.True(reader.EndOfBlock());
        }

        [Fact]
        public static void BeAtEndOfBlockAtClose()
        {
            AssertWarnings.NotExpected();
            var reader = Create.Reader(") nexttoken");
            Assert.True(reader.EndOfBlock());
        }

        [Fact]
        public static void AtEndOfBlockConsumeCloseMarker()
        {
            AssertWarnings.NotExpected();
            string followuptoken = "sometoken";
            var reader = Create.Reader(")" + followuptoken + " " + followuptoken); // should be at EOF

            Assert.True(reader.EndOfBlock());
            Assert.Equal(followuptoken, reader.ReadItem());
            Assert.False(reader.EndOfBlock());
        }
    }
    #endregion

    #region MustMatch
    public class OnMustMatchShould
    {
        [Fact]
        public static void MatchSimpleStrings()
        {
            AssertWarnings.NotExpected();
            string matchingToken = "a";
            string someTokenAfterMatch = "b";

            var reader = Create.Reader(matchingToken + " " + someTokenAfterMatch);
            reader.MustMatch(matchingToken);
            Assert.Equal(someTokenAfterMatch, reader.ReadItem());
        }

        [Fact]
        public static void MatchOpenBracket()
        {
            AssertWarnings.NotExpected();
            string matchingToken = "(";
            string someTokenAfterMatch = "b";

            var reader = Create.Reader(matchingToken + someTokenAfterMatch);
            reader.MustMatch(matchingToken);
            Assert.Equal(someTokenAfterMatch, reader.ReadItem());
        }

        [Fact]
        public static void MatchCloseBracket()
        {
            AssertWarnings.NotExpected();
            string matchingToken = ")";
            string someTokenAfterMatch = "b";

            var reader = Create.Reader(matchingToken + someTokenAfterMatch);
            reader.MustMatch(matchingToken);
            Assert.Equal(someTokenAfterMatch, reader.ReadItem());
        }

        [Fact]
        public static void MatchWhileIgnoringCase()
        {
            AssertWarnings.NotExpected();
            string matchingToken = "somecase";
            string matchingTokenOtherCase = "SomeCase";
            string someTokenAfterMatch = "b";

            var reader = Create.Reader(matchingTokenOtherCase + " " + someTokenAfterMatch);
            reader.MustMatch(matchingToken);
            Assert.Equal(someTokenAfterMatch, reader.ReadItem());
        }

        [Fact]
        public static void WarnOnSingleMissingMatch()
        {
            AssertWarnings.NotExpected();
            string tokenToMatch = "a";
            string someotherToken = "b";
            var reader = Create.Reader(someotherToken + " " + tokenToMatch);
            AssertWarnings.Matching("not found.*instead", () => reader.MustMatch(tokenToMatch));
        }

        [Fact]
        public static void ThrowOnDoubleMissingMatch()
        {
            AssertWarnings.Expected();  // we first expect a warning, only after that an error
            string tokenToMatch = "a";
            string someotherToken = "b";
            var reader = Create.Reader(someotherToken + " " + someotherToken);
            AssertStfException.Throws(() => reader.MustMatch(tokenToMatch), "not found.*instead");
        }

        [Fact]
        public static void WarnOnEofDuringMatch()
        {
            AssertWarnings.NotExpected();
            string tokenToMatch = "a";
            var reader = Create.Reader("");
            AssertWarnings.Matching("Unexpected end of file instead", () => reader.MustMatch(tokenToMatch));

        }
    }
    #endregion

    #region Preprocessing comments/skip, include
    #region Comments/skip
    public class PreprocessingShould
    {
        [Fact]
        public static void SkipBlockOnComment()
        {
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            var reader = Create.Reader("comment(a)" + someFollowingToken);
            Assert.Equal(someFollowingToken, reader.ReadItem());
        }

        [Fact]
        public static void SkipBlockOnCommentOtherCase()
        {
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            var reader = Create.Reader("Comment(a)" + someFollowingToken);
            Assert.Equal(someFollowingToken, reader.ReadItem());
        }
        
        [Fact]
        public static void WarnOnMissingBlockAfterComment()
        {
            // todo: old stf reader would simply return 'b'. Throwing an exception is perhaps harsh
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            AssertStfException.Throws(() => 
            {
                var reader = Create.Reader("comment a " + someFollowingToken);
                Assert.Equal(someFollowingToken, reader.ReadItem());
            }, "expected.*open");
        }

        [Fact]
        public static void SkipBlockOnSkip()
        {
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            var reader = Create.Reader("skip(a)" + someFollowingToken);
            Assert.Equal(someFollowingToken, reader.ReadItem());
        }

        [Fact]
        public static void SkipBlockOnSkipOtherCase()
        {
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            var reader = Create.Reader("Skip(a)" + someFollowingToken);
            Assert.Equal(someFollowingToken, reader.ReadItem());
        }

        [Fact]
        public static void WarnOnMissingBlockAfterSkip()
        {
            // todo: old stf reader would simply return 'b'. Throwing an exception is perhaps harsh
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            AssertStfException.Throws(() =>
            {
                var reader = Create.Reader("skip a " + someFollowingToken);
                Assert.Equal(someFollowingToken, reader.ReadItem());
            }, "expected.*open");
        }

        [Fact]
        public static void SkipBlockOnTokenStartingWithHash()
        {
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            var reader = Create.Reader("#token(a)" + someFollowingToken);
            Assert.Equal(someFollowingToken, reader.ReadItem());
        }

        [Fact]
        public static void SkipSingleItemOnTokenStartingWithHash()
        {
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            var reader = Create.Reader("#token a " + someFollowingToken);
            Assert.Equal(someFollowingToken, reader.ReadItem());
        }

        [Fact]
        public static void WarnOnEofAfterHashToken()
        {
            AssertWarnings.NotExpected();
            AssertWarnings.Matching("a # marker.*EOF", () =>
            {
                var reader = Create.Reader("#sometoken");
                reader.ReadItem();
            });
        }

        [Fact]
        public static void SkipBlockOnTokenStartingWithUnderscore()
        {
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            var reader = Create.Reader("_token(a)" + someFollowingToken);
            Assert.Equal(someFollowingToken, reader.ReadItem());
        }

        [Fact]
        public static void SkipSingleItemOnTokenStartingWithUnderscore()
        {
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            var reader = Create.Reader("_token a " + someFollowingToken);
            Assert.Equal(someFollowingToken, reader.ReadItem());
        }

        [Fact]
        public static void SkipBlockDisregardsNestedComment()
        {
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            var reader = Create.Reader("comment(a comment( c) skip _underscore #hash)" + someFollowingToken);
            Assert.Equal(someFollowingToken, reader.ReadItem());
        }

        [Fact]
        public static void SkipBlockDisregardsNestedInclude()
        {
            AssertWarnings.NotExpected();
            string someFollowingToken = "b";
            var reader = Create.Reader("comment(a include c )" + someFollowingToken);
            Assert.Equal(someFollowingToken, reader.ReadItem());
        }
    }
    #endregion

    #region Include
#if NEW_READER
    public class OnIncludeShould
    {
        [Fact]
        public static void IncludeNamedStream()
        {
            AssertWarnings.Activate();

            string name = "somename";
            string includedSingleTokenInput = "a";
            var store = new StreamReaderStore();
            store.AddReaderFromMemory(name, includedSingleTokenInput);

            string followingToken = "b";
            string inputString = "include ( " + name + ") " + followingToken;

            var reader = Create.Reader(inputString, store);
            Assert.Equal(includedSingleTokenInput, reader.ReadItem());
            Assert.Equal(followingToken, reader.ReadItem());
        }

        [Fact]
        public static void AllowCapitalizedInclude()
        {
            AssertWarnings.Activate();

            string name = "somename";
            string includedSingleTokenInput = "a";
            var store = new StreamReaderStore();
            store.AddReaderFromMemory(name, includedSingleTokenInput);

            string followingToken = "b";
            string inputString = "Include ( " + name + ") " + followingToken;

            var reader = Create.Reader(inputString, store);
            Assert.Equal(includedSingleTokenInput, reader.ReadItem());
            Assert.Equal(followingToken, reader.ReadItem());
        }

        [Fact]
        public static void AllowNestedIncludes()
        {
            AssertWarnings.Activate();

            string name2 = "name2";
            string name3 = "name3";
            var store = new StreamReaderStore();
            store.AddReaderFromMemory(name3, "c");
            store.AddReaderFromMemory(name2, "b1 include ( " + name3 + ") b2");

            var reader = Create.Reader("a1 include ( " + name2 + ") a2", store);
            Assert.Equal("a1", reader.ReadItem());
            Assert.Equal("b1", reader.ReadItem());
            Assert.Equal("c", reader.ReadItem());
            Assert.Equal("b2", reader.ReadItem());
            Assert.Equal("a2", reader.ReadItem());
        }

        [Fact]
        public static void AllowNestedIncludesRelativePath()
        {
            AssertWarnings.Activate();

            string basedir = @"C:basedir\";
            string fullname1 = basedir + "name1";
            string relname2 = @"subdir\name2";
            string fullname2 = basedir + relname2;
            string relname3 = "name3";
            string fullname3 = basedir + @"subdir\" + relname3;
            string relname4 = "name4";
            string fullname4 = basedir + relname4;

            var store = new StreamReaderStore();
            var reader = Create.Reader(
                String.Format("a1 include ( {0} ) a2 include ( {1} ) a3", relname2, relname4)
                , fullname1, store);
            store.AddReaderFromMemory(fullname2, String.Format("b1 include ( {0} ) b2", relname3));
            store.AddReaderFromMemory(fullname3, "c1");
            store.AddReaderFromMemory(fullname4, "d1");
            
            Assert.Equal("a1", reader.ReadItem());
            Assert.Equal("b1", reader.ReadItem());
            Assert.Equal("c1", reader.ReadItem());
            Assert.Equal("b2", reader.ReadItem());
            Assert.Equal("a2", reader.ReadItem());
            Assert.Equal("d1", reader.ReadItem());
            Assert.Equal("a3", reader.ReadItem());
        }

        [Fact]
        public static void ThrowOnIncompleteIncludeBlock()
        {
            AssertWarnings.ExpectAWarning();
            AssertStfException.Throws(() => { var reader = Create.Reader("include "); }, "Unexpected end of file");
            AssertStfException.Throws(() => { var reader = Create.Reader("include ( "); }, "Unexpected end of file");
            AssertStfException.Throws(() => { var reader = Create.Reader("include ( somename"); }, "Unexpected end of file");

        }

    }
#endif
    #endregion
    #endregion

    #region Reading values, numbers, ...
    #region Common test utilities
    class StfTokenReaderCommon
    {
        #region Value itself
        public static void OnNoValueWarnAndReturnDefault<T, nullableT>
            (T niceDefault, T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = ")";
            T result = default(T);

            var reader = Create.Reader(inputString);
            AssertWarnings.Matching("Cannot parse|expecting.*found.*[)]", () => { result = codeDoingReading(reader, default(nullableT)); });
            Assert.Equal(niceDefault, result);
            Assert.Equal(")", reader.ReadItem());

            reader = Create.Reader(inputString);
            AssertWarnings.Matching("expecting.*found.*[)]", () => { result = codeDoingReading(reader, someDefault); });
            Assert.Equal(resultDefault, result);
            Assert.Equal(")", reader.ReadItem());
        }

        public static void ReturnValue<T>
            (T[] testValues, ReadValueCode<T> codeDoingReading)
        {
            string[] inputValues = testValues.Select(
                value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
            ReturnValue<T>(testValues, inputValues, codeDoingReading);
        }

        public static void ReturnValue<T>
            (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string inputString = String.Join(" ", inputValues);
            var reader = Create.Reader(inputString);
            foreach (T testValue in testValues)
            {
                T result = codeDoingReading(reader);
                Assert.Equal(testValue, result);
            }
        }

        public static void WithCommaReturnValue<T>
            (T[] testValues, ReadValueCode<T> codeDoingReading)
        {
            string[] inputValues = testValues.Select(
                value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
            WithCommaReturnValue(testValues, inputValues, codeDoingReading);
        }

        public static void WithCommaReturnValue<T>
            (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string inputString = String.Join(" ", inputValues);
            var reader = Create.Reader(inputString);
            foreach (T testValue in testValues)
            {
                T result = codeDoingReading(reader);
                Assert.Equal(testValue, result);
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

            T result = codeDoingReading(reader, default(nullableT));
            Assert.Equal(niceDefault, result);

            result = codeDoingReading(reader, someDefault);
            Assert.Equal(niceDefault, result);
        }

        public static void ForNonNumbersReturnNiceDefaultAndWarn<T, nullableT>
            (T niceDefault, T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string[] testValues = new string[] { "noint", "sometoken", "(" };
            string inputString = String.Join(" ", testValues);

            var reader = Create.Reader(inputString);
            foreach (string testValue in testValues)
            {
                T result = default(T);
                AssertWarnings.Matching("Cannot parse", () => { result = codeDoingReading(reader, default(nullableT)); });
                Assert.Equal(niceDefault, result);
            }

            reader = Create.Reader(inputString);
            foreach (string testValue in testValues)
            {
                T result = default(T);
                AssertWarnings.Matching("Cannot parse", () => { result = codeDoingReading(reader, someDefault); });
                Assert.Equal(resultDefault, result);

            }
        }
        #endregion

        #region Value in blocks
        public static void OnEofWarnAndReturnDefault<T, nullableT>
            (T resultDefault, ref nullableT someDefault, ReadValueCodeByRef<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = "";
            var reader = Create.Reader(inputString);
            T result = default(T);
            codeDoingReading(reader, ref someDefault);
            //AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, ref default(nullableT)); });
            //Assert.Equal(default(T), result);
            //AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, ref someDefault); });
            Assert.Equal(resultDefault, result);
        }

        public static void OnEofWarnAndReturnDefault<T, nullableT>
            (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = "";
            var reader = Create.Reader(inputString);
            T result = default(T);
            AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, default(nullableT)); });
            Assert.Equal(default(T), result);
            AssertWarnings.Matching("Unexpected end of file", () => { result = codeDoingReading(reader, someDefault); });
            Assert.Equal(resultDefault, result);
        }

        public static void ForNoOpenWarnAndReturnDefault<T, nullableT>
            (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = "noblock";
            T result = default(T);

            var reader = Create.Reader(inputString);
            AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, default(nullableT)); });
            Assert.Equal(default(T), result);

            reader = Create.Reader(inputString);
            AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, someDefault); });
            Assert.Equal(resultDefault, result);
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
            T result = default(T);

            var reader = Create.Reader(inputString);
            AssertWarnings.Matching("Block [nN]ot [fF]ound", () => { result = codeDoingReading(reader, default(nullableT)); });
            Assert.Equal(default(T), result);
        }

        public static void OnBlockEndReturnGivenDefault<T, nullableT>
            (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = ")";
            T result = default(T);

            var reader = Create.Reader(inputString);
            result = codeDoingReading(reader, someDefault);
            Assert.Equal(resultDefault, result);
            Assert.Equal(")", reader.ReadItem());
        }

        public static void OnEmptyBlockEndReturnGivenDefault<T, nullableT>
            (T resultDefault, nullableT someDefault, ReadValueCode<T, nullableT> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            var inputString = "()a";
            T result = default(T);

            var reader = Create.Reader(inputString);
            result = codeDoingReading(reader, someDefault);
            Assert.Equal(resultDefault, result);
            Assert.Equal("a", reader.ReadItem());
        }

        public static void ReturnValueInBlock<T>
            (T[] testValues, ReadValueCode<T> codeDoingReading)
        {
            string[] inputValues = testValues.Select(
                value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
            ReturnValueInBlock<T>(testValues, inputValues, codeDoingReading);
        }

        public static void ReturnValueInBlock<T>
            (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string[] tokenValues = inputValues.Select(
                value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "({0})", value)).ToArray();
            string inputString = String.Join(" ", tokenValues);
            var reader = Create.Reader(inputString);

            foreach (T testValue in testValues)
            {
                T result = codeDoingReading(reader);
                Assert.Equal(testValue, result);
            }
        }

        public static void ReturnValueInBlockAndSkipRestOfBlock<T>
            (T[] testValues, ReadValueCode<T> codeDoingReading)
        {
            string[] inputValues = testValues.Select(
                value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "{0}", value)).ToArray();
            ReturnValueInBlockAndSkipRestOfBlock<T>(testValues, inputValues, codeDoingReading);
        }

        public static void ReturnValueInBlockAndSkipRestOfBlock<T>
            (T[] testValues, string[] inputValues, ReadValueCode<T> codeDoingReading)
        {
            AssertWarnings.NotExpected();
            string[] tokenValues = inputValues.Select(
                value => String.Format(System.Globalization.CultureInfo.InvariantCulture, "({0} dummy_token(nested_token))", value)
                ).ToArray();
            string inputString = String.Join(" ", tokenValues);
            var reader = Create.Reader(inputString);

            foreach (T testValue in testValues)
            {
                T result = codeDoingReading(reader);
                Assert.Equal(testValue, result);
            }
        }

        #endregion

        public delegate T ReadValueCode<T, nullableT>(STFReader reader, nullableT defaultValue);
        public delegate T ReadValueCode<T>(STFReader reader);
        public delegate void ReadValueCodeByRef<T, nullableT>(STFReader reader, ref nullableT defaultResult);

    }
    #endregion

    #region string
    public class OnReadingStringShould
    {
        [Fact]
        public static void ReturnValue()
        {
            AssertWarnings.NotExpected();
            string[] testValues = new string[] { "token", "somestring", "lights" };
            string inputString = String.Join(" ", testValues);
            var reader = Create.Reader(inputString);

            foreach (string testValue in testValues)
            {
                Assert.Equal(testValue, reader.ReadString());
            }
        }

        [Fact]
        public static void SkipValueStartingWithUnderscore()
        {
            AssertWarnings.NotExpected();
            string underscoreToken = "_underscore";
            string toBeSkippedToken = "tobeskippedtoken";
            string followingToken = "followingtoken";
            string inputString = underscoreToken + " " + toBeSkippedToken + " " + followingToken;
            var reader = Create.Reader(inputString);
            // todo spec change?: old STF reader would return _underscore and tobeskippedtoken"
            //Assert.Equal(underscoreToken, reader.ReadString());
            //Assert.Equal(toBeSkippedToken, reader.ReadString());
            Assert.Equal(followingToken, reader.ReadString());

        }
    }

    public class OnReadingStringBlockShould
    {
        static readonly string SOMEDEFAULT = "a";
        static readonly string[] SOMEDEFAULTS = new string[] { "ss", "SomeThing", "da" };

        [Fact]
        public static void OnEofWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<string, string>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadStringBlock(x));
        }

        [Fact]
        public static void ForNoOpenWarnAndReturnDefault()
        {
            StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<string, string>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadStringBlock(x));
        }

        [Fact]
        public static void OnBlockEndReturnDefaultOrWarn()
        {
            StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<string, string>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadStringBlock(x));
        }

        [Fact]
        public static void ReturnValueInBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlock<string>(SOMEDEFAULTS, reader => reader.ReadStringBlock(null));
        }

        [Fact]
        public static void ReturnValueInBlockAndSkipRestOfBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<string>(SOMEDEFAULTS, reader => reader.ReadStringBlock(null));
        }

    }
    #endregion

    #region int
    public class OnReadingIntShould
    {
        static readonly int NICEDEFAULT = default(int);
        static readonly int SOMEDEFAULT = -2;
        static readonly int[] SOMEDEFAULTS = new int[] { 3, 5, -6 };

        [Fact]
        public static void OnNoValueWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnNoValueWarnAndReturnDefault<int, int?>
                (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadInt(x));
        }

        [Fact]
        public static void ReturnValue()
        {
            StfTokenReaderCommon.ReturnValue<int>(SOMEDEFAULTS, reader => reader.ReadInt(null));
        }

        [Fact]
        public static void ReturnValueWithSign()
        {
            string[] inputs = { "+2", "-3" };
            int[] expected = { 2, -3 };
            StfTokenReaderCommon.ReturnValue<int>(expected, inputs, reader => reader.ReadInt(null));
        }

        [Fact]
        public static void WithCommaReturnValue()
        {
            StfTokenReaderCommon.WithCommaReturnValue<int>(SOMEDEFAULTS, reader => reader.ReadInt(null));
        }

        [Fact]
        public static void ForEmptyStringReturnNiceDefault()
        {
            StfTokenReaderCommon.ForEmptyStringReturnNiceDefault<int, int?>
                (NICEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadInt(x));
        }

        [Fact]
        public static void ForNonNumbersReturnZeroAndWarn()
        {
            StfTokenReaderCommon.ForNonNumbersReturnNiceDefaultAndWarn<int, int?>
                (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadInt(x));
        }

    }

    public class OnReadingIntBlockShould
    {
        static readonly int SOMEDEFAULT = -2;
        static readonly int[] SOMEDEFAULTS = new int[] { 3, 5, -6 };

        [Fact]
        public static void OnEofWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<int, int?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadIntBlock(x));
        }

        [Fact]
        public static void ForNoOpenReturnDefaultAndWarn()
        {
            StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<int, int?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadIntBlock(x));
        }

        [Fact]
        public static void OnBlockEndReturnDefaultOrWarn()
        {
            StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<int, int?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadIntBlock(x));
        }

        public static void ReturnValueInBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlock<int>(SOMEDEFAULTS, reader => reader.ReadIntBlock(null));
        }

        [Fact]
        public static void ReturnValueInBlockAndSkipRestOfBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<int>(SOMEDEFAULTS, reader => reader.ReadIntBlock(null));
        }

    }
    #endregion

    #region uint
    public class OnReadingUIntShould
    {
        static readonly uint NICEDEFAULT = default(uint);
        static readonly uint SOMEDEFAULT = 2;
        static readonly uint[] SOMEDEFAULTS = new uint[] { 3, 5, 200 };

        [Fact]
        public static void OnNoValueWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnNoValueWarnAndReturnDefault<uint, uint?>
                (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUInt(x));
        }

        [Fact]
        public static void ReturnValue()
        {
            StfTokenReaderCommon.ReturnValue<uint>(SOMEDEFAULTS, reader => reader.ReadUInt(null));
        }

        [Fact]
        public static void WithCommaReturnValue()
        {
            StfTokenReaderCommon.WithCommaReturnValue<uint>(SOMEDEFAULTS, reader => reader.ReadUInt(null));
        }

        [Fact]
        public static void ForEmptyStringReturnNiceDefault()
        {
            StfTokenReaderCommon.ForEmptyStringReturnNiceDefault<uint, uint?>
                (NICEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUInt(x));
        }

        [Fact]
        public static void ForNonNumbersReturnZeroAndWarn()
        {
            StfTokenReaderCommon.ForNonNumbersReturnNiceDefaultAndWarn<uint, uint?>
                (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUInt(x));
        }
    }

    public class OnReadingUIntBlockShould
    {
        static readonly uint SOMEDEFAULT = 4;
        static readonly uint[] SOMEDEFAULTS = new uint[] { 3, 5, 20 };

        [Fact]
        public static void OnEofWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<uint, uint?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUIntBlock(x));
        }

        [Fact]
        public static void ForNoOpenReturnDefaultAndWarn()
        {
            StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<uint, uint?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUIntBlock(x));
        }

        [Fact]
        public static void OnBlockEndReturnDefaultOrWarn()
        {
            StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<uint, uint?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadUIntBlock(x));
        }

        [Fact]
        public static void ReturnValueInBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlock<uint>(SOMEDEFAULTS, reader => reader.ReadUIntBlock(null));
        }

        [Fact]
        public static void ReturnValueInBlockAndSkipRestOfBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<uint>(SOMEDEFAULTS, reader => reader.ReadUIntBlock(null));
        }
    }
    #endregion

    #region hex
    public class OnReadingHexShould
    {
        static readonly uint NICEDEFAULT = default(uint);
        static readonly uint SOMEDEFAULT = 2;
        static readonly uint[] SOMEDEFAULTS = new uint[] { 3, 5, 129 };
        static readonly string[] STRINGDEFAULTS = new string[] { "0000003", "00000005", "00000081" };

        [Fact]
        public static void OnNoValueWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnNoValueWarnAndReturnDefault<uint, uint?>
                (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHex(x));
        }

        [Fact]
        public static void ReturnValue()
        {
            StfTokenReaderCommon.ReturnValue<uint>(SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadHex(null));
        }

        [Fact]
        public static void WithCommaReturnValue()
        {
            StfTokenReaderCommon.WithCommaReturnValue<uint>(SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadHex(null));
        }

        [Fact]
        public static void ForEmptyStringReturnNiceDefault()
        {
            StfTokenReaderCommon.ForEmptyStringReturnNiceDefault<uint, uint?>
                (NICEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHex(x));
        }

        [Fact]
        public static void ForNonNumbersReturnZeroAndWarn()
        {
            StfTokenReaderCommon.ForNonNumbersReturnNiceDefaultAndWarn<uint, uint?>
                (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHex(x));
        }
    }

    public class OnReadingHexBlockShould
    {
        static readonly uint SOMEDEFAULT = 4;
        static readonly uint[] SOMEDEFAULTS = new uint[] { 3, 5, 20 };
        static readonly string[] STRINGDEFAULTS = new string[] { "00000003", "00000005", "00000014" };

        [Fact]
        public static void OnEofWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<uint, uint?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHexBlock(x));
        }

        [Fact]
        public static void ForNoOpenReturnDefaultAndWarn()
        {
            StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<uint, uint?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHexBlock(x));
        }

        [Fact]
        public static void OnBlockEndReturnDefaultOrWarn()
        {
            StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<uint, uint?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadHexBlock(x));
        }

        [Fact]
        public static void ReturnValueInBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlock<uint>(SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadHexBlock(null));
        }

        [Fact]
        public static void ReturnValueInBlockAndSkipRestOfBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<uint>(SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadHexBlock(null));
        }
    }
    #endregion

    #region bool
    // ReadBool is not supported.
    public class OnReadingBoolBlockShould
    {
        static readonly bool SOMEDEFAULT = false;
        static readonly bool[] SOMEDEFAULTS1 = new bool[] { false, true, false, true, true, true, true  };
        static readonly string[] STRINGDEFAULTS1 = new string[] { "false", "true", "0", "1", "1.1", "-2.9e3", "non"  };

        [Fact]
        public static void OnEofWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<bool, bool>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadBoolBlock(x));
        }

        [Fact]
        public static void ForNoOpenReturnDefaultAndWarn()
        {
            StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<bool, bool>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadBoolBlock(x));
        }

        [Fact]
        public static void OnBlockEndReturnGivenDefault()
        {
            StfTokenReaderCommon.OnBlockEndReturnGivenDefault<bool, bool>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadBoolBlock(x));
        }

        [Fact]
        public static void ReturnStringValueInBlock()
        {
            string[] inputValues = {"true", "false"};
            bool[] expectedValues = {true, false};
            StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(false));
        }

        [Fact]
        public static void ReturnIntValueInBlock()
        {
            string[] inputValues = {"0", "1", "-2"};
            bool[] expectedValues = {false, true, true};
            StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(false));
        }

        [Fact]
        public static void ReturnDefaultValueOtherwiseInBlock()
        {
            bool[] expectedValues;
            string[] inputValues = { "0.1", "1.1", "something", "()" };
            bool expectedValue = false;
            expectedValues = new bool[]{ expectedValue, expectedValue, expectedValue, expectedValue};
            StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(expectedValue));
            
            expectedValue = true;
            expectedValues = new bool[] { expectedValue, expectedValue, expectedValue, expectedValue };
            StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(expectedValue));
        }

        [Fact]
        public static void ReturnStringValueInBlockAndSkipRestOfBlock()
        {
            string[] inputValues = { "true next", "false next" };
            bool[] expectedValues = { true, false };
            StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(false));
        }

        [Fact]
        public static void ReturnIntValueInBlockAndSkipRestOfBlock()
        {
            string[] inputValues = { "0 next", "1 some", "-2 thing" };
            bool[] expectedValues = { false, true, true };
            StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(false));
        }

        [Fact]
        public static void ReturnDefaultValueOtherwiseInBlockAndSkipRestOfBlock()
        {
            string[] inputValues = { "0.1 x", "1.1 y", "something z", "()" };
            bool[] expectedValues = { false, false, false, false };
            StfTokenReaderCommon.ReturnValueInBlock<bool>(expectedValues, inputValues, reader => reader.ReadBoolBlock(false));
        }
        [Fact]
        public static void OnEmptyBlockReturnGivenDefault()
        {
            StfTokenReaderCommon.OnEmptyBlockEndReturnGivenDefault<bool, bool>
               (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadBoolBlock(x));
        }

        [Fact]
        public static void OnNonBoolOrIntInBlockReturnFalseWithoutWarning()
        {
            AssertWarnings.NotExpected();
            string[] testValues = new string[] { "(bool)", "(something)" };
            string inputString = String.Join(" ", testValues);

            bool expectedResult = false;
            var reader = Create.Reader(inputString);
            foreach (string testValue in testValues)
            {
                bool result = ! expectedResult;
                result = reader.ReadBoolBlock(expectedResult);
                Assert.Equal(expectedResult, result);
            }

            expectedResult = true;
            reader = Create.Reader(inputString);
            foreach (string testValue in testValues)
            {
                bool result = !expectedResult;
                result = reader.ReadBoolBlock(expectedResult);
                Assert.Equal(expectedResult, result);
            }
        }
    }
    #endregion

    #region double
    public class OnReadingDoubleShould
    {
        static readonly double NICEDEFAULT = default(double);
        static readonly double SOMEDEFAULT = -2;
        static readonly double[] SOMEDEFAULTS = new double[] { 3, 5, -6 };

        [Fact]
        public static void OnNoValueWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnNoValueWarnAndReturnDefault<double, double?>
                (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDouble(x));
        }

        [Fact]
        public static void ReturnValue()
        {
            StfTokenReaderCommon.ReturnValue<double>(SOMEDEFAULTS, reader => reader.ReadDouble(null));
        }

        [Fact]
        public static void WithCommaReturnValue()
        {
            StfTokenReaderCommon.WithCommaReturnValue<double>(SOMEDEFAULTS, reader => reader.ReadDouble(null));
        }

        [Fact]
        public static void ForEmptyStringReturnNiceDefault()
        {
            StfTokenReaderCommon.ForEmptyStringReturnNiceDefault<double, double?>
                (NICEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDouble(x));
        }

        [Fact]
        public static void ForNonNumbersReturnZeroAndWarn()
        {
            StfTokenReaderCommon.ForNonNumbersReturnNiceDefaultAndWarn<double, double?>
                (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDouble(x));
        }

    }

    public class OnReadingDoubleBlockShould
    {
        static readonly double SOMEDEFAULT = -2;
        static readonly double[] SOMEDEFAULTS = new double[] { 3, 5, -6 };

        [Fact]
        public static void OnEofWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<double, double?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDoubleBlock(x));
        }

        [Fact]
        public static void ForNoOpenReturnDefaultAndWarn()
        {
            StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<double, double?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDoubleBlock(x));
        }

        [Fact]
        public static void OnBlockEndReturnDefaultOrWarn()
        {
            StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<double, double?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadDoubleBlock(x));
        }

        public static void ReturnValueInBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlock<double>(SOMEDEFAULTS, reader => reader.ReadDoubleBlock(null));
        }

        [Fact]
        public static void ReturnValueInBlockAndSkipRestOfBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<double>(SOMEDEFAULTS, reader => reader.ReadDoubleBlock(null));
        }

    }
    #endregion

    #region float
    public class OnReadingFloatShould
    {
        static readonly float NICEDEFAULT = default(float);
        static readonly float SOMEDEFAULT = 2.1f;
        static readonly float[] SOMEDEFAULTS = new float[] { 1.1f, 4.5e6f, -0.01f };

        [Fact]
        public static void OnNoValueWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnNoValueWarnAndReturnDefault<float, float?>
                (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloat(STFReader.Units.None, x));
        }

        [Fact]
        public static void ReturnValue()
        {
            StfTokenReaderCommon.ReturnValue<float>(SOMEDEFAULTS, reader => reader.ReadFloat(STFReader.Units.None, null));
        }

        [Fact]
        public static void WithCommaReturnValue()
        {
            StfTokenReaderCommon.WithCommaReturnValue<float>(SOMEDEFAULTS, reader => reader.ReadFloat(STFReader.Units.None, null));
        }

        [Fact]
        public static void ForEmptyStringReturnNiceDefault()
        {
            StfTokenReaderCommon.ForEmptyStringReturnNiceDefault<float, float?>
                (NICEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloat(STFReader.Units.None, x));
        }

        [Fact]
        public static void ForNonNumbersReturnZeroAndWarn()
        {
            StfTokenReaderCommon.ForNonNumbersReturnNiceDefaultAndWarn<float, float?>
                (NICEDEFAULT, SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloat(STFReader.Units.None, x));
        }

    }

    public class OnReadingFloatBlockShould
    {
        static readonly float SOMEDEFAULT = 2.1f;
        static readonly float[] SOMEDEFAULTS = new float[] { 1.1f, 4.5e6f, -0.01f };

        [Fact]
        public static void OnEofWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<float, float?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloatBlock(STFReader.Units.None, x));
        }

        [Fact]
        public static void ForNoOpenReturnDefaultAndWarn()
        {
            StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<float, float?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloatBlock(STFReader.Units.None, x));
        }

        [Fact]
        public static void OnBlockEndReturnDefaultOrWarn()
        {
            StfTokenReaderCommon.OnBlockEndReturnDefaultOrWarn<float, float?>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadFloatBlock(STFReader.Units.None, x));
        }

        public static void ReturnValueInBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlock<float>
                (SOMEDEFAULTS, reader => reader.ReadFloatBlock(STFReader.Units.None, null));
        }

        [Fact]
        public static void ReturnValueInBlockAndSkipRestOfBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<float>
                (SOMEDEFAULTS, reader => reader.ReadFloatBlock(STFReader.Units.None, null));
        }
    }
    #endregion

    #region Vector2
    public class OnReadingVector2BlockShould
    {
        static readonly Vector2 SOMEDEFAULT = new Vector2(1.1f, 1.2f);
        static readonly Vector2[] SOMEDEFAULTS = new Vector2[] { new Vector2(1.3f, 1.5f), new Vector2(-2f, 1e6f) };
        static readonly string[] STRINGDEFAULTS = new string[] { "1.3 1.5 ignore", "-2 1000000" };

        [Fact]
        public static void OnEofWarnAndReturnDefault()
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

    #region Vector3
    public class OnReadingVector3BlockShould
    {
        static readonly Vector3 SOMEDEFAULT = new Vector3(1.1f, 1.2f, 1.3f);
        static readonly Vector3[] SOMEDEFAULTS = new Vector3[] { new Vector3(1.3f, 1.5f, 1.8f), new Vector3(1e-3f, -2f, -1e3f) };
        static readonly string[] STRINGDEFAULTS = new string[] { "1.3 1.5 1.8 ignore", "0.001, -2, -1000" };


        [Fact]
        public static void OnEofWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<Vector3, Vector3>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector3Block(STFReader.Units.None, x));
        }

        [Fact]
        public static void ForNoOpenReturnDefaultAndWarn()
        {
            StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<Vector3, Vector3>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector3Block(STFReader.Units.None, x));
        }

        [Fact]
        public static void OnBlockEndReturnDefaultWhenGiven()
        {
            StfTokenReaderCommon.OnBlockEndReturnGivenDefault<Vector3, Vector3>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector3Block(STFReader.Units.None, x));
        }

        [Fact]
        public static void ReturnValueInBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlock<Vector3>
                (SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadVector3Block(STFReader.Units.None, Vector3.Zero));
        }

        [Fact]
        public static void ReturnValueInBlockAndSkipRestOfBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<Vector3>
                (SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadVector3Block(STFReader.Units.None, Vector3.Zero));
        }
    }
    #endregion

    #region Vector4
    public class OnReadingVector4BlockShould
    {
        static readonly Vector4 SOMEDEFAULT = new Vector4(1.1f, 1.2f, 1.3f, 1.4f);
        static readonly Vector4[] SOMEDEFAULTS = new Vector4[] { new Vector4(1.3f, 1.5f, 1.7f, 1.9f) };
        static readonly string[] STRINGDEFAULTS = new string[] { "1.3 1.5 1.7 1.9 ignore" };


        [Fact]
        public static void OnEofWarnAndReturnDefault()
        {
            StfTokenReaderCommon.OnEofWarnAndReturnDefault<Vector4, Vector4>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector4Block(STFReader.Units.None, x));
        }

        [Fact]
        public static void ForNoOpenReturnDefaultAndWarn()
        {
            StfTokenReaderCommon.ForNoOpenWarnAndReturnDefault<Vector4, Vector4>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector4Block(STFReader.Units.None, x));
        }

        [Fact]
        public static void OnBlockEndReturnDefaultWhenGiven()
        {
            StfTokenReaderCommon.OnBlockEndReturnGivenDefault<Vector4, Vector4>
                (SOMEDEFAULT, SOMEDEFAULT, (reader, x) => reader.ReadVector4Block(STFReader.Units.None, x));
        }

        [Fact]
        public static void ReturnValueInBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlock<Vector4>
                (SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadVector4Block(STFReader.Units.None, Vector4.Zero));
        }

        [Fact]
        public static void ReturnValueInBlockAndSkipRestOfBlock()
        {
            StfTokenReaderCommon.ReturnValueInBlockAndSkipRestOfBlock<Vector4>
                (SOMEDEFAULTS, STRINGDEFAULTS, reader => reader.ReadVector4Block(STFReader.Units.None, Vector4.Zero));
        }
    }
    #endregion

    #endregion

    #region TokenProcessor and Parseblock/File
    public class TokenProcessingShould
    {
        [Fact]
        public static void DefineTokenProcessor()
        {
            string sometoken = "sometoken";
            int called = 0;
            var tokenProcessor = new STFReader.TokenProcessor(sometoken, () => { called++; });
            Assert.Equal(sometoken, tokenProcessor.Token);
            tokenProcessor.Processor.Invoke();
            Assert.Equal(1, called);
        }

        [Fact]
        public static void ParseABlock()
        {
            string source = "block1 block2()block1()";
            var reader = Create.Reader(source);
            int called1 = 0;
            int called2 = 0;
            reader.ParseBlock(new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
                    new STFReader.TokenProcessor("block2", () => { called2++; })
                });
            Assert.Equal(2, called1);
            Assert.Equal(1, called2);
        }

        [Fact]
        public static void ParseABlockWithBreakOut()
        {
            string source = "block1 block2()block1()";
            var reader = Create.Reader(source);
            int called1 = 0;
            int called2 = 0;
            reader.ParseBlock(() => called2 == 1, new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
                    new STFReader.TokenProcessor("block2", () => { called2++; })
                });
            Assert.Equal(1, called1);
            Assert.Equal(1, called2);
        }

        [Fact]
        public static void ParseABlockAndThenContinue()
        {
            string followingtoken = "sometoken";
            string source = "block1())" + followingtoken;
            var reader = Create.Reader(source);
            int called1 = 0;
            reader.ParseBlock(new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
            });
            Assert.Equal(followingtoken, reader.ReadItem());
        }

        [Fact]
        public static void ParseAFile()
        {
            string source = "block1 block2()block1()";
            var reader = Create.Reader(source);
            int called1 = 0;
            int called2 = 0;
            reader.ParseFile(new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
                    new STFReader.TokenProcessor("block2", () => { called2++; })
                });
            Assert.Equal(2, called1);
            Assert.Equal(1, called2);
        }

        [Fact]
        public static void ParseAFileWithBreakOut()
        {
            string source = "block1 block2()block1()";
            var reader = Create.Reader(source);
            int called1 = 0;
            int called2 = 0;
            reader.ParseFile(() => called2 == 1, new[] {
                    new STFReader.TokenProcessor("block1", () => { called1++; }),
                    new STFReader.TokenProcessor("block2", () => { called2++; })
                });
            Assert.Equal(1, called1);
            Assert.Equal(1, called2);
        }

        [Fact]
        public static void ParseAFileTillEOF()
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
            Assert.Equal(1, called2);
            Assert.True(reader.Eof);
        }
    }
    #endregion

    #region Legacy
    public class UsedTo
    {
        [Fact]
        static void PeekPastWhiteSpace()
        {   //testing only what is really needed right now.
            var reader = Create.Reader("a  )  ");
            Assert.False(')' == reader.PeekPastWhitespace());
            Assert.False(-1 == reader.PeekPastWhitespace());

            reader.ReadItem();
            Assert.Equal(')', reader.PeekPastWhitespace());
            reader.ReadItem();
            Assert.Equal(-1, reader.PeekPastWhitespace());
        }

    }
    #endregion

    #region Test utilities
    class Create
    {
        public static STFReader Reader(string source)
        {
            return Create.Reader(source, "some.stf", false);
        }

        public static STFReader Reader(string source, string fileName)
        {
            return Create.Reader(source, fileName, false);
        }

        public static STFReader Reader(string source, bool useTree)
        {
            return Create.Reader(source, "some.stf", useTree);
        }

        public static STFReader Reader(string source, string fileName, bool useTree)
        {
            var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes(source));
            return new STFReader(memoryStream, fileName, Encoding.ASCII, useTree);
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
        public string inputString;
        public string[] expectedTokens;
        public int[] expectedLineNumbers;

        public TokenTester(string input, string[] output)
        {
            inputString = input;
            expectedTokens = output;
            expectedLineNumbers = new int[0];
        }

        public TokenTester(string input, int[] lineNumbers)
        {
            inputString = input;
            expectedTokens = new string[0];
            expectedLineNumbers = lineNumbers;
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
        public static void Throws(Assert.ThrowsDelegate testCode, string pattern)
        {
            var exception = Record.Exception(testCode);
            Assert.NotNull(exception);
            Assert.IsType<STFException>(exception);
            Assert.True(Regex.IsMatch(exception.Message, pattern), exception.Message + " does not match pattern: " + pattern);
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
    #endregion

}

