using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;

using OpenSim.Framework.Configuration.Interfaces;

namespace OpenSim.Framework.Configuration.HTTP
{
    public class HTTPConfiguration : IGenericConfig
    {
        RemoteConfigSettings remoteConfigSettings;

        XmlConfiguration xmlConfig;

        private string configFileName = "";

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
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(this.remoteConfigSettings.baseConfigURL + this.configFileName);
                HttpWebResponse response = (HttpWebResponse)request.GetResponse();

                Stream resStream = response.GetResponseStream();

                string tempString = null;
                int count = 0;

                do
                {
                    count = resStream.Read(buf, 0, buf.Length);
                    if (count != 0)
                    {
                        tempString = Encoding.ASCII.GetString(buf, 0, count);
                        sb.Append(tempString);
                    }
                }
                while (count > 0);
                LoadDataFromString(sb.ToString());
            }
            catch (Exception e)
            {
                Console.MainLog.Instance.Warn("Unable to connect to remote configuration file (" + remoteConfigSettings.baseConfigURL + configFileName + "). Creating local file instead.");
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
