using System;
using System.Collections;
using System.Collections.Generic;
using Nwc.XmlRpc;
using System.Threading;

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
           
			responseData["msg"]="Shutdown command accepted";
			(new Thread(new ThreadStart(ZOMGServerIsNowTerminallyIll))).Start();

	    		response.Value = responseData;
	    		return response;
		}

		// Brought to by late-night coding
		public static void ZOMGServerIsNowTerminallyIll()
		{
			Console.WriteLine("ZOMG! THIS SERVER IS TERMINALLY ILL - WE GOT A SHUTDOWN REQUEST FROM A GRID MANAGER!!!!");
			Console.WriteLine("We have 3 seconds to live...");
			Thread.Sleep(3000);
			thecallback("shutdown");
		}
	 }
}

