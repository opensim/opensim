/*
 * Created by SharpDevelop.
 * User: Adam Stevenson
 * Date: 6/13/2007
 * Time: 12:55 AM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace OpenSim.Servers
{
	/// <summary>
	/// 
	/// </summary>
	public class SocketRegistry
	{
		static List<Socket> _Sockets;

        static SocketRegistry()
        {
            _Sockets = new List<Socket>();
        }

		private SocketRegistry()
		{
			
		}
		
		public static void Register(Socket pSocket)
		{
            _Sockets.Add(pSocket);
		}

        public static void Unregister(Socket pSocket)
        {
            _Sockets.Remove(pSocket);
        }

        public static void UnregisterAllAndClose()
        {
            int iSockets = _Sockets.Count;

            for (int i = 0; i < iSockets; i++)
            {
                try
                {
                    _Sockets[i].Close();
                }
                catch
                {

                }
            }

            _Sockets.Clear();
        }
    }
}
