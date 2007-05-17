using Nwc.XmlRpc;
using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenGridServices.Manager
{
	public class GridServerConnectionManager
	{
		private string ServerURL;
		
		public bool Connect(string GridServerURL, string username, string password)
		{
			try {
				this.ServerURL=GridServerURL;
				Hashtable LoginParamsHT = new Hashtable();
				LoginParamsHT["username"]=username;
				LoginParamsHT["password"]=password;
				ArrayList LoginParams = new ArrayList();
				LoginParams.Add(LoginParamsHT);
				XmlRpcRequest GridLoginReq = new XmlRpcRequest("manager_login",LoginParams);
				XmlRpcResponse GridResp = GridLoginReq.Send(ServerURL,3000);
				if(GridResp.IsFault) {
					return false;
				} else {
					return true;
				}
			} catch(Exception e) {
				Console.WriteLine(e.ToString());
				return false;
			}
		}
	}
}
