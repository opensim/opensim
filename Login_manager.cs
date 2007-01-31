/*Copyright (c) 2007 Michael Wright

* Copyright (c) <year>, <copyright holder>
* All rights reserved.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Second_server
{
	/// <summary>
	/// Description of Login_manager.
	/// </summary>
	public class Login_manager
	{
		public Login_manager()
		{
			
		}
		
		public ushort loginPort = 8080;
		public IPAddress clientAddress = IPAddress.Loopback;
		public IPAddress remoteAddress = IPAddress.Any;
		private Socket loginServer;
		private Random RandomClass = new Random();

		// InitializeLoginProxy: initialize the login proxy
		private void InitializeLoginProxy() {
			loginServer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			loginServer.Bind(new IPEndPoint(remoteAddress, loginPort));
			loginServer.Listen(1);
		}
		
		public void startup()
		{
			this.InitializeLoginProxy();
			Thread runLoginProxy = new Thread(new ThreadStart(RunLoginProxy));
			runLoginProxy.IsBackground = true;
			runLoginProxy.Start();
		}
		
			private void RunLoginProxy() {
		    try {
			for (;;) {
				Socket client = loginServer.Accept();
				IPEndPoint clientEndPoint = (IPEndPoint)client.RemoteEndPoint;

				
				NetworkStream networkStream = new NetworkStream(client);
				StreamReader networkReader = new StreamReader(networkStream);
				StreamWriter networkWriter = new StreamWriter(networkStream);

				try 
				{
					ProxyLogin(networkReader, networkWriter);
				} 
				catch (Exception e) 
				{
					Console.WriteLine(e.Message);
				}

				networkWriter.Close();
				networkReader.Close();
				networkStream.Close();

				client.Close();

				// send any packets queued for injection
				
			}
		    } catch (Exception e) {
			Console.WriteLine(e.Message);
			Console.WriteLine(e.StackTrace);
		    }
		}

		// ProxyLogin: proxy a login request
		private void ProxyLogin(StreamReader reader, StreamWriter writer) { lock(this) {
			string line;
			int contentLength = 0;

			// read HTTP header
			do {
				// read one line of the header
				line = reader.ReadLine();

				// check for premature EOF
				if (line == null)
					throw new Exception("EOF in client HTTP header");

				// look for Content-Length
				Match match = (new Regex(@"Content-Length: (\d+)$")).Match(line);
				if (match.Success)
					contentLength = Convert.ToInt32(match.Groups[1].Captures[0].ToString());
			} while (line != "");
			System.Console.WriteLine(line);
			// read the HTTP body into a buffer
			char[] content = new char[contentLength];
			reader.Read(content, 0, contentLength);


			// forward the XML-RPC response to the client
            writer.WriteLine("HTTP/1.0 200 OK");
            writer.WriteLine("Content-type: text/xml");
            writer.WriteLine();
			
			
			StreamReader SR;
    		string lines;
    		SR=File.OpenText("login.dat");
    		lines=SR.ReadLine();
    		writer.WriteLine(lines);
    		lines=SR.ReadLine();
    		int ran=this.RandomClass.Next(1,9999);
    		
    		lines="<member><name>session_id</name><value><string>99998888-"+ran.ToString("0000")+"-4f52-8ec1-0b1d5cd6aead</string></value></member>";
    		writer.WriteLine(lines);
    		lines=SR.ReadLine();
    		writer.WriteLine(lines);
    		lines=SR.ReadLine();
    		 ran=this.RandomClass.Next(1,9999);
    		lines="<member><name>agent_id</name><value><string>aaaabbbb-8932-"+ran.ToString("0000")+"-8664-58f53e442797</string></value></member>";
    		writer.WriteLine(lines);
    		lines=SR.ReadLine();
    		
    		while(lines!="end-mfile")
    		{
    		writer.WriteLine(lines);
    		lines=SR.ReadLine();
    		}
    		SR.Close();
			}
		}
	}
}
