using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using libsecondlife;
using OpenSim.Region.ScriptEngine.Common.TRPC;

namespace OpenSim.Region.ScriptEngine.Common
{
    public class TRPC_Remote
    {
        public readonly int MaxQueueSize = 1024 * 10;
        public readonly TCPCommon.ServerAndClientInterface TCPS;

        public delegate void ReceiveCommandDelegate(int ID, string Command, params object[] p);
        public event ReceiveCommandDelegate ReceiveCommand;
        System.Collections.Generic.Dictionary<string, Type> TypeDictionary = new Dictionary<string, Type>();
        Type[] Types = 
                            {
                                typeof(System.String),
                                typeof(System.Int16),
                                typeof(System.Int32),
                                typeof(System.Int64),
                                typeof(System.Double),
                                typeof(System.Decimal),
                                typeof(System.Array),
                                typeof(LLUUID),
                                typeof(System.UInt16),
                                typeof(System.UInt32),
                                typeof(System.UInt64)
                            };

        // TODO: Maybe we should move queue into TCPSocket so we won't have to keep one queue instance per connection
        private System.Collections.Generic.Dictionary<int, InQueueStruct> InQueue = new Dictionary<int, InQueueStruct>();
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

        void TCPS_ClientConnected(int ID, System.Net.EndPoint Remote)
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

            string receivedData = Encoding.ASCII.GetString(myIQS.Queue, 0, myIQS.QueueSize);
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
                            spl = System.Web.HttpUtility.UrlDecode(parts[i]).Split('|');
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
                tmpStr += "," + p[i].GetType().ToString() + "|" + System.Web.HttpUtility.UrlEncode(p[i].ToString()); // .Replace(",", "%44")
            }
            tmpStr += "\n";
            byte[] byteData = Encoding.ASCII.GetBytes(tmpStr);
            TCPS.Send(ID, byteData, 0, byteData.Length);
        }
    }
}