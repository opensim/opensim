/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
using System.Collections;
using System.Threading;
using libsecondlife;
using Nwc.XmlRpc;

namespace OpenSim.Framework.Manager {

    /// <summary>
    /// A remote management system for the grid server
    /// </summary>
	public class GridServerManager 
	{
        /// <summary>
        /// Triggers events from the grid manager
        /// </summary>
		public static GridManagerCallback thecallback;

        /// <summary>
        /// Security keys
        /// </summary>
		public static string sendkey;
		public static string recvkey;

        /// <summary>
        /// Disconnects the grid server and shuts it down
        /// </summary>
        /// <param name="request">XmlRpc Request</param>
        /// <returns>An XmlRpc response containing either a "msg" or an "error"</returns>
		public static XmlRpcResponse XmlRpcShutdownMethod(XmlRpcRequest request)
         	{
           		XmlRpcResponse response = new XmlRpcResponse();
            		Hashtable requestData = (Hashtable)request.Params[0];
	    		Hashtable responseData = new Hashtable();
         
			if(requestData.ContainsKey("session_id")) {
				if(GridManagementAgent.SessionExists(new LLUUID((string)requestData["session_id"]))) {
					responseData["msg"]="Shutdown command accepted";
					(new Thread(new ThreadStart(ShutdownServer))).Start();
				} else {
					response.IsFault=true;
					responseData["error"]="bad session ID";
				}
			} else {
				response.IsFault=true;
				responseData["error"]="no session ID";
			}

	    		response.Value = responseData;
	    		return response;
		}

		/// <summary>
        /// Shuts down the grid server
		/// </summary>
		public static void ShutdownServer()
		{
			Console.WriteLine("Shutting down the grid server - recieved a grid manager request");
            Console.WriteLine("Terminating in three seconds...");
			Thread.Sleep(3000);
			thecallback("shutdown");
		}
	 }
}

