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
using System.Threading;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Timers;
using System.Xml;
using System.Xml.Serialization;
using libsecondlife;
using Nwc.XmlRpc;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Communications;
using OpenSim.Region.Environment.Scenes;
using OpenSim.ApplicationPlugins.Rest;

namespace OpenSim.ApplicationPlugins.Rest.Regions
{

    public partial class RestRegionPlugin : RestPlugin
    {
        #region POST methods
        public string PostHandler(string request, string path, string param,
                                  OSHttpRequest httpRequest, OSHttpResponse httpResponse)
        {
            // foreach (string h in httpRequest.Headers.AllKeys)
            //     foreach (string v in httpRequest.Headers.GetValues(h))
            //         m_log.DebugFormat("{0} IsGod: {1} -> {2}", MsgID, h, v);

            MsgID = RequestID;
            m_log.DebugFormat("{0} POST path {1} param {2}", MsgID, path, param);

            try
            {
                // param empty: new region post
                if (!IsGod(httpRequest))
                    // XXX: this needs to be turned into a FailureUnauthorized(...)
                    return Failure(httpResponse, OSHttpStatusCode.ClientErrorUnauthorized,
                                   "GET", "you are not god");

                if (String.IsNullOrEmpty(param)) return CreateRegion(httpRequest, httpResponse);

                return Failure(httpResponse, OSHttpStatusCode.ClientErrorNotFound,
                               "POST", "url {0} not supported", param);
            }
            catch (Exception e)
            {
                return Failure(httpResponse, OSHttpStatusCode.ServerErrorInternalError, "POST", e);
            }
        }

        public string CreateRegion(OSHttpRequest request, OSHttpResponse response)
        {
            XmlWriter.WriteStartElement(String.Empty, "regions", String.Empty);
            foreach (Scene s in App.SceneManager.Scenes)
            {
                XmlWriter.WriteStartElement(String.Empty, "uuid", String.Empty);
                XmlWriter.WriteString(s.RegionInfo.RegionID.ToString());
                XmlWriter.WriteEndElement();
            }
            XmlWriter.WriteEndElement();

            return XmlWriterResult;
        }
        #endregion POST methods
    }
}
