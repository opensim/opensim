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
        private string MainPath = "00334-0000/";
        private string MapLayerPath = "00334-0001/";
        private BaseHttpServer httpListener;

        public Caps(BaseHttpServer httpServer, string httpListen, uint httpPort)
        {
            httpListener = httpServer;
            httpListenerAddress = httpListen;
            httpListenPort = httpPort;
        }

        /// <summary>
        /// 
        /// </summary>
        public void RegisterHandlers()
        {
            Console.WriteLine("registering caps handlers");
            httpListener.AddRestHandler("POST", "/CAPS/" + MainPath, CapsRequest);
            httpListener.AddRestHandler("POST", "/CAPS/" + MapLayerPath, MapLayer);
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
            Console.WriteLine("Caps request " + request);
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
            string capURLS = "<key>MapLayer</key><string>http://" + httpListenerAddress + ":" + httpListenPort.ToString() + "/CAPS/" + MapLayerPath + "</string>";
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
            Console.WriteLine("Caps MapLayer request " + request);
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

            string res= "<map><key>Left</key><integer>"+left+"</integer><key>Bottom</key><integer>"+bottom +"</integer><key>Top</key><integer>"+top+"</integer><key>Right</key><integer>"+right+"</integer><key>ImageID</key><uuid>"+image.ToStringHyphenated()+"</uuid></map>";
            return res;
        }
    }
}
