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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System.Net;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using System.Text;
using Nini.Config;
using OpenSim.Framework;

namespace OpenSim.Framework.RegionLoader.Web
{
    public class RegionLoaderWebServer : IRegionLoader
    {
        private IniConfigSource m_configSouce;
        public void SetIniConfigSource(IniConfigSource configSource)
        {
            m_configSouce = configSource;
        }
        public RegionInfo[] LoadRegions()
        {
            if (m_configSouce == null)
            {
                Console.MainLog.Instance.Error("Unable to load configuration source! (WebServer Region Loader)");
                return null;
            }
            else
            {
                IniConfig startupConfig = (IniConfig)m_configSouce.Configs["Startup"];
                string url = startupConfig.GetString("regionload_webserver_url","").Trim();
                if (url == "")
                {
                    Console.MainLog.Instance.Error("Unable to load webserver URL - URL was empty (WebServer Region Loader");
                    return null;
                }
                else
                {
                    
                    HttpWebRequest webRequest = (HttpWebRequest)WebRequest.Create(url);
                    webRequest.Timeout = 30000; //30 Second Timeout
                    Console.MainLog.Instance.Debug("Sending Download Request...");
                    HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();
                    Console.MainLog.Instance.Debug("Downloading Region Information From Remote Server...");
                    StreamReader reader = new StreamReader(webResponse.GetResponseStream());
                    string xmlSource = "";
                    string tempStr = reader.ReadLine();
                    while (tempStr != null)
                    {
                        xmlSource = xmlSource + tempStr;
                        tempStr = reader.ReadLine();
                    }
                    Console.MainLog.Instance.Debug("Done downloading region information from server. Total Bytes: " + xmlSource.Length);
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(xmlSource);
                    if (xmlDoc.FirstChild.Name == "Regions")
                    {
                        RegionInfo[] regionInfos = new RegionInfo[xmlDoc.FirstChild.ChildNodes.Count];
                        int i;
                        for (i = 0; i < xmlDoc.FirstChild.ChildNodes.Count; i++)
                        {
                            Console.MainLog.Instance.Debug(xmlDoc.FirstChild.ChildNodes[i].OuterXml);
                            regionInfos[i] = new RegionInfo("REGION CONFIG #" + (i + 1), xmlDoc.FirstChild.ChildNodes[i]);
                        }

                        return regionInfos;
                    }
                    return null;
                }
            }
        }
    }
}
