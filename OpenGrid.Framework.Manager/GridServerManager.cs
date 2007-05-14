using System;
using System.Collections;
using System.Collections.Generic;
using Nwc.XmlRpc;


namespace OpenGrid.Framework.Manager {

	public class GridServerManager 
	{
		public static GridManagerCallback thecallback;

		public static string sendkey;
		public static string recvkey;

		public static XmlRpcResponse XmlRpcShutdownMethod(XmlRpcRequest request)
         	{
           		XmlRpcResponse response = new XmlRpcResponse();
            		Hashtable requestData = (Hashtable)request.Params[0];
	    		Hashtable responseData = new Hashtable();
           
	    		if(requestData["authkey"]!=recvkey) {
	    			responseData["error"]="INVALID KEY";
	    		} else {
				responseData["msg"]="Shutdown command accepted";
	    			responseData["authkey"]=sendkey;
	    			thecallback("shutdown");	
	    		} 


	    		response.Value = responseData;
	    		return response;
		}
	 }
}

