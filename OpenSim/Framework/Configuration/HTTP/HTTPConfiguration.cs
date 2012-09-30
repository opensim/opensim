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
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using log4net;
using OpenSim.Framework.Configuration.XML;

namespace OpenSim.Framework.Configuration.HTTP
{
    public class HTTPConfiguration : IGenericConfig
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private RemoteConfigSettings remoteConfigSettings;

        private XmlConfiguration xmlConfig;

        private string configFileName = String.Empty;

        public HTTPConfiguration()
        {
            remoteConfigSettings = new RemoteConfigSettings("remoteconfig.xml");
            xmlConfig = new XmlConfiguration();
        }

        public void SetFileName(string fileName)
        {
            configFileName = fileName;
        }

        public void LoadData()
        {
            try
            {
                StringBuilder sb = new StringBuilder();

                byte[] buf = new byte[8192];
                HttpWebRequest request =
                    (HttpWebRequest) WebRequest.Create(remoteConfigSettings.baseConfigURL + configFileName);
                HttpWebResponse response = (HttpWebResponse) request.GetResponse();

                Stream resStream = response.GetResponseStream();

                string tempString = null;
                int count = 0;

                do
                {
                    count = resStream.Read(buf, 0, buf.Length);
                    if (count != 0)
                    {
                        tempString = Util.UTF8.GetString(buf, 0, count);
                        sb.Append(tempString);
                    }
                } while (count > 0);
                LoadDataFromString(sb.ToString());
            }
            catch (WebException)
            {
                m_log.Warn("Unable to connect to remote configuration file (" +
                                      remoteConfigSettings.baseConfigURL + configFileName +
                                      "). Creating local file instead.");
                xmlConfig.SetFileName(configFileName);
                xmlConfig.LoadData();
            }
        }

        public void LoadDataFromString(string data)
        {
            xmlConfig.LoadDataFromString(data);
        }

        public string GetAttribute(string attributeName)
        {
            return xmlConfig.GetAttribute(attributeName);
        }

        public bool SetAttribute(string attributeName, string attributeValue)
        {
            return true;
        }

        public void Commit()
        {
        }

        public void Close()
        {
        }
    }
}
