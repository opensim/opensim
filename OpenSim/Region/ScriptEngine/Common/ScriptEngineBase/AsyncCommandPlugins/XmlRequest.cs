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
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using libsecondlife;
using Axiom.Math;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Modules;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework;

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

            if (xmlrpc != null)
            {
                RPCRequestInfo rInfo = xmlrpc.GetNextCompletedRequest();

                while (rInfo != null)
                {
                    if (m_CmdManager.m_ScriptEngine.m_ScriptManager.GetScript(rInfo.GetLocalID(), rInfo.GetItemID()) != null)
                    {
                        xmlrpc.RemoveCompletedRequest(rInfo.GetMessageID());

                        //Deliver data to prim's remote_data handler
                        object[] resobj = new object[]
                        {
                            2, rInfo.GetChannelKey().ToString(), rInfo.GetMessageID().ToString(), String.Empty,
                            rInfo.GetIntValue(),
                            rInfo.GetStrVal()
                        };
                        m_CmdManager.m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                            rInfo.GetLocalID(), rInfo.GetItemID(), "remote_data", EventQueueManager.llDetectNull, resobj
                        );
                    }

                    rInfo = xmlrpc.GetNextCompletedRequest();
                }

                SendRemoteDataRequest srdInfo = xmlrpc.GetNextCompletedSRDRequest();

                while (srdInfo != null)
                {
                    if (m_CmdManager.m_ScriptEngine.m_ScriptManager.GetScript(srdInfo.m_localID, srdInfo.m_itemID) != null)
                    {
                        xmlrpc.RemoveCompletedSRDRequest(srdInfo.GetReqID());

                        //Deliver data to prim's remote_data handler
                        object[] resobj = new object[]
                        {
                            3, srdInfo.channel.ToString(), srdInfo.GetReqID().ToString(), String.Empty,
                            srdInfo.idata,
                            srdInfo.sdata
                        };
                        m_CmdManager.m_ScriptEngine.m_EventQueueManager.AddToScriptQueue(
                            srdInfo.m_localID, srdInfo.m_itemID, "remote_data", EventQueueManager.llDetectNull, resobj
                        );
                    }

                    srdInfo = xmlrpc.GetNextCompletedSRDRequest();
                }
            }
        }
    }
}
