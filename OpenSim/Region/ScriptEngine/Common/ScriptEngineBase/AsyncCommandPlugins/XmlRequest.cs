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
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules.Scripting.XMLRPC;
using OpenSim.Region.ScriptEngine.Shared;

namespace OpenSim.Region.ScriptEngine.Common.ScriptEngineBase.AsyncCommandPlugins
{
    public class XmlRequest
    {
        public AsyncCommandManager m_CmdManager;

        public XmlRequest(AsyncCommandManager CmdManager)
        {
            m_CmdManager = CmdManager;
        }

        public void CheckXMLRPCRequests()
        {
            if (m_CmdManager.m_ScriptEngine.World == null)
                return;

            IXMLRPC xmlrpc = m_CmdManager.m_ScriptEngine.World.RequestModuleInterface<IXMLRPC>();
            if (null == xmlrpc)
                return;

            // Process the completed request queue
            RPCRequestInfo rInfo = xmlrpc.GetNextCompletedRequest();

            while (rInfo != null)
            {
                bool handled = false;

                // Request must be taken out of the queue in case there is no handler, otherwise we loop infinitely
                xmlrpc.RemoveCompletedRequest(rInfo.GetMessageID());

                // And since the xmlrpc request queue is actually shared among all regions on the simulator, we need
                // to look in each one for the appropriate handler
                foreach (ScriptEngine sman in ScriptEngine.ScriptEngines) {
                    if (sman.m_ScriptManager.GetScript(rInfo.GetLocalID(),rInfo.GetItemID()) != null) {

                        //Deliver data to prim's remote_data handler
                        object[] resobj = new object[]
                        {
                            new LSL_Types.LSLInteger(2), new LSL_Types.LSLString(rInfo.GetChannelKey().ToString()), new LSL_Types.LSLString(rInfo.GetMessageID().ToString()), new LSL_Types.LSLString(String.Empty),
                            new LSL_Types.LSLInteger(rInfo.GetIntValue()),
                            new LSL_Types.LSLString(rInfo.GetStrVal())
                        };
                        sman.m_EventQueueManager.AddToScriptQueue(
                            rInfo.GetLocalID(), rInfo.GetItemID(), "remote_data", EventQueueManager.llDetectNull, resobj
                        );

                        handled = true;
                    }
                }

                if (! handled)
                {
                    Console.WriteLine("Unhandled xml_request: " + rInfo.GetItemID());
                }

                rInfo = xmlrpc.GetNextCompletedRequest();
            }

            // Process the send queue
            SendRemoteDataRequest srdInfo = xmlrpc.GetNextCompletedSRDRequest();

            while (srdInfo != null)
            {
                bool handled = false;

                // Request must be taken out of the queue in case there is no handler, otherwise we loop infinitely
                xmlrpc.RemoveCompletedSRDRequest(srdInfo.GetReqID());

                // And this is another shared queue... so we check each of the script engines for a handler
                foreach (ScriptEngine sman in ScriptEngine.ScriptEngines)
                {
                    if (sman.m_ScriptManager.GetScript(srdInfo.m_localID,srdInfo.m_itemID) != null) {

                        //Deliver data to prim's remote_data handler
                        object[] resobj = new object[]
                        {
                            new LSL_Types.LSLInteger(3), new LSL_Types.LSLString(srdInfo.channel.ToString()), new LSL_Types.LSLString(srdInfo.GetReqID().ToString()), new LSL_Types.LSLString(String.Empty),
                            new LSL_Types.LSLInteger(srdInfo.idata),
                            new LSL_Types.LSLString(srdInfo.sdata)
                        };
                        sman.m_EventQueueManager.AddToScriptQueue(
                            srdInfo.m_localID, srdInfo.m_itemID, "remote_data", EventQueueManager.llDetectNull, resobj
                        );

                        handled = true;
                    }
                }

                if (! handled)
                {
                    Console.WriteLine("Unhandled xml_srdrequest: " + srdInfo.GetReqID());
                }

                srdInfo = xmlrpc.GetNextCompletedSRDRequest();
            }
        }
    }
}
