using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Servers;
using libsecondlife;

namespace OpenSim.Region
{
    public class Caps
    {
        private string httpListenerAddress;
        private uint httpListenPort;
        private string capsObjectPath = "00001-";
        private string requestPath = "0000/";
        private string mapLayerPath = "0001/";
        private BaseHttpServer httpListener;
        private LLUUID agentID;

        public Caps(BaseHttpServer httpServer, string httpListen, uint httpPort, string capsPath, LLUUID agent)
        {
            capsObjectPath = capsPath;
            httpListener = httpServer;
            httpListenerAddress = httpListen;
            httpListenPort = httpPort;
            agentID = agent;
        }

        /// <summary>
        /// 
        /// </summary>
        public void RegisterHandlers()
        {
            Console.WriteLine("registering CAPS handlers");
            httpListener.AddRestHandler("POST", "/CAPS/" +capsObjectPath+ requestPath, CapsRequest);
            httpListener.AddRestHandler("POST", "/CAPS/" +capsObjectPath+ mapLayerPath, MapLayer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string CapsRequest(string request, string path, string param)
        {
            //Console.WriteLine("Caps Request " + request);
            string result = "<llsd><map>";
            result += this.GetCapabilities();
            result += "</map></llsd>";
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected string GetCapabilities()
        {
            string capURLS="";

            capURLS += "<key>MapLayer</key><string>http://" + httpListenerAddress + ":" + httpListenPort.ToString() + "/CAPS/" +capsObjectPath+ mapLayerPath + "</string>";
            
            return capURLS;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="request"></param>
        /// <param name="path"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public string MapLayer(string request, string path, string param)
        {

            string res = "<llsd><map><key>AgentData</key><map><key>Flags</key><integer>0</integer></map><key>LayerData</key><array>";
            res += this.BuildLLSDMapLayerResponse();
            res += "</array></map></llsd>";
            return res;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected string BuildLLSDMapLayerResponse()
        {
            string res = "";
            int left;
            int right;
            int top;
            int bottom;
            LLUUID image = null;

            left = 500;
            bottom = 500;
            top = 1500;
            right = 1500;
            image = new LLUUID("00000000-0000-0000-9999-000000000006");

            res += "<map><key>Left</key><integer>" + left + "</integer><key>Bottom</key><integer>" + bottom + "</integer><key>Top</key><integer>" + top + "</integer><key>Right</key><integer>" + right + "</integer><key>ImageID</key><uuid>" + image.ToStringHyphenated() + "</uuid></map>";
            
            return res;
        }
    }
}
