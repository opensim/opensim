/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using System.Reflection;
using System.Text;
using log4net;

namespace OpenSim.Framework.Serialization
{
    /// <summary>
    /// Temporary code to do the bare minimum required to read a tar archive for our purposes
    /// </summary>
    public class TarArchiveReader
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public enum TarEntryType
        {
            TYPE_UNKNOWN = 0,
            TYPE_NORMAL_FILE = 1,
            TYPE_HARD_LINK = 2,
            TYPE_SYMBOLIC_LINK = 3,
            TYPE_CHAR_SPECIAL = 4,
            TYPE_BLOCK_SPECIAL = 5,
            TYPE_DIRECTORY = 6,
            TYPE_FIFO = 7,
            TYPE_CONTIGUOUS_FILE = 8,
        }

        /// <summary>
        /// Binary reader for the underlying stream
        /// </summary>
        protected BinaryReader m_br;

        /// <summary>
        /// Used to trim off null chars
        /// </summary>
        protected static char[] m_nullCharArray = new char[] { '\0' };
        /// <summary>
        /// Used to trim off space chars
        /// </summary>
        protected static char[] m_spaceCharArray = new char[] { ' ' };

        /// <summary>
        /// Generate a tar reader which reads from the given stream.
        /// </summary>
        /// <param name="s"></param>
        public TarArchiveReader(Stream s)
        {
            m_br = new BinaryReader(s);
        }

        /// <summary>
        /// Read the next entry in the tar file.
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns>the data for the entry.  Returns null if there are no more entries</returns>
        public byte[] ReadEntry(out string filePath, out TarEntryType entryType)
        {
            filePath = String.Empty;
            entryType = TarEntryType.TYPE_UNKNOWN;
            TarHeader header = ReadHeader();

            if (null == header)
                return null;

            entryType = header.EntryType;
            filePath = header.FilePath;
            return ReadData(header.FileSize);
        }

        /// <summary>
        /// Read the next 512 byte chunk of data as a tar header.
        /// </summary>
        /// <returns>A tar header struct.  null if we have reached the end of the archive.</returns>
        protected TarHeader ReadHeader()
        {
            byte[] header = m_br.ReadBytes(512);

            // If there are no more bytes in the stream, return null header
            if (header.Length == 0)
                return null;

            // If we've reached the end of the archive we'll be in null block territory, which means
            // the next byte will be 0
            if (header[0] == 0)
                return null;

            TarHeader tarHeader = new TarHeader();

            // If we're looking at a GNU tar long link then extract the long name and pull up the next header
            if (header[156] == (byte)'L')
            {
                int longNameLength = ConvertOctalBytesToDecimal(header, 124, 11);
                tarHeader.FilePath = Encoding.ASCII.GetString(ReadData(longNameLength));
                //m_log.DebugFormat("[TAR ARCHIVE READER]: Got long file name {0}", tarHeader.FilePath);
                header = m_br.ReadBytes(512);
            }
            else
            {
                tarHeader.FilePath = Encoding.ASCII.GetString(header, 0, 100);
                tarHeader.FilePath = tarHeader.FilePath.Trim(m_nullCharArray);
                //m_log.DebugFormat("[TAR ARCHIVE READER]: Got short file name {0}", tarHeader.FilePath);
            }

            tarHeader.FileSize = ConvertOctalBytesToDecimal(header, 124, 11);

            switch (header[156])
            {
                case 0:
                    tarHeader.EntryType = TarEntryType.TYPE_NORMAL_FILE;
                    break;
                case (byte)'0':
                    tarHeader.EntryType = TarEntryType.TYPE_NORMAL_FILE;
                break;
                case (byte)'1':
                    tarHeader.EntryType = TarEntryType.TYPE_HARD_LINK;
                break;
                case (byte)'2':
                    tarHeader.EntryType = TarEntryType.TYPE_SYMBOLIC_LINK;
                break;
                case (byte)'3':
                    tarHeader.EntryType = TarEntryType.TYPE_CHAR_SPECIAL;
                break;
                case (byte)'4':
                    tarHeader.EntryType = TarEntryType.TYPE_BLOCK_SPECIAL;
                break;
                case (byte)'5':
                    tarHeader.EntryType = TarEntryType.TYPE_DIRECTORY;
                break;
                case (byte)'6':
                    tarHeader.EntryType = TarEntryType.TYPE_FIFO;
                break;
                case (byte)'7':
                    tarHeader.EntryType = TarEntryType.TYPE_CONTIGUOUS_FILE;
                break;
            }

            return tarHeader;
        }

        /// <summary>
        /// Read data following a header
        /// </summary>
        /// <param name="fileSize"></param>
        /// <returns></returns>
        protected byte[] ReadData(int fileSize)
        {
            byte[] data = m_br.ReadBytes(fileSize);

            //m_log.DebugFormat("[TAR ARCHIVE READER]: fileSize {0}", fileSize);

            // Read the rest of the empty padding in the 512 byte block
            if (fileSize % 512 != 0)
            {
                int paddingLeft = 512 - (fileSize % 512);

                //m_log.DebugFormat("[TAR ARCHIVE READER]: Reading {0} padding bytes", paddingLeft);

                m_br.ReadBytes(paddingLeft);
            }

            return data;
        }

        public void Close()
        {
            m_br.Close();
        }

        /// <summary>
        /// Convert octal bytes to a decimal representation
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static int ConvertOctalBytesToDecimal(byte[] bytes, int startIndex, int count)
        {
            // Trim leading white space: ancient tars do that instead
            // of leading 0s :-( don't ask. really.
            string oString = Encoding.ASCII.GetString(bytes, startIndex, count).TrimStart(m_spaceCharArray);

            int d = 0;

            foreach (char c in oString)
            {
                d <<= 3;
                d |= c - '0';
            }

            return d;
        }
    }

    public class TarHeader
    {
        public string FilePath;
        public int FileSize;
        public TarArchiveReader.TarEntryType EntryType;
    }
}
