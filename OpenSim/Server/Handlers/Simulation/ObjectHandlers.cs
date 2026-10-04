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
using System.Reflection;
using System.Net;
using OpenSim.Services.Interfaces;
using GridRegion = OpenSim.Services.Interfaces.GridRegion;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using log4net;


namespace OpenSim.Server.Handlers.Simulation
{
    public class ObjectSimpleHandler : SimpleStreamHandler
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private ISimulationService m_SimulationService;
        protected bool m_Proxy = false;

        public ObjectSimpleHandler(ISimulationService service) : base("/object")
        {
            m_SimulationService = service;
        }

        protected override void ProcessRequest(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse)
        {
            httpResponse.KeepAlive = false;

            if (m_SimulationService == null)
            {
                httpResponse.StatusCode = (int)HttpStatusCode.InternalServerError;
                httpResponse.RawBuffer = Utils.falseStrBytes;
                return;
            }

            /*this things are ignored
            if (!Utils.GetParams(httpRequest.UriPath, out UUID objectID, out UUID regionID, out string action))
            {
                m_log.InfoFormat("[OBJECT HANDLER]: Invalid parameters for object message {0}", httpRequest.UriPath);
                httpResponse.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }
            */

            switch (httpRequest.HttpMethod)
            {
                case "POST":
                {
                    OSDMap args = Utils.DeserializeJSONOSMap(httpRequest);
                    if (args == null)
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        httpResponse.RawBuffer = Utils.falseStrBytes;
                        return;
                    }
                    DoObjectPost(args, httpResponse);
                    break;
                }
                case "DELETE":
                default:
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                    return;
                }
            }
        }

        protected void DoObjectPost(OSDMap args, IOSHttpResponse httpResponse)
        {
            // retrieve the input arguments
            if(!args.TryGetUUID("destination_uuid", out UUID uuid))
                return;

            IScene s = m_SimulationService.GetScene(uuid);
            if (s == null)
                return;

            if (!args.TryGetString("sog", out string sogXmlStr) || sogXmlStr.Length == 0 )
                return;

            args.TryGetInt("destination_x", out int x);
            args.TryGetInt("destination_y", out int y);
            args.TryGetString("destination_name" , out string regionname);
            args.TryGetVector3("new_position", out Vector3 newPosition);

            args.TryGetString("extra", out string extraStr);

            ISceneObject sog;
            try
            {
                //m_log.DebugFormat("[OBJECT HANDLER]: received {0}", sogXmlStr);
                sog = s.DeserializeObject(sogXmlStr);
                sog.ExtraFromXmlString(extraStr);
            }
            catch (Exception ex)
            {
                m_log.InfoFormat("[OBJECT HANDLER]: exception on deserializing scene object {0}", ex.Message);
                httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            sog.HasGroupChanged = args.TryGetBool("modified", out bool sogMod) && sogMod;

            if (args.TryGetString("state", out string stateXmlStr) && s.AllowScriptCrossings)
            {
                if (stateXmlStr.Length > 0)
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

            GridRegion destination = new()
            {
                RegionID = uuid,
                RegionLocX = x,
                RegionLocY = y,
                RegionName = regionname
            };

            bool result;
            try
            {
                // This is the meaning of POST object
                result = CreateObject(destination, newPosition, sog);
            }
            catch (Exception e)
            {
                m_log.DebugFormat("[OBJECT HANDLER]: Exception in CreateObject: {0}", e.StackTrace);
                result = false;
            }

            httpResponse.StatusCode = (int)HttpStatusCode.OK;
            httpResponse.RawBuffer = Util.UTF8.GetBytes(result.ToString());
        }

        // subclasses can override this
        protected virtual bool CreateObject(GridRegion destination, Vector3 newPosition, ISceneObject sog)
        {
            return m_SimulationService.CreateObject(destination, newPosition, sog, false);
        }
    }
}
