using System;
using System.Runtime.CompilerServices;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xna.Framework;

using Orts.Formats.OR.Parsers;

using Tests.Orts.Shared;

namespace Tests.Orts.Formats.OR.Parsers
{
    [TestClass]
    public class JsonReaderTests
    {
        private static void ReadJsonText(string text, Func<JsonReader, bool> tryParse, int expectedWarning = 0, int expectedInformation = 0, [CallerMemberName] string memberName = "")
        {
            if (expectedWarning > 0)
                AssertWarnings.Expected();
            else
                AssertWarnings.NotExpected();
            (int Warning, int Information) = JsonReader.ReadTest(text, $"{memberName}.json", tryParse);
            Assert.IsTrue(expectedWarning == Warning, $"Expected {expectedWarning} warning log messages; got {Warning}");
            Assert.IsTrue(expectedInformation == Information, $"Expected {expectedInformation} information log messages; got {Information}");
        }

        [TestMethod]
        public void FileTooShort()
        {
            ReadJsonText("{ ", item => true, expectedWarning: 1);
            ReadJsonText("[ ", item => true, expectedWarning: 1);
        }

        [TestMethod]
        public void FileTooLong()
        {
            ReadJsonText("{ } }", item => true, expectedWarning: 1);
            ReadJsonText("[ ] ]", item => true, expectedWarning: 1);
        }

        [TestMethod]
        public void FileJustRight()
        {
            ReadJsonText("{ }", item => true);
            ReadJsonText("[ ]", item => true);
        }

        [TestMethod]
        public void FileTruncated()
        {
            ReadJsonText(@"{ ""foo ", item => true, expectedWarning: 1);
            ReadJsonText(@"{ ""foo"": ", item => true, expectedWarning: 1);
            ReadJsonText(@"{ ""foo"": tr ", item => true, expectedWarning: 1);
            ReadJsonText(@"{ ""foo"": true, ", item => true, expectedWarning: 1);
            ReadJsonText(@"{ ""foo"": { ", item => true, expectedWarning: 1);
        }

        [TestMethod]
        public void ParseObjectProperties()
        {
            ReadJsonText(@"{
                ""bool"": true,
                ""enum"": ""oRdInAl"",
                ""float"": 1.111,
                ""integer"": 1,
                ""string"": ""1"",
                ""time"": ""12:34:56"",
                ""vector3"": [1.1, 2.2, 3.3],
                ""object1"": {
                    ""a"": 1.111,
                    ""b"": 1,
                    ""c"": ""1"",
                },
                ""object2"": {
                    ""a"": 1.111,
                    ""b"": 1,
                    ""c"": ""1"",
                },
            }", item =>
            {
                switch (item.Path)
                {
                    case "":
                        // Root object, AOK
                        break;
                    case "bool":
                        Assert.IsTrue(item.AsBoolean(false));
                        break;
                    case "enum":
                        Assert.AreEqual(StringComparison.Ordinal, item.AsEnum<StringComparison>(StringComparison.CurrentCulture));
                        break;
                    case "float":
                        Assert.AreEqual(1.111f, item.AsFloat(0));
                        break;
                    case "integer":
                        Assert.AreEqual(1, item.AsInteger(0));
                        break;
                    case "string":
                        Assert.AreEqual("1", item.AsString(""));
                        break;
                    case "time":
                        Assert.AreEqual(new TimeSpan(12, 34, 56).TotalSeconds, item.AsTime(0));
                        break;
                    case "vector3[]":
                        Assert.AreEqual(new Vector3(1.1f, 2.2f, 3.3f), item.AsVector3(Vector3.Zero));
                        break;
                    case "object1.":
                    case "object2.":
                        item.ReadBlock(item2 =>
                        {
                            switch (item2.Path)
                            {
                                case "a":
                                    Assert.AreEqual(1.111f, item.AsFloat(0));
                                    break;
                                case "b":
                                    Assert.AreEqual(1, item.AsInteger(0));
                                    break;
                                case "c":
                                    Assert.AreEqual("1", item.AsString(""));
                                    break;
                                default:
                                    return false;
                            }
                            return true;
                        });
                        break;
                    default:
                        return false;
                }
                return true;
            });
        }

        [TestMethod]
        public void ParseArrayItems()
        {
            ReadJsonText(@"[
                {
                    ""a"": 1.111,
                    ""b"": 1,
                    ""c"": ""1"",
                },
                {
                    ""a"": 1.111,
                    ""b"": 1,
                    ""c"": ""1"",
                },
            ]", item =>
            {
                switch (item.Path)
                {
                    case "[]":
                        // Root array, AOK
                        break;
                    case "[].":
                        item.ReadBlock(item2 =>
                        {
                            switch (item2.Path)
                            {
                                case "":
                                    break;
                                case "a":
                                    Assert.AreEqual(1.111f, item.AsFloat(0));
                                    break;
                                case "b":
                                    Assert.AreEqual(1, item.AsInteger(0));
                                    break;
                                case "c":
                                    Assert.AreEqual("1", item.AsString(""));
                                    break;
                                default:
                                    return false;
                            }
                            return true;
                        });
                        break;
                    default:
                        return false;
                }
                return true;
            });
        }

        [TestMethod]
        public void ParseInvalidVector3()
        {
            ReadJsonText(@"{
                ""a"": [1.1, 2.2, 3.3],
                ""b"": [1, 2, 3],
                ""1"": [1.1, 2.2, 3.3, 4.4],
                ""2"": [1.1, 2.2],
                ""3"": [1.1],
                ""4"": [],
                ""5"": [1, 2, {}],
                ""6"": [1, 2, []],
                ""7"": [1, 2, true],
                ""8"": {},
                ""9"": 0,
                ""z"": [1.1, 2.2, 3.3],
            }", item =>
            {
                switch (item.Path)
                {
                    case "":
                        // Root object, AOK
                        break;
                    case "a[]":
                    case "z[]":
                        Assert.AreEqual(new Vector3(1.1f, 2.2f, 3.3f), item.AsVector3(Vector3.Zero));
                        break;
                    case "b[]":
                        Assert.AreEqual(new Vector3(1f, 2f, 3f), item.AsVector3(Vector3.Zero));
                        break;
                    case "1[]":
                    case "2[]":
                    case "3[]":
                    case "4[]":
                    case "5[]":
                    case "6[]":
                    case "7[]":
                        Assert.AreEqual(Vector3.Zero, item.AsVector3(Vector3.Zero));
                        break;
                    default:
                        return false;
                }
                return true;
            }, expectedWarning: 7, expectedInformation: 2);
        }

        [TestMethod]
        public void TryRead()
        {
            ReadJsonText(@"{
                ""a"": {
                    ""field"": 1,
                },
                ""b"": {
                    ""field"": false,
                },
            }", item =>
            {
                switch (item.Path)
                {
                    case "a.":
                        Assert.IsTrue(item.TryRead(TryReadObject, out var _));
                        break;
                    case "b.":
                        Assert.IsFalse(item.TryRead(TryReadObject, out var _));
                        break;
                }
                return true;
            }, expectedWarning: 1);
        }

        private int TryReadObject(JsonReader json)
        {
            json.ReadBlock(item =>
            {
                switch (item.Path)
                {
                    case "field":
                        item.AsInteger(0);
                        break;
                    default:
                        return false;
                }
                return true;
            });
            return 42;
        }

    }
}
