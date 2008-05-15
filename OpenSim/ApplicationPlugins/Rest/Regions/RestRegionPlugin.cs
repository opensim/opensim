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
* 
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
using Mono.Addins;
using Nwc.XmlRpc;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Communications;
using OpenSim.Region.Environment.Scenes;
using OpenSim.ApplicationPlugins.Rest;

[assembly : Addin]
[assembly : AddinDependency("OpenSim", "0.5")]

namespace OpenSim.ApplicationPlugins.Rest.Regions
{

    [Extension("/OpenSim/Startup")]
    public class RestRegionPlugin : RestPlugin
    {
        private static readonly log4net.ILog  _log = 
            log4net.LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        #region overriding properties
        public override string Name 
        { 
            get { return "REGION"; }
        }

        public override string ConfigName
        {
            get { return "RestRegionPlugin"; }
        }
        #endregion overriding properties

        #region overriding methods
        /// <summary>
        /// This method is called by OpenSimMain immediately after loading the
        /// plugin and after basic server setup,  but before running any server commands.
        /// </summary>
        /// <remarks>
        /// Note that entries MUST be added to the active configuration files before
        /// the plugin can be enabled.
        /// </remarks>
        public override void Initialise(OpenSimMain openSim)
        {
            try
            {
                base.Initialise(openSim);
                if (IsEnabled)                     
                    m_log.InfoFormat("{0} Rest Plugins Enabled", MsgID);
                else
                    m_log.WarnFormat("{0} Rest Plugins are disabled", MsgID);

                // add REST method handlers
                AddRestStreamHandler("GET", "/regions/", GetHandler);
            }
            catch (Exception e)
            {
                _log.WarnFormat("{0} Initialization failed: {1}", MsgID, e.Message);
                _log.DebugFormat("{0} Initialization failed: {1}", MsgID, e.ToString());
            }
        }

        public override void Close()
        {
        }
        #endregion overriding methods

        #region methods
        public string GetHandler(string request, string path, string param)
        {
            m_log.DebugFormat("{0} GET path {1} param {2}", MsgID, path, param);

            // param empty: regions list
            if (String.IsNullOrEmpty(param)) return GetHandlerRegions();

            return GetHandlerRegion(param);
        }

        public string GetHandlerRegions()
        {
            StringWriter sw = new StringWriter();
            XmlTextWriter xw = new XmlTextWriter(sw);
            xw.Formatting = Formatting.Indented;
            
            xw.WriteStartElement(String.Empty, "regions", String.Empty);
            foreach (Scene s in App.SceneManager.Scenes)
            {
                xw.WriteStartElement(String.Empty, "uuid", String.Empty);
                xw.WriteString(s.RegionInfo.RegionID.ToString());
                xw.WriteEndElement();
            }
            xw.WriteEndElement();
            xw.Close();
            
            return sw.ToString();
        }

        public string GetHandlerRegion(string param)
        {
            string[] comps = param.Split('/');
            LLUUID regionID = (LLUUID)comps[0];
            _log.DebugFormat("{0} region UUID {1}", MsgID, regionID.ToString());

            if (LLUUID.Zero == regionID) throw new Exception("missing region ID");

            Scene scene = null;
            App.SceneManager.TryGetScene(regionID, out scene);

            XmlSerializer xs = new XmlSerializer(typeof(RegionDetails));
            StringWriter sw = new StringWriter();
            XmlTextWriter xw = new XmlTextWriter(sw);
            xw.Formatting = Formatting.Indented;

            xs.Serialize(xw, new RegionDetails(scene.RegionInfo));
            xw.Close();
            
            return sw.ToString();
        }
        #endregion methods
    }
}
