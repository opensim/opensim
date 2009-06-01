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
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;

namespace OpenSim.GridLaunch.GUI.Network
{
    internal class Client
    {
        public TcpClient tcpClient;
        private byte[] readBuffer = new byte[4096];
        private byte[] writeBuffer;
        private TCPD tcp;
        private string inputData = "";
        private object inputDataLock = new object();
        public Client(TCPD _tcp, TcpClient Client)
        {
            tcp = _tcp;
            tcpClient = Client;
            asyncReadStart();
            Write("OpenSim TCP Console GUI");
            Write("Use commands /0, /1, /2, etc to switch between applications.");
            Write("Type /list for list of applications.");
            Write("Anything that doesn't start with a / will be sent to selected application");
            Write("type /quit to exit");

        }

        private void asyncReadStart()
        {
            tcpClient.GetStream().BeginRead(readBuffer, 0, readBuffer.Length, asyncReadCallBack, null);
        }

        //private Regex LineExtractor = new Regex("^(.*)$")
        private void asyncReadCallBack(IAsyncResult ar)
        {
            try
            {
                // Read data
                int len = tcpClient.GetStream().EndRead(ar);

                // Send it to app
                string newData = System.Text.Encoding.ASCII.GetString(readBuffer, 0, len);
                //lock (inputDataLock)
                //{
                inputData += newData;
                if (newData.Contains("\n"))
                    SendInputLines();
                //}

                // Start it again
                asyncReadStart();
            }
            catch
            {
                // TODO: Remove client when we get exception
                // Temp patch: if exception we don't call asyncReadStart()
            }
        }

        private void SendInputLines()
        {
            StringBuilder line = new StringBuilder();
            foreach (char c in inputData)
            {
                if (c == 13)
                    continue;
                if (c == 10)
                {
                    Program.WriteLine(tcp.currentApp, line.ToString());
                    line = new StringBuilder();
                    continue;
                }
                line.Append(c);
            }
            // We'll keep whatever is left over
            inputData = line.ToString();
        }

        public void Write(string Text)
        {
            writeBuffer = Encoding.ASCII.GetBytes(Text);
            tcpClient.GetStream().Write(writeBuffer, 0, writeBuffer.Length);
        }
    }
}
