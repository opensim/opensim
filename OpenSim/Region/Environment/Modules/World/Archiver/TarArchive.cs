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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Text;
using System.Reflection;
using log4net;

namespace OpenSim.Region.Environment
{    
    /// <summary>
    /// Temporary code to produce a tar archive in tar v7 format
    /// </summary>    
    public class TarArchive
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        protected Dictionary<string, byte[]> m_files = new Dictionary<string, byte[]>();
        
        protected static System.Text.ASCIIEncoding m_asciiEncoding = new System.Text.ASCIIEncoding();

        /// <summary>
        /// Add a file to the tar archive
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="data"></param>
        public void AddFile(string filePath, string data)
        {
            AddFile(filePath, m_asciiEncoding.GetBytes(data));
        }
        
        /// <summary>
        /// Add a file to the tar archive
        /// </summary>
        /// <param name="filePath"></param>
        /// <param name="data"></param>
        public void AddFile(string filePath, byte[] data)
        {
            m_files[filePath] = data;
        }
        
        /// <summary>
        /// Write the raw tar archive data to a file
        /// </summary>
        /// <returns></returns>
        public void WriteTar(string archivePath)
        {
            BinaryWriter bw = new BinaryWriter(new FileStream(archivePath, FileMode.Create));                       

            foreach (string filePath in m_files.Keys)
            {
                byte[] header = new byte[512];
                byte[] data = m_files[filePath];
                
                //string filePath = "test.txt";                
                //byte[] data = m_asciiEncoding.GetBytes("hello\n");                                        
                
                // file path field (100)
                byte[] nameBytes = m_asciiEncoding.GetBytes(filePath);
                int nameSize = (nameBytes.Length >= 100) ? 100 : nameBytes.Length;
                Array.Copy(nameBytes, header, nameSize);                
                
                // file mode (8)
                byte[] modeBytes = m_asciiEncoding.GetBytes("0000644");
                Array.Copy(modeBytes, 0, header, 100, 7);
                
                // owner user id (8)
                byte[] ownerIdBytes = m_asciiEncoding.GetBytes("0000764");
                Array.Copy(ownerIdBytes, 0, header, 108, 7);
                
                // group user id (8)
                byte[] groupIdBytes = m_asciiEncoding.GetBytes("0000764");
                Array.Copy(groupIdBytes, 0, header, 116, 7);
                
                // file size in bytes (12)
                int fileSize = data.Length;
                m_log.DebugFormat("[TAR ARCHIVE]: File size of {0} is {1}", filePath, fileSize);

                byte[] fileSizeBytes = ConvertDecimalToPaddedOctalBytes(fileSize, 11);
                
                Array.Copy(fileSizeBytes, 0, header, 124, 11);
                
                // last modification time (12)
                byte[] lastModTimeBytes = m_asciiEncoding.GetBytes("11017037332");
                Array.Copy(lastModTimeBytes, 0, header, 136, 11);                        
                                                                
                // link indicator (1)
                //header[156] = m_asciiEncoding.GetBytes("0")[0];
                header[156] = 0;
            
                Array.Copy(m_asciiEncoding.GetBytes("0000000"), 0, header, 329, 7);
                Array.Copy(m_asciiEncoding.GetBytes("0000000"), 0, header, 337, 7);
                
                // check sum for header block (8) [calculated last]
                Array.Copy(m_asciiEncoding.GetBytes("        "), 0, header, 148, 8);
                
                int checksum = 0;
                foreach (byte b in header)
                {
                    checksum += b;
                }
                
                m_log.DebugFormat("[TAR ARCHIVE]: Decimal header checksum is {0}", checksum);
            
                byte[] checkSumBytes = ConvertDecimalToPaddedOctalBytes(checksum, 6);
                //byte[] checkSumBytes = m_asciiEncoding.GetBytes("007520");
                
                Array.Copy(checkSumBytes, 0, header, 148, 6);
                
                header[154] = 0;
                
                // Write out header                
                bw.Write(header);
                
                // Write out data
                bw.Write(data);
                
                int paddingRequired = 512 - (data.Length % 512);
                if (paddingRequired > 0)
                {
                    m_log.DebugFormat("Padding data with {0} bytes", paddingRequired);
                    
                    byte[] padding = new byte[paddingRequired];
                    bw.Write(padding);
                }
            }
            
            // Write two consecutive 0 blocks to end the archive
            byte[] finalZeroPadding = new byte[1024];
            bw.Write(finalZeroPadding);
            
            bw.Close();
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
            
            byte[] oBytes = m_asciiEncoding.GetBytes(oString);
            
            return oBytes;
        }
    }
}