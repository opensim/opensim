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
using System.Xml.Serialization;

namespace OpenSim.ApplicationPlugins.Rest.Regions
{
    public partial class RestRegionPlugin : RestPlugin
    {
        private static XmlSerializerNamespaces _xmlNs;

        static RestRegionPlugin()
        {
            _xmlNs = new XmlSerializerNamespaces();
            _xmlNs.Add(String.Empty, String.Empty);
        }

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
        public override void Initialise(OpenSimBase openSim)
        {
            try
            {
                base.Initialise(openSim);
                if (!IsEnabled)
                {
                    //m_log.WarnFormat("{0} Rest Plugins are disabled", MsgID);
                    return;
                }
                
                m_log.InfoFormat("{0} REST region plugin enabled", MsgID);

                // add REST method handlers
                AddRestStreamHandler("GET", "/regions/", GetHandler);
                AddRestStreamHandler("POST", "/regions/", PostHandler);
                AddRestStreamHandler("GET", "/regioninfo/", GetRegionInfoHandler);
            }
            catch (Exception e)
            {
                m_log.WarnFormat("{0} Initialization failed: {1}", MsgID, e.Message);
                m_log.DebugFormat("{0} Initialization failed: {1}", MsgID, e.ToString());
            }
        }

        public override void Close()
        {
        }
        #endregion overriding methods
    }
}
