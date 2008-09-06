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
using System.Diagnostics;
using System.Net;
using System.Text;
using System.Web;
using OpenMetaverse;
using OpenSim.Region.ScriptEngine.Common.TRPC;

namespace OpenSim.Region.ScriptEngine.Common
{
    public class TRPC_Remote
    {
        public readonly int MaxQueueSize = 1024 * 10;
        public readonly TCPCommon.ServerAndClientInterface TCPS;

        public delegate void ReceiveCommandDelegate(int ID, string Command, params object[] p);
        public event ReceiveCommandDelegate ReceiveCommand;
        Dictionary<string, Type> TypeDictionary = new Dictionary<string, Type>();
        Type[] Types =
        {
            typeof(String),
            typeof(Int16),
            typeof(Int32),
            typeof(Int64),
            typeof(Double),
            typeof(Decimal),
            typeof(Array),
            typeof(UUID),
            typeof(UInt16),
            typeof(UInt32),
            typeof(UInt64)
        };

        // TODO: Maybe we should move queue into TCPSocket so we won't have to keep one queue instance per connection
        private Dictionary<int, InQueueStruct> InQueue = new Dictionary<int, InQueueStruct>();
        private class InQueueStruct
        {
            public byte[] Queue;
            public int QueueSize;
            public object QueueLockObject = new object();
        }

        public TRPC_Remote(TCPCommon.ServerAndClientInterface TCPClientOrServer)
        {
            TCPS = TCPClientOrServer;
            TCPS.Close += new TCPCommon.CloseDelegate(TCPS_Close);
            TCPS.ClientConnected += new TCPCommon.ClientConnectedDelegate(TCPS_ClientConnected);
            TCPS.DataReceived += new TCPCommon.DataReceivedDelegate(TCPS_DataReceived);
            //TCPS.StartListen();

            // Make a lookup dictionary for types
            foreach (Type t in Types)
            {
                TypeDictionary.Add(t.ToString(), t);
            }
        }

        void TCPS_ClientConnected(int ID, EndPoint Remote)
        {
            // Create a incoming queue for this connection
            InQueueStruct iq = new InQueueStruct();
            iq.Queue = new byte[MaxQueueSize];
            iq.QueueSize = 0;
            InQueue.Add(ID, iq);
        }

        void TCPS_Close(int ID)
        {
            // Remove queue
            InQueue.Remove(ID);
        }

        void TCPS_DataReceived(int ID, byte[] data, int offset, int length)
        {
            // Copy new data to incoming queue
            lock (InQueue[ID].QueueLockObject)
            {
                Array.Copy(data, offset, InQueue[ID].Queue, InQueue[ID].QueueSize, length);
                InQueue[ID].QueueSize += length;

                // Process incoming queue
                ProcessQueue(ID);
            }
        }

        private void ProcessQueue(int ID)
        {
            // This is just a temp implementation -- not so fast :)

            InQueueStruct myIQS = InQueue[ID];
            if (myIQS.QueueSize == 0)
                return;

            string receivedData = Encoding.UTF8.GetString(myIQS.Queue, 0, myIQS.QueueSize);
            Debug.WriteLine("RAW: " + receivedData);

            byte newLine = 10;
            while (true)
            {
                bool ShouldProcess = false;
                int lineEndPos = 0;

                // Look for newline
                for (int i = 0; i < myIQS.QueueSize; i++)
                {
                    if (myIQS.Queue[i] == newLine)
                    {
                        ShouldProcess = true;
                        lineEndPos = i;
                        break;
                    }
                }

                // Process it?
                if (!ShouldProcess)
                    return;
                // Yes
                string cmdLine = Encoding.ASCII.GetString(myIQS.Queue, 0, lineEndPos);
                Debug.WriteLine("Command: " + cmdLine);

                // Fix remaining queue in an inefficient way
                byte[] newQueue = new byte[MaxQueueSize];
                Array.Copy(myIQS.Queue, lineEndPos, newQueue, 0, myIQS.QueueSize - lineEndPos);
                myIQS.Queue = newQueue;
                myIQS.QueueSize -= (lineEndPos + 1);

                // Now back to the command
                string[] parts = cmdLine.Split(',');
                if (parts.Length > 0)
                {
                    string cmd = parts[0];
                    int paramCount = parts.Length - 1;
                    object[] param = null;

                    if (paramCount > 0)
                    {
                        // Process all parameters (decoding them from URL encoding)
                        param = new object[paramCount];
                        for (int i = 1; i < parts.Length; i++)
                        {
                            string[] spl;
                            spl = HttpUtility.UrlDecode(parts[i]).Split('|');
                            string t = spl[0];
                            param[i - 1] = Convert.ChangeType(spl[1], TypeLookup(t));
                        }
                    }

                    ReceiveCommand(ID, cmd, param);
                }
            }
        }

        private Type TypeLookup(string t)
        {
            Type ret = TypeDictionary[t];
            if (ret != null)
                return ret;
            return typeof(object);
        }

        public void SendCommand(int ID, string Command, params object[] p)
        {
            // Call PacketFactory to have it create a packet for us

            //string[] tmpP = new string[p.Length];
            string tmpStr = Command;
            for (int i = 0; i < p.Length; i++)
            {
                tmpStr += "," + p[i].GetType().ToString() + "|" + HttpUtility.UrlEncode(p[i].ToString()); // .Replace(",", "%44")
            }
            tmpStr += "\n";
            byte[] byteData = Encoding.UTF8.GetBytes(tmpStr);
            TCPS.Send(ID, byteData, 0, byteData.Length);
        }
    }
}
