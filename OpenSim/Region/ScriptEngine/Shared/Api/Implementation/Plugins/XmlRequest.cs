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
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.CoreModules.Scripting.XMLRPC;
using OpenSim.Region.ScriptEngine.Interfaces;
using OpenSim.Region.ScriptEngine.Shared;
using OpenSim.Region.ScriptEngine.Shared.Api;

namespace OpenSim.Region.ScriptEngine.Shared.Api.Plugins
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
                RPCRequestInfo rInfo = (RPCRequestInfo)xmlrpc.GetNextCompletedRequest();

                while (rInfo != null)
                {
                    xmlrpc.RemoveCompletedRequest(rInfo.GetMessageID());

                    //Deliver data to prim's remote_data handler
                    object[] resobj = new object[]
                    {
                        new LSL_Types.LSLInteger(2),
                        new LSL_Types.LSLString(
                                rInfo.GetChannelKey().ToString()),
                        new LSL_Types.LSLString(
                                rInfo.GetMessageID().ToString()),
                        new LSL_Types.LSLString(String.Empty),
                        new LSL_Types.LSLInteger(rInfo.GetIntValue()),
                        new LSL_Types.LSLString(rInfo.GetStrVal())
                    };

                    foreach (IScriptEngine e in m_CmdManager.ScriptEngines)
                    {
                        if (e.PostScriptEvent(
                                rInfo.GetItemID(), new EventParams(
                                    "remote_data", resobj,
                                    new DetectParams[0])))
                            break;
                    }

                    rInfo = (RPCRequestInfo)xmlrpc.GetNextCompletedRequest();
                }

                SendRemoteDataRequest srdInfo = (SendRemoteDataRequest)xmlrpc.GetNextCompletedSRDRequest();

                while (srdInfo != null)
                {
                    xmlrpc.RemoveCompletedSRDRequest(srdInfo.GetReqID());

                    //Deliver data to prim's remote_data handler
                    object[] resobj = new object[]
                    {
                        new LSL_Types.LSLInteger(3),
                        new LSL_Types.LSLString(srdInfo.Channel.ToString()),
                        new LSL_Types.LSLString(srdInfo.GetReqID().ToString()),
                        new LSL_Types.LSLString(String.Empty),
                        new LSL_Types.LSLInteger(srdInfo.Idata),
                        new LSL_Types.LSLString(srdInfo.Sdata)
                    };

                    foreach (IScriptEngine e in m_CmdManager.ScriptEngines)
                    {
                        if (e.PostScriptEvent(
                                srdInfo.ItemID, new EventParams(
                                    "remote_data", resobj,
                                    new DetectParams[0])))
                            break;
                    }

                    srdInfo = (SendRemoteDataRequest)xmlrpc.GetNextCompletedSRDRequest();
                }
            }
        }
    }
}
