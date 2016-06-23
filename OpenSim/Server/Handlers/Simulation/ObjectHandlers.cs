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
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
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

        public ObjectHandler() { }

        public ObjectHandler(ISimulationService sim)
        {
            m_SimulationService = sim;
        }

        public Hashtable Handler(Hashtable request)
        {
            //m_log.Debug("[CONNECTION DEBUGGING]: ObjectHandler Called");

            //m_log.Debug("---------------------------");
            //m_log.Debug(" >> uri=" + request["uri"]);
            //m_log.Debug(" >> content-type=" + request["content-type"]);
            //m_log.Debug(" >> http-method=" + request["http-method"]);
            //m_log.Debug("---------------------------\n");

            Culture.SetCurrentCulture();

            Hashtable responsedata = new Hashtable();
            responsedata["content_type"] = "text/html";

            UUID objectID;
            UUID regionID;
            string action;
            if (!Utils.GetParams((string)request["uri"], out objectID, out regionID, out action))
            {
                m_log.InfoFormat("[OBJECT HANDLER]: Invalid parameters for object message {0}", request["uri"]);
                responsedata["int_response_code"] = 404;
                responsedata["str_response_string"] = "false";

                return responsedata;
            }

            try
            {
                // Next, let's parse the verb
                string method = (string)request["http-method"];
                if (method.Equals("POST"))
                {
                    DoObjectPost(request, responsedata, regionID);
                    return responsedata;
                }
                //else if (method.Equals("DELETE"))
                //{
                //    DoObjectDelete(request, responsedata, agentID, action, regionHandle);
                //    return responsedata;
                //}
                else
                {
                    m_log.InfoFormat("[OBJECT HANDLER]: method {0} not supported in object message", method);
                    responsedata["int_response_code"] = HttpStatusCode.MethodNotAllowed;
                    responsedata["str_response_string"] = "Method not allowed";

                    return responsedata;
                }
            }
            catch (Exception e)
            {
                m_log.WarnFormat("[OBJECT HANDLER]: Caught exception {0}", e.StackTrace);
                responsedata["int_response_code"] = HttpStatusCode.InternalServerError;
                responsedata["str_response_string"] = "Internal server error";

                return responsedata;

            }
        }

        protected void DoObjectPost(Hashtable request, Hashtable responsedata, UUID regionID)
        {
            OSDMap args = Utils.GetOSDMap((string)request["body"]);
            if (args == null)
            {
                responsedata["int_response_code"] = 400;
                responsedata["str_response_string"] = "false";
                return;
            }
            // retrieve the input arguments
            int x = 0, y = 0;
            UUID uuid = UUID.Zero;
            string regionname = string.Empty;
            Vector3 newPosition = Vector3.Zero;

            if (args.ContainsKey("destination_x") && args["destination_x"] != null)
                Int32.TryParse(args["destination_x"].AsString(), out x);
            if (args.ContainsKey("destination_y") && args["destination_y"] != null)
                Int32.TryParse(args["destination_y"].AsString(), out y);
            if (args.ContainsKey("destination_uuid") && args["destination_uuid"] != null)
                UUID.TryParse(args["destination_uuid"].AsString(), out uuid);
            if (args.ContainsKey("destination_name") && args["destination_name"] != null)
                regionname = args["destination_name"].ToString();
            if (args.ContainsKey("new_position") && args["new_position"] != null)
                Vector3.TryParse(args["new_position"], out newPosition);

            GridRegion destination = new GridRegion();
            destination.RegionID = uuid;
            destination.RegionLocX = x;
            destination.RegionLocY = y;
            destination.RegionName = regionname;

            string sogXmlStr = "", extraStr = "", stateXmlStr = "";
            if (args.ContainsKey("sog") && args["sog"] != null)
                sogXmlStr = args["sog"].AsString();
            if (args.ContainsKey("extra") && args["extra"] != null)
                extraStr = args["extra"].AsString();

            IScene s = m_SimulationService.GetScene(destination.RegionID);
            ISceneObject sog = null;
            try
            {
                //m_log.DebugFormat("[OBJECT HANDLER]: received {0}", sogXmlStr);
                sog = s.DeserializeObject(sogXmlStr);
                sog.ExtraFromXmlString(extraStr);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[OBJECT HANDLER]: exception on deserializing scene object {0}", ex.Message);
                responsedata["int_response_code"] = HttpStatusCode.BadRequest;
                responsedata["str_response_string"] = "Bad request";
                return;
            }

            if (args.ContainsKey("modified"))
                sog.HasGroupChanged = args["modified"].AsBoolean();
            else
                sog.HasGroupChanged = false;

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
                        m_log.InfoFormat("[OBJECT HANDLER]: exception on setting state for scene object {0}", ex.Message);
                        // ignore and continue
                    }
                }
            }

            bool result = false;
            try
            {
                // This is the meaning of POST object
                result = CreateObject(destination, newPosition, sog);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[OBJECT HANDLER]: Exception in CreateObject: {0}", e.StackTrace);
            }

            responsedata["int_response_code"] = HttpStatusCode.OK;
            responsedata["str_response_string"] = result.ToString();
        }

        // subclasses can override this
        protected virtual bool CreateObject(GridRegion destination, Vector3 newPosition, ISceneObject sog)
        {
            return m_SimulationService.CreateObject(destination, newPosition, sog, false);
        }
    }
}
