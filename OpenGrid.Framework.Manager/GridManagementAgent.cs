using Nwc.XmlRpc;
using OpenSim.Framework;
using OpenSim.Servers;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;

namespace OpenGrid.Framework.Manager {

	public delegate void GridManagerCallback(string param);
	
	public class GridManagementAgent {

		private GridManagerCallback thecallback;
		private string sendkey;
		private string recvkey;
		private string component_type;

		private static ArrayList Sessions;

		public GridManagementAgent(BaseHttpServer app_httpd, string component_type, string sendkey, string recvkey, GridManagerCallback thecallback)
		{
			this.sendkey=sendkey;
			this.recvkey=recvkey;
			this.component_type=component_type;
			this.thecallback=thecallback;
			Sessions = new ArrayList();

			app_httpd.AddXmlRPCHandler("manager_login",XmlRpcLoginMethod);
	
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

		public static bool SessionExists(LLUUID sessionID)
		{
			return Sessions.Contains(sessionID);
		}

		public static XmlRpcResponse XmlRpcLoginMethod(XmlRpcRequest request)
                {
                        XmlRpcResponse response = new XmlRpcResponse();
                        Hashtable requestData = (Hashtable)request.Params[0];
                        Hashtable responseData = new Hashtable();
	
			// TODO: Switch this over to using OpenGrid.Framework.Data
			if( requestData["username"].Equals("admin") && requestData["password"].Equals("supersecret")) {
				response.IsFault=false;
				LLUUID new_session=LLUUID.Random();
				Sessions.Add(new_session);
				responseData["session_id"]=new_session.ToString();
				responseData["msg"]="Login OK";
			} else {
				response.IsFault=true;
				responseData["error"]="Invalid username or password";
			}
			
                        response.Value = responseData;
                        return response;

		}

	}
}
