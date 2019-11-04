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
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

using Orts.Common;

namespace Orts.Formats.Msts.Parsers
{
    /// <summary>
    /// Structured Block Reader can read compressed binary or uncompressed unicode files.
    /// Its intended to replace the KujuBinary classes ( which are binary only ).
    /// Every block must be closed with either Skip() or VerifyEndOfBlock()
    /// </summary>
    public abstract class SBR : IDisposable
    {
        public TokenID ID;
        public string Label;  // First data item may be a label ( usually a 0 byte )
        public string FileName { get; protected set; }

        public static SBR Open(string fileName)
        {
            Stream fb = new FileStream(fileName, FileMode.Open, FileAccess.Read);

            byte[] buffer = new byte[34];
            fb.Read(buffer, 0, 2);

            bool unicode = (buffer[0] == 0xFF && buffer[1] == 0xFE);  // unicode header

            string headerString;
            if (unicode)
            {
                fb.Read(buffer, 0, 32);
                headerString = System.Text.Encoding.Unicode.GetString(buffer, 0, 16);
            }
            else
            {
                fb.Read(buffer, 2, 14);
                headerString = System.Text.Encoding.ASCII.GetString(buffer, 0, 8);
            }

            // SIMISA@F  means compressed
            // SIMISA@@  means uncompressed
            if (headerString.StartsWith("SIMISA@F"))
            {

                /* Skipping past the first two bytes. Those bytes are part of zlib specification (RFC 1950), not the deflate specification (RFC 1951) and 
                 * contain information about the compression method and flags.
                 * The zlib and deflate formats are related; the compressed data part of zlib-formatted data may be stored in the deflate format. 
                 * In particular, if the compression method in the zlib header is set to 8, then the compressed data is stored in the deflate format. 
                 * System.IO.Compression.DeflateStream, represents the deflate specification (RFC 1951) but not RFC 1950. So a workaround of skipping 
                 * past the first 2 bytes to get to the deflate data will work.
                */
                fb.Seek(2, SeekOrigin.Current);
                fb = new DeflateStream(fb, CompressionMode.Decompress);
            }
            else if (headerString.StartsWith("\r\nSIMISA"))
            {
                // ie us1rd2l1000r10d.s, we are going to allow this but warn
                Console.Error.WriteLine("Improper header in " + fileName);
                fb.Read(buffer, 0, 4);
            }
            else if (!headerString.StartsWith("SIMISA@@"))
            {
                throw new System.Exception("Unrecognized header \"" + headerString + "\" in " + fileName);
            }

            // Read SubHeader
            string subHeader;
            if (unicode)
            {
                fb.Read(buffer, 0, 32);
                subHeader = System.Text.Encoding.Unicode.GetString(buffer, 0, 16);
            }
            else
            {
                fb.Read(buffer, 0, 16);
                subHeader = System.Text.Encoding.ASCII.GetString(buffer, 0, 8);
            }

            // Select for binary vs text content
            if (subHeader[7] == 't')
            {
                return new UnicodeFileReader(fb, fileName, unicode ? Encoding.Unicode : Encoding.ASCII);
            }
            else if (subHeader[7] != 'b')
            {
                throw new System.Exception("Unrecognized subHeader \"" + subHeader + "\" in " + fileName);
            }

            // And for binary types, select where their tokens will appear in our TokenID enum
            if (subHeader[5] == 'w')  // and [7] must be 'b'
            {
                return new BinaryFileReader(fb, fileName, 300);
            }
            else
            {
                return new BinaryFileReader(fb, fileName, 0);
            }
        }

        public abstract SBR ReadSubBlock();

        /// <summary>
        /// Skip to the end of this block
        /// </summary>
        public abstract void Skip();
        public abstract void VerifyEndOfBlock();
        public abstract uint ReadFlags();
        public abstract int ReadInt();
        public abstract uint ReadUInt();
        public abstract float ReadFloat();
        public abstract string ReadString();
        public abstract bool EndOfBlock();

        public void VerifyID(TokenID desiredID)
        {
           if (ID != desiredID)
               TraceInformation("Expected block " + desiredID + "; got " + ID);
        }

        /// <summary>
        /// Verify that this is a comment block.
        /// </summary>
        /// <param name="block"></param>
        public void ExpectComment()
        {
            if (ID == TokenID.Comment)
            {
                Skip();
            }
            else
            {
                TraceInformation("Expected block comment; got " + ID);
                Skip();
            }
        }

        public abstract void TraceInformation(string message);
        public abstract void TraceWarning(string message);
        public abstract void ThrowException(string message);

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            VerifyEndOfBlock();
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                }
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion
    }

    /// <summary>
    /// Structured unicode text file reader
    /// </summary>
    public class UnicodeFileReader : UnicodeBlockReader
    {
        private bool closed;

        public UnicodeFileReader(Stream inputStream, string fileName, Encoding encoding)
        {
            FileName = fileName;
            stf = new STFReader(inputStream, fileName, encoding, false);
        }

        /// <summary>
        /// Skip to the end of this block
        /// </summary>
        /// <returns></returns>
        public override void Skip()
        {
            stf.Dispose();
            closed = true;
        }

        public override void VerifyEndOfBlock()
        {
            if (closed) return;

            string s = stf.ReadItem();
            string extraData = s;
            if (s != "")
            {
                // we have extra data at the end of the file
                while (s != "")
                {
                    if (s != ")")  // we'll ignore extra )'s since the files are full of misformed brackets
                    {
                        TraceWarning("Expected end of file; got '" + s + "'");
                        stf.Dispose();
                        closed = true;
                        return;
                    }
                    s = stf.ReadItem();
                }
            }
            stf.Dispose();
            closed = true;
        }

        /// <summary>
        /// Note, it doesn't consume the end of block marker, you must still
        /// call VerifiyEndOfBlock to consume it
        /// </summary>
        /// <returns></returns>
        public override bool EndOfBlock()
        {
            return closed || endOfBlock || stf.PeekPastWhitespace() == -1;
        }
    }

    /// <summary>
    /// Structured unicode text file reader
    /// </summary>
    public class UnicodeBlockReader : SBR
    {
        protected STFReader stf;
        protected bool endOfBlock;

        public override SBR ReadSubBlock()
        {
            UnicodeBlockReader block = new UnicodeBlockReader
            {
                stf = stf
            };

            string token = stf.ReadItem();

            if (token == "(")
            {
                // ie 310.eng Line 349  (#_fire temp, fire mass, water mass, boil ...
                block.ID = TokenID.Comment;
                return block;
            }

            // parse token
            block.ID = GetTokenID(token);

            if (token == ")")
            {
                TraceWarning("Ignored extra close bracket");
                return block;
            }

            // now look for optional label, ie matrix MAIN ( ....
            token = stf.ReadItem();

            if (token != "(")
            {
                block.Label = token;
                stf.MustMatchBlockStart();
            }

            return block;
        }

        private TokenID GetTokenID(string token)
        {

            if (EnumExtension.GetValue(token, out TokenID tokenID))
                return tokenID;
            else if (string.Compare(token, "SKIP", true) == 0 || string.Compare(token, "COMMENT", true) == 0 
                || token.StartsWith("#"))
                return TokenID.Comment;
            else
            {
                TraceWarning("Skipped unknown token " + token);
                return TokenID.Comment;
            }
        }

        /// <summary>
        /// Skip to the end of this block
        /// </summary>
        /// <returns></returns>
        public override void Skip()
        {
            if (endOfBlock) return;  // already there

            // We are inside a pair of brackets, skip the entire hierarchy to past the end bracket
            int depth = 1;
            while (depth > 0)
            {
                string token = stf.ReadItem();
                if (token == "")
                {
                    TraceWarning("Unexpected end of file");
                    endOfBlock = true;
                    return;
                }
                if (token == "(")
                    ++depth;
                if (token == ")")
                    --depth;
            }
            endOfBlock = true;
        }

        /// <summary>
        /// Note, it doesn't consume the end of block marker, you must still
        /// call VerifiyEndOfBlock to consume it
        /// </summary>
        /// <returns></returns>
        public override bool EndOfBlock()
        {
            return endOfBlock || stf.PeekPastWhitespace() == ')' || stf.EOF();
        }

        public override void VerifyEndOfBlock()
        {
            if (!endOfBlock)
            {
                string s = stf.ReadItem();
                if (s.StartsWith("#") || 0 == string.Compare(s, "comment", true))
                {
                    // allow comments at end of block ie
                    // MaxReleaseRate( 1.4074  #For train position 31-45  use (1.86 - ( 0.0146 * 31 ))	)
                    Skip();
                    return;
                }
                if (s != ")")
                    TraceWarning("Expected end of block; got '" + s + "'");

                endOfBlock = true;
            }
        }

        public override uint ReadFlags() { return stf.ReadHex(null); }
        public override int ReadInt() { return stf.ReadInt(null); }
        public override uint ReadUInt() { return stf.ReadUInt(null); }
        public override float ReadFloat() { return stf.ReadFloat(STFReader.Units.None, null); }
        public override string ReadString() { return stf.ReadItem(); }

        public override void TraceInformation(string message)
        {
            STFException.TraceInformation(stf, message);
        }

        public override void TraceWarning(string message)
        {
            STFException.TraceWarning(stf, message);
        }

        public override void ThrowException(string message)
        {
            throw new STFException(stf, message);
        }
    }

    /// <summary>
    /// Structured kuju binary file reader
    /// </summary>
    public class BinaryFileReader : BinaryBlockReader
    {
        /// <summary>
        /// Assumes that fb is positioned just after the SIMISA@F header
        /// filename is provided for error reporting purposes
        /// Each block has a token ID.  It's value corresponds to the value of
        /// the TokenID enum.  For some file types, ie .W files, the token value's 
        /// will be offset into the TokenID table by the specified tokenOffset.
        /// </summary>
        /// <param name="fb"></param>
        public BinaryFileReader(Stream inputStream, string fileName, int tokenOffset)
        {
            FileName = fileName;
            InputStream = new BinaryReader(inputStream);
            TokenOffset = tokenOffset;
        }

        public override void Skip()
        {
            while (!EndOfBlock())
                InputStream.ReadByte();
        }

        public override bool EndOfBlock()
        {
            return InputStream.PeekChar() == -1;
        }

        public override void VerifyEndOfBlock()
        {
            if (!EndOfBlock())
                TraceWarning("Expected end of file; got more data");
            InputStream.Close();
        }
    }

    /// <summary>
    /// Structured kuju binary file reader
    /// </summary>
    public class BinaryBlockReader : SBR
    {
        public BinaryReader InputStream;
        public uint RemainingBytes;  // number of bytes in this block not yet read from the stream
        public uint Flags;
        protected int TokenOffset;     // the binaryTokens are offset by this amount, ie for binary world files 

        public override SBR ReadSubBlock()
        {
            BinaryBlockReader block = new BinaryBlockReader
            {
                FileName = FileName,
                InputStream = InputStream,
                TokenOffset = TokenOffset
            };

            int MSTSToken = InputStream.ReadUInt16();
            block.ID = (TokenID)(MSTSToken + TokenOffset);
            block.Flags = InputStream.ReadUInt16();
            block.RemainingBytes = InputStream.ReadUInt32(); // record length

            uint blockSize = block.RemainingBytes + 8; //for the header
            RemainingBytes -= blockSize;

            int labelLength = InputStream.ReadByte();
            block.RemainingBytes -= 1;
            if (labelLength > 0)
            {
                byte[] buffer = InputStream.ReadBytes(labelLength * 2);
                block.Label = System.Text.Encoding.Unicode.GetString(buffer, 0, labelLength * 2);
                block.RemainingBytes -= (uint)labelLength * 2;
            }
            return block;
        }

        public override void Skip()
        {
            if (RemainingBytes > 0)
            {
                if (RemainingBytes > Int32.MaxValue)
                {
                    TraceWarning("Remaining Bytes overflow");
                    RemainingBytes = 1000;
                }
                InputStream.ReadBytes((int)RemainingBytes);

                RemainingBytes = 0;
            }
        }
        public override bool EndOfBlock()
        {
            return RemainingBytes == 0;
        }

        public override void VerifyEndOfBlock()
        {
            if (!EndOfBlock())
            {
                TraceWarning("Expected end of block " + ID + "; got more data");
                Skip();
            }
        }

        public override uint ReadFlags() { RemainingBytes -= 4; return InputStream.ReadUInt32(); }
        public override int ReadInt() { RemainingBytes -= 4; return InputStream.ReadInt32(); }
        public override uint ReadUInt() { RemainingBytes -= 4; return InputStream.ReadUInt32(); }
        public override float ReadFloat() { RemainingBytes -= 4; return InputStream.ReadSingle(); }
        public override string ReadString()
        {
            ushort count = InputStream.ReadUInt16();
            if (count > 0)
            {
                byte[] b = InputStream.ReadBytes(count * 2);
                string s = System.Text.Encoding.Unicode.GetString(b);
                RemainingBytes -= (uint)(count * 2 + 2);
                return s;
            }
            else
            {
                return "";
            }
        }

        public override void TraceInformation(string message)
        {
            SBRException.TraceInformation(this, message);
        }

        public override void TraceWarning(string message)
        {
            SBRException.TraceWarning(this, message);
        }

        public override void ThrowException(string message)
        {
            throw new SBRException(this, message);
        }
    }

    [Serializable]
    public class SBRException : Exception
    {
        public static void TraceWarning(BinaryBlockReader sbr, string message)
        {
            long position = (sbr.InputStream.BaseStream is DeflateStream deflateStream) ? deflateStream.BaseStream.Position : sbr.InputStream.BaseStream.Position;
            Trace.TraceWarning("{2} in {0}:byte {1}", sbr.FileName, position, message);
        }

        public static void TraceInformation(BinaryBlockReader sbr, string message)
        {
            long position = (sbr.InputStream.BaseStream is DeflateStream deflateStream) ? deflateStream.BaseStream.Position : sbr.InputStream.BaseStream.Position;
            Trace.TraceInformation("{2} in {0}:byte {1}", sbr.FileName, position, message);
        }

        public SBRException(BinaryBlockReader sbr, string message)
            : base($"{message} in {sbr.FileName}:byte {((sbr.InputStream.BaseStream is DeflateStream deflateStream) ? deflateStream.BaseStream.Position : sbr.InputStream.BaseStream.Position)}\n")
        {
        }
    }
}
