using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Servers;

namespace OpenGrid.Framework.Manager {

	public delegate void GridManagerCallback(string param);
	
	public class GridManagementAgent {

		private GridManagerCallback thecallback;
		private string sendkey;
		private string recvkey;
		private string component_type;

		public GridManagementAgent(BaseHttpServer app_httpd, string component_type, string sendkey, string recvkey, GridManagerCallback thecallback)
		{
			this.sendkey=sendkey;
			this.recvkey=recvkey;
			this.component_type=component_type;
			this.thecallback=thecallback;

			switch(component_type)
			{
				case "gridserver":
				  GridServerManager.sendkey=this.sendkey;
				  GridServerManager.recvkey=this.recvkey;
				  GridServerManager.thecallback=thecallback;
				  app_httpd.AddXmlRPCHandler("shutdown", GridServerManager.XmlRpcShutdownMethod);
				break;
			}
		}

	}
}
