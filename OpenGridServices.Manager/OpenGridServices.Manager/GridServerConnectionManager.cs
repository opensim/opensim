using Nwc.XmlRpc;
using System;
using System.Collections;
using System.Collections.Generic;
using libsecondlife;

namespace OpenGridServices.Manager
{
	public class GridServerConnectionManager
	{
		private string ServerURL;
		public LLUUID SessionID;
		
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
					Hashtable gridrespData = (Hashtable)GridResp.Value;
					this.SessionID = new LLUUID((string)gridrespData["session_id"]);
					return true;
				}
			} catch(Exception e) {
				Console.WriteLine(e.ToString());
				return false;
			}
		}
		
		public bool RestartServer()
		{
			return true;
		}
		
		public bool ShutdownServer()
		{
			try {
				Hashtable ShutdownParamsHT = new Hashtable();
				ArrayList ShutdownParams = new ArrayList();
				ShutdownParams.Add(ShutdownParamsHT);
				XmlRpcRequest GridShutdownReq = new XmlRpcRequest("shutdown",ShutdownParams);
				XmlRpcResponse GridResp = GridShutdownReq.Send(this.ServerURL,3000);
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
