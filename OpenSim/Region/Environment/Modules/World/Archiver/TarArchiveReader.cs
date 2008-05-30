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
using System.IO;
//using System.Reflection;
//using log4net;

namespace OpenSim.Region.Environment.Modules.World.Archiver
{
    /// <summary>
    /// Temporary code to do the bare minimum required to read a tar archive for our purposes
    /// </summary>
    public class TarArchiveReader
    {
//        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        
        protected static System.Text.ASCIIEncoding m_asciiEncoding = new System.Text.ASCIIEncoding();
        
        /// <summary>
        /// Binary reader for the underlying stream
        /// </summary>
        protected BinaryReader m_br;
        
        public TarArchiveReader(string archivePath)
        {
            m_br = new BinaryReader(new FileStream(archivePath, FileMode.Open));
        }
        
        public byte[] Read(out string filePath)
        {
            TarHeader header = ReadHeader();
            filePath = header.FilePath;
            return m_br.ReadBytes(header.FileSize);
        }

        /// <summary>
        /// Read the next 512 byte chunk of data as a tar header.
        /// </summary>
        /// <returns>A tar header struct</returns>
        protected TarHeader ReadHeader()
        {
            TarHeader tarHeader = new TarHeader();
            
            byte[] header = m_br.ReadBytes(512);
            
            tarHeader.FilePath = m_asciiEncoding.GetString(header, 0, 100);
            tarHeader.FileSize = ConvertOctalBytesToDecimal(header, 124, 11);
            
            return tarHeader;
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
            string oString = m_asciiEncoding.GetString(bytes, startIndex, count);            
            
            int d = 0;
                        
            foreach (char c in oString)
            {
                d <<= 3;                
                d |= c - '0';               
            }

            return d;
        }        
    }
    
    public struct TarHeader
    {
        public string FilePath;
        public int FileSize;
    }
}
