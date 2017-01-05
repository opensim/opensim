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
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using log4net;

namespace OpenSim.Framework.Serialization
{
    /// <summary>
    /// Temporary code to produce a tar archive in tar v7 format
    /// </summary>
    public class TarArchiveWriter
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Binary writer for the underlying stream
        /// </summary>
        protected BinaryWriter m_bw;

        public TarArchiveWriter(Stream s)
        {
            m_bw = new BinaryWriter(s);
        }

        /// <summary>
        /// Write a directory entry to the tar archive.  We can only handle one path level right now!
        /// </summary>
        /// <param name="dirName"></param>
        public void WriteDir(string dirName)
        {
            // Directories are signalled by a final /
            if (!dirName.EndsWith("/"))
                dirName += "/";

            WriteFile(dirName, new byte[0]);
        }

        /// <summary>
        /// Write a file to the tar archive
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="data"></param>
        public void WriteFile(string filePath, string data)
        {
            WriteFile(filePath, Util.UTF8NoBomEncoding.GetBytes(data));
        }

        /// <summary>
        /// Write a file to the tar archive
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="data"></param>
        public void WriteFile(string filePath, byte[] data)
        {
            if (filePath.Length > 100)
                WriteEntry("././@LongLink", Encoding.ASCII.GetBytes(filePath), 'L');

            char fileType;

            if (filePath.EndsWith("/"))
            {
                fileType = '5';
            }
            else
            {
                fileType = '0';
            }

            WriteEntry(filePath, data, fileType);
        }

        /// <summary>
        /// Finish writing the raw tar archive data to a stream.  The stream will be closed on completion.
        /// </summary>
        /// <param name="s">Stream to which to write the data</param>
        /// <returns></returns>
        public void Close()
        {
            //m_log.Debug("[TAR ARCHIVE WRITER]: Writing final consecutive 0 blocks");

            // Write two consecutive 0 blocks to end the archive
            byte[] finalZeroPadding = new byte[1024];

            lock (m_bw)
            {
                m_bw.Write(finalZeroPadding);

                m_bw.Flush();
                m_bw.Close();
            }
        }

        public static byte[] ConvertDecimalToPaddedOctalBytes(int d, int padding)
        {
            string oString = "";

            while (d > 0)
            {
                oString = Convert.ToString((byte)'0' + d & 7) + oString;
                d >>= 3;
            }

            while (oString.Length < padding)
            {
                oString = "0" + oString;
            }

            byte[] oBytes = Encoding.ASCII.GetBytes(oString);

            return oBytes;
        }

        /// <summary>
        /// Write a particular entry
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="data"></param>
        /// <param name="fileType"></param>
        protected void WriteEntry(string filePath, byte[] data, char fileType)
        {
//            m_log.DebugFormat(
//                "[TAR ARCHIVE WRITER]: Data for {0} is {1} bytes", filePath, (null == data ? "null" : data.Length.ToString()));

            byte[] header = new byte[512];

            // file path field (100)
            byte[] nameBytes = Encoding.ASCII.GetBytes(filePath);
            int nameSize = (nameBytes.Length >= 100) ? 100 : nameBytes.Length;
            Array.Copy(nameBytes, header, nameSize);

            // file mode (8)
            byte[] modeBytes = Encoding.ASCII.GetBytes("0000777");
            Array.Copy(modeBytes, 0, header, 100, 7);

            // owner user id (8)
            byte[] ownerIdBytes = Encoding.ASCII.GetBytes("0000764");
            Array.Copy(ownerIdBytes, 0, header, 108, 7);

            // group user id (8)
            byte[] groupIdBytes = Encoding.ASCII.GetBytes("0000764");
            Array.Copy(groupIdBytes, 0, header, 116, 7);

            // file size in bytes (12)
            int fileSize = data.Length;
            //m_log.DebugFormat("[TAR ARCHIVE WRITER]: File size of {0} is {1}", filePath, fileSize);

            byte[] fileSizeBytes = ConvertDecimalToPaddedOctalBytes(fileSize, 11);

            Array.Copy(fileSizeBytes, 0, header, 124, 11);

            // last modification time (12)
            byte[] lastModTimeBytes = Encoding.ASCII.GetBytes("11017037332");
            Array.Copy(lastModTimeBytes, 0, header, 136, 11);

            // entry type indicator (1)
            header[156] = Encoding.ASCII.GetBytes(new char[] { fileType })[0];

            Array.Copy(Encoding.ASCII.GetBytes("0000000"), 0, header, 329, 7);
            Array.Copy(Encoding.ASCII.GetBytes("0000000"), 0, header, 337, 7);

            // check sum for header block (8) [calculated last]
            Array.Copy(Encoding.ASCII.GetBytes("        "), 0, header, 148, 8);

            int checksum = 0;
            foreach (byte b in header)
            {
                checksum += b;
            }

            //m_log.DebugFormat("[TAR ARCHIVE WRITER]: Decimal header checksum is {0}", checksum);

            byte[] checkSumBytes = ConvertDecimalToPaddedOctalBytes(checksum, 6);

            Array.Copy(checkSumBytes, 0, header, 148, 6);

            header[154] = 0;

            lock (m_bw)
            {
                // Write out header
                m_bw.Write(header);

                // Write out data
                // An IOException occurs if we try to write out an empty array in Mono 2.6
                if (data.Length > 0)
                    m_bw.Write(data);

                if (data.Length % 512 != 0)
                {
                    int paddingRequired = 512 - (data.Length % 512);

                    //m_log.DebugFormat("[TAR ARCHIVE WRITER]: Padding data with {0} bytes", paddingRequired);

                    byte[] padding = new byte[paddingRequired];
                    m_bw.Write(padding);
                }
            }
        }
    }
}
