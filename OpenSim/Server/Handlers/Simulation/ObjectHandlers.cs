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
using System.Collections;
using System.IO;
using System.Reflection;
using System.Net;
using System.Text;

using OpenSim.Server.Base;
using OpenSim.Server.Handlers.Base;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Nini.Config;
using log4net;


namespace OpenSim.Server.Handlers.Simulation
{
    public class ObjectHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private ISimulationService m_SimulationService;

        public ObjectHandler(ISimulationService sim)
        {
            m_SimulationService = sim;
        }

        public Hashtable Handler(Hashtable request)
        {
            m_log.Debug("[CONNECTION DEBUGGING]: ObjectHandler Called");

            m_log.Debug("---------------------------");
            m_log.Debug(" >> uri=" + request["uri"]);
            m_log.Debug(" >> content-type=" + request["content-type"]);
            m_log.Debug(" >> http-method=" + request["http-method"]);
            m_log.Debug("---------------------------\n");

            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/html";

            UUID objectID;
            string action;
            ulong regionHandle;
            if (!Utils.GetParams((string)request["uri"], out objectID, out regionHandle, out action))
            {
                m_log.InfoFormat("[REST COMMS]: Invalid parameters for object message {0}", request["uri"]);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            // Next, let's parse the verb
            string method = (string)request["http-method"];
            if (method.Equals("POST"))
            {
                DoObjectPost(request, responsedata, regionHandle);
                return responsedata;
            }
            else if (method.Equals("PUT"))
            {
                DoObjectPut(request, responsedata, regionHandle);
                return responsedata;
            }
            //else if (method.Equals("DELETE"))
            //{
            //    DoObjectDelete(request, responsedata, agentID, action, regionHandle);
            //    return responsedata;
            //}
            else
            {
                m_log.InfoFormat("[REST COMMS]: method {0} not supported in object message", method);
                responsedata["int_response_code"] = HttpStatusCode.MethodNotAllowed;
                responsedata["str_response_string"] = "Mthod not allowed";

                return responsedata;
            }

        }

        protected virtual void DoObjectPost(Hashtable request, Hashtable responsedata, ulong regionhandle)
        {
            OSDMap args = Utils.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            string sogXmlStr = "", extraStr = "", stateXmlStr = "";
            if (args["sog"] != null)
                sogXmlStr = args["sog"].AsString();
            if (args["extra"] != null)
                extraStr = args["extra"].AsString();

            IScene s = m_SimulationService.GetScene(regionhandle);
            ISceneObject sog = null;
            try
            {
                //sog = SceneObjectSerializer.FromXml2Format(sogXmlStr);
                sog = s.DeserializeObject(sogXmlStr);
                sog.ExtraFromXmlString(extraStr);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[REST COMMS]: exception on deserializing scene object {0}", ex.Message);
                responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                responsedata["str_response_string"] = "Bad request";
                return;
            }

            if ((args["state"] != null) && s.AllowScriptCrossings)
            {
                stateXmlStr = args["state"].AsString();
                if (stateXmlStr != "")
                {
                    try
                    {
                        sog.SetState(stateXmlStr, s);
                    }
                    catch (Exception ex)
                    {
                        m_log.InfoFormat("[REST COMMS]: exception on setting state for scene object {0}", ex.Message);
                        // ignore and continue
                    }
                }
            }
            // This is the meaning of POST object
            bool result = m_SimulationService.CreateObject(regionhandle, sog, false);

            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = result.ToString();
        }

        protected virtual void DoObjectPut(Hashtable request, Hashtable responsedata, ulong regionhandle)
        {
            OSDMap args = Utils.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }

            UUID userID = UUID.Zero, itemID = UUID.Zero;
            if (args["userid"] != null)
                userID = args["userid"].AsUUID();
            if (args["itemid"] != null)
                itemID = args["itemid"].AsUUID();

            // This is the meaning of PUT object
            bool result = m_SimulationService.CreateObject(regionhandle, userID, itemID);

            responsedata["int_response_code"] = 200;
            responsedata["str_response_string"] = result.ToString();
        }

    }
}