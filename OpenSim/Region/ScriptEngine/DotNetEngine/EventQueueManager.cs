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
/* Original code: Tedd Hansen */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Reflection;
using OpenSim.Region.Environment.Scenes.Scripting;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    /// <summary>
    /// EventQueueManager handles event queues
    /// Events are queued and executed in separate thread
    /// </summary>
    [Serializable]
    class EventQueueManager
    {
        private Thread EventQueueThread;
        private int NothingToDoSleepms = 200;
        private Queue<QueueItemStruct> EventQueue = new Queue<QueueItemStruct>();
        private struct QueueItemStruct
        {
            public IScriptHost ObjectID;
            public string ScriptID;
            public string FunctionName;
            public object[] param;
        }

        private ScriptEngine myScriptEngine;
        public EventQueueManager(ScriptEngine _ScriptEngine)
        {
            myScriptEngine = _ScriptEngine;
            //myScriptEngine.m_logger.Verbose("ScriptEngine", "EventQueueManager Start");
            // Start worker thread
            EventQueueThread = new Thread(EventQueueThreadLoop);
            EventQueueThread.IsBackground = true;
            EventQueueThread.Name = "EventQueueManagerThread";
            EventQueueThread.Start();
        }
        ~EventQueueManager()
        {
            // Kill worker thread
            if (EventQueueThread != null && EventQueueThread.IsAlive == true)
            {
                try
                {
                    EventQueueThread.Abort();
                    EventQueueThread.Join();
                }
                catch (Exception e)
                {
                    myScriptEngine.Log.Verbose("ScriptEngine", "EventQueueManager Exception killing worker thread: " + e.ToString());
                }
            }
            // Todo: Clean up our queues

        }

        private void EventQueueThreadLoop()
        {
            //myScriptEngine.m_logger.Verbose("ScriptEngine", "EventQueueManager Worker thread spawned");
            try
            {
                while (true)
                {
                    if (EventQueue.Count == 0)
                    {
                        // Nothing to do? Sleep a bit waiting for something to do
                        Thread.Sleep(NothingToDoSleepms);
                    }
                    else
                    {
                        // Something in queue, process
                        QueueItemStruct QIS = EventQueue.Dequeue();
                        //myScriptEngine.m_logger.Verbose("ScriptEngine", "Processing event for ObjectID: " + QIS.ObjectID + ", ScriptID: " + QIS.ScriptID + ", FunctionName: " + QIS.FunctionName);
                        // TODO: Execute function
                        myScriptEngine.myScriptManager.ExecuteEvent(QIS.ObjectID, QIS.ScriptID, QIS.FunctionName, QIS.param);
                    }
                }
            }
            catch (ThreadAbortException tae)
            {
                myScriptEngine.Log.Verbose("ScriptEngine", "EventQueueManager Worker thread killed: " + tae.Message);
            }
        }

        public void AddToObjectQueue(IScriptHost ObjectID, string FunctionName, object[] param)
        {
            // Determine all scripts in Object and add to their queue
            //myScriptEngine.m_logger.Verbose("ScriptEngine", "EventQueueManager Adding ObjectID: " + ObjectID + ", FunctionName: " + FunctionName);

            foreach (string ScriptID in myScriptEngine.myScriptManager.GetScriptKeys(ObjectID))
            {
                // Add to each script in that object
                // TODO: Some scripts may not subscribe to this event. Should we NOT add it? Does it matter?

                // Create a structure and add data
                QueueItemStruct QIS = new QueueItemStruct();
                QIS.ObjectID = ObjectID;
                QIS.ScriptID = ScriptID;
                QIS.FunctionName = FunctionName;
                QIS.param = param;

                // Add it to queue
                EventQueue.Enqueue(QIS);
                
            }
        }
        //public void AddToScriptQueue(string ObjectID, string FunctionName, object[] param)
        //{
        //    // Add to script queue
        //}

    }
}
