// COPYRIGHT 2018 by the Open Rails project.
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

// Use this define to diagnose issues in the JSON reader below.
// #define DEBUG_JSON_READER

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

using Microsoft.Xna.Framework;

using Newtonsoft.Json;

namespace Orts.Formats.OR.Parsers
{
    public class JsonReader
    {
        public static void ReadFile(string fileName, Func<JsonReader, bool> tryParse)
        {
            using (JsonTextReader reader = new JsonTextReader(File.OpenText(fileName)))
            {
                new JsonReader(fileName, reader).ReadBlock(tryParse);
            }
        }

        /// <summary>
        /// Read the JSON from a string using a method TryParse() which is specific for the expected objects.
        /// </summary>
        /// <param name="content"></param>
        /// <param name="fileName"></param>
        /// <param name="tryParse"></param>
        public static (int Warning, int Information) ReadTest(string content, string fileName, Func<JsonReader, bool> tryParse)
        {
            using (var reader = new JsonTextReader(new StringReader(content)))
            {
                JsonReader json = new JsonReader(fileName, reader);
                json.ReadFile(tryParse);
                return (json.countWarnings, json.countInformations);
            }
        }

        private readonly string fileName;
        private readonly JsonTextReader reader;
        private readonly StringBuilder path;
        private readonly Stack<int> pathPositions;
        private Stack<string> paths;
        private int countWarnings;
        private int countInformations;

        private string FullPath { get => path.Length > 0 ? path.ToString() : "(root)"; }

        /// <summary>
        /// Contains a condensed account of the position of the current item in the JSON, such as when parsing "Clear" from a WeatherFile:
        /// JsonReader item;
        ///   item.Path = "Changes[].Type"
        /// </summary>
        public string Path { get => paths.Peek(); }

        private JsonReader(string fileName, JsonTextReader reader)
        {
            this.fileName = fileName;
            this.reader = reader;
            path = new StringBuilder();
            pathPositions = new Stack<int>();
            paths = new Stack<string>();
        }

        private void ReadFile(Func<JsonReader, bool> tryParse)
        {
            try
            {
                ReadBlock(tryParse);
                // Read the rest of the file so that we catch any extra data, which might be in error
                while (reader.Read())
                    ;
            }
            catch (JsonReaderException error)
            {
                // Newtonsoft.Json unfortunately includes extra information in the message we already provide
                string[] jsonMessage = error.Message.Split(new[] { ". Path '" }, StringSplitOptions.None);
                TraceWarning($"{jsonMessage[0]} in {FullPath}");
            }
        }
        /// <summary>
        /// Reads next token and stores in _reader.TokenType, _reader.ValueType, _reader.Value
        /// Throws exception if value not as expected.
        /// PropertyNames are case-sensitive.
        /// </summary>
        /// <param name="tryParse"></param>
        public void ReadBlock(Func<JsonReader, bool> tryParse)
        {
            int basePosition = pathPositions.Count > 0 ? pathPositions.Peek() : 0;

#if DEBUG_JSON_READER
            Console.WriteLine($"JsonReader({basePosition} / {path} / {String.Join(" ", pathPositions)}).ReadBlock()");
#endif

            while (reader.Read())
            {
#if DEBUG_JSON_READER
                Console.Write($"JsonReader({basePosition} / {path} / {String.Join(" ", pathPositions)}) --> ");
#endif
                switch (reader.TokenType)
                {
                    case JsonToken.StartArray:
                        pathPositions.Push(path.Length);
                        path.Append("[]");
                        break;
                    case JsonToken.EndArray:
                        path.Length = pathPositions.Pop();
                        break;
                    case JsonToken.StartObject:
                        pathPositions.Push(path.Length);
                        if (pathPositions.Count > 1)
                            path.Append('.');
                        pathPositions.Push(path.Length);
                        break;
                    case JsonToken.PropertyName:
                        Debug.Assert(reader.ValueType == typeof(string));
                        path.Length = pathPositions.Peek();
                        path.Append((string)reader.Value);
                        break;
                    case JsonToken.EndObject:
                        int end = pathPositions.Pop();
                        path.Length = pathPositions.Pop();
                        if (end == basePosition)
                            return;
                        break;
                }

#if DEBUG_JSON_READER
                Console.WriteLine($"({basePosition} / {path} / {string.Join(" ", pathPositions)}) token={reader.TokenType} value={reader.Value} type={reader.ValueType}");
#endif
                if (path.Length <= basePosition && (reader.TokenType == JsonToken.EndArray || reader.TokenType == JsonToken.EndObject))
                    return;

                switch (reader.TokenType)
                {
                    case JsonToken.StartObject:
                    case JsonToken.StartArray:
                    case JsonToken.Boolean:
                    case JsonToken.Bytes:
                    case JsonToken.Date:
                    case JsonToken.Float:
                    case JsonToken.Integer:
                    case JsonToken.Null:
                    case JsonToken.String:
                        paths.Push(path.ToString()[basePosition..]);
                        if (!tryParse(this))
                            TraceInformation($"Skipped unknown {reader.TokenType} \"{reader.Value}\" in {Path}");
                        paths.Pop();
                        break;
                }
            }
            TraceWarning($"Unexpected end of file in {FullPath}");
        }

        public bool TryRead<T>(Func<JsonReader, T> read, out T output)
        {
            var warnings = countWarnings;
            output = read(this);
            return warnings == countWarnings;
        }

        public T AsEnum<T>(T defaultValue)
        {
            Debug.Assert(typeof(T).IsEnum, "Must use type inheriting from Enum for AsEnum()");
            switch (reader.TokenType)
            {
                case JsonToken.String:
                    string value = (string)reader.Value;
                    return (T)Enum.Parse(typeof(T), value, true);
                default:
                    TraceWarning($"Expected string (enum) value in {FullPath}; got {reader.TokenType}");
                    return defaultValue;
            }
        }

        public float AsFloat(float defaultValue)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Float:
                    return (float)(double)reader.Value;
                case JsonToken.Integer:
                    return (long)reader.Value;
                default:
                    TraceWarning($"Expected floating point value in {FullPath}; got {reader.TokenType}");
                    return defaultValue;
            }
        }

        public int AsInteger(int defaultValue)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Integer:
                    return (int)(long)reader.Value;
                default:
                    TraceWarning($"Expected integer value in {FullPath}; got {reader.TokenType}");
                    return defaultValue;
            }
        }

        public bool AsBoolean(bool defaultValue)
        {
            switch (reader.TokenType)
            {
                case JsonToken.Boolean:
                    return (bool)reader.Value;
                default:
                    TraceWarning($"Expected Boolean value in {FullPath}; got {reader.TokenType}");
                    return defaultValue;
            }
        }

        public string AsString(string defaultValue)
        {
            switch (reader.TokenType)
            {
                case JsonToken.String:
                    return (string)reader.Value;
                default:
                    TraceWarning($"Expected string value in {FullPath}; got {reader.TokenType}");
                    return defaultValue;
            }
        }

        public float AsTime(float defaultValue)
        {
            switch (reader.TokenType)
            {
                case JsonToken.String:
                    string[] time = ((string)reader.Value).Split(':');
                    TimeSpan StartTime = new TimeSpan(int.Parse(time[0], CultureInfo.InvariantCulture), time.Length > 1 ? int.Parse(time[1], CultureInfo.InvariantCulture) : 0, time.Length > 2 ? int.Parse(time[2], CultureInfo.InvariantCulture) : 0);
                    return (float)StartTime.TotalSeconds;
                default:
                    TraceWarning($"Expected string (time) value in {FullPath}; got {reader.TokenType}");
                    return defaultValue;
            }
        }

        public Vector3 AsVector3(Vector3 defaultValue)
        {
            Vector3 vector3 = defaultValue;
            switch (reader.TokenType)
            {
                case JsonToken.StartArray:
                    if (TryRead(json =>
                    {
                        List<float> floats = new List<float>(3);
                        ReadBlock(item =>
                        {
                            floats.Add(item.AsFloat(0));
                            return true;
                        });
                        return floats;
                    }, out List<float> vector))
                    {
                        if (vector.Count == 3)
                            return new Vector3(vector[0], vector[1], vector[2]);
                        TraceWarning($"Expected 3 float array (Vector3) value in {FullPath}; got {vector.Count} float array");
                    }
                    return defaultValue;
                default:
                    TraceWarning($"Expected array (Vector3) value in {FullPath}; got {reader.TokenType}");

                    return defaultValue;
            }
        }

        public void TraceWarning(string message)
        {
            Trace.TraceWarning("{2} in {0}:line {1}", fileName, reader.LineNumber, message);
            countWarnings++;
        }

        public void TraceInformation(string message)
        {
            Trace.TraceInformation("{2} in {0}:line {1}", fileName, reader.LineNumber, message);
            countInformations++;
        }
    }
}
