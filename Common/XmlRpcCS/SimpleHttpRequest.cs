/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
namespace Nwc.XmlRpc
{
    using System;
    using System.IO;
    using System.Net.Sockets;
    using System.Collections;
    using System.Text;

    ///<summary>Very basic HTTP request handler.</summary>
    ///<remarks>This class is designed to accept a TcpClient and treat it as an HTTP request.
    /// It will do some basic header parsing and manage the input and output streams associated
    /// with the request.</remarks>
    public class SimpleHttpRequest
    {
        private String _httpMethod = null;
        private String _protocol;
        private String _filePathFile = null;
        private String _filePathDir = null;
        private String __filePath;
        private TcpClient _client;
        private StreamReader _input;
        private StreamWriter _output;
        private Hashtable _headers;

        /// <summary>A constructor which accepts the TcpClient.</summary>
        /// <remarks>It creates the associated input and output streams, determines the request type,
        /// and parses the remaining HTTP header.</remarks>
        /// <param name="client">The <c>TcpClient</c> associated with the HTTP connection.</param>
        public SimpleHttpRequest(TcpClient client)
        {
            _client = client;
            
            _output = new StreamWriter(client.GetStream() );            
            _input = new StreamReader(client.GetStream() );
            
            GetRequestMethod();
            GetRequestHeaders();
        }

        /// <summary>The output <c>StreamWriter</c> associated with the request.</summary>
        public StreamWriter Output
        {
            get { return _output; }
        }

        /// <summary>The input <c>StreamReader</c> associated with the request.</summary>
        public StreamReader Input
        {
            get { return _input; }
        }

        /// <summary>The <c>TcpClient</c> with the request.</summary>
        public TcpClient Client
        {
            get { return _client; }
        }

        private String _filePath
        {
            get { return __filePath; }
            set
            {
                __filePath = value;
                _filePathDir = null;
                _filePathFile = null;
            }
        }

        /// <summary>The type of HTTP request (i.e. PUT, GET, etc.).</summary>
        public String HttpMethod
        {
            get { return _httpMethod; }
        }

        /// <summary>The level of the HTTP protocol.</summary>
        public String Protocol
        {
            get { return _protocol; }
        }

        /// <summary>The "path" which is part of any HTTP request.</summary>
        public String FilePath
        {
            get { return _filePath; }
        }

        /// <summary>The file portion of the "path" which is part of any HTTP request.</summary>
        public String FilePathFile
        {
            get
            {
                if (_filePathFile != null)
                    return _filePathFile;

                int i = FilePath.LastIndexOf("/");

                if (i == -1)
                    return "";

                i++;
                _filePathFile = FilePath.Substring(i, FilePath.Length - i);
                return _filePathFile;
            }
        }

        /// <summary>The directory portion of the "path" which is part of any HTTP request.</summary>
        public String FilePathDir
        {
            get
            {
                if (_filePathDir != null)
                    return _filePathDir;

                int i = FilePath.LastIndexOf("/");

                if (i == -1)
                    return "";

                i++;
                _filePathDir = FilePath.Substring(0, i);
                return _filePathDir;
            }
        }

        private void GetRequestMethod()
        {
            string req = _input.ReadLine();
            if (req == null)
                throw new ApplicationException("Void request.");

            if (0 == String.Compare("GET ", req.Substring(0, 4), true))
                _httpMethod = "GET";
            else if (0 == String.Compare("POST ", req.Substring(0, 5), true))
                _httpMethod = "POST";
            else
                throw new InvalidOperationException("Unrecognized method in query: " + req);

            req = req.TrimEnd();
            int idx = req.IndexOf(' ') + 1;
            if (idx >= req.Length)
                throw new ApplicationException("What do you want?");

            string page_protocol = req.Substring(idx);
            int idx2 = page_protocol.IndexOf(' ');
            if (idx2 == -1)
                idx2 = page_protocol.Length;

            _filePath = page_protocol.Substring(0, idx2).Trim();
            _protocol = page_protocol.Substring(idx2).Trim();
        }

        private void GetRequestHeaders()
        {
            String line;
            int idx;

            _headers = new Hashtable();

            while ((line = _input.ReadLine()) != "")
            {
                if (line == null)
                {
                    break;
                }

                idx = line.IndexOf(':');
                if (idx == -1 || idx == line.Length - 1)
                {
                    Logger.WriteEntry("Malformed header line: " + line, LogLevel.Information);
                    continue;
                }

                String key = line.Substring(0, idx);
                String value = line.Substring(idx + 1);

                try
                {
                    _headers.Add(key, value);
                }
                catch (Exception)
                {
                    Logger.WriteEntry("Duplicate header key in line: " + line, LogLevel.Information);
                }
            }
        }

        /// <summary>
        /// Format the object contents into a useful string representation.
        /// </summary>
        ///<returns><c>String</c> representation of the <c>SimpleHttpRequest</c> as the <i>HttpMethod FilePath Protocol</i>.</returns>
        override public String ToString()
        {
            return HttpMethod + " " + FilePath + " " + Protocol;
        }

        /// <summary>
        /// Close the <c>SimpleHttpRequest</c>. This flushes and closes all associated io streams.
        /// </summary>
        public void Close()
        {
            _output.Flush();
            _output.Close();
            _input.Close();
            _client.Close();
        }
    }
}
