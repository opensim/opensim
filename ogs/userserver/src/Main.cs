/*
Copyright (c) OpenSim project, http://osgrid.org/


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
using System.Collections;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using ServerConsole;

namespace OpenGridServices
{
	/// <summary>
	/// </summary>
	public class OpenUser_Main
	{
		
		public static OpenUser_Main userserver;
		
		public UserHTTPServer _httpd;
		public UserProfileManager _profilemanager;
		public UserProfile GridGod;
		public string DefaultStartupMsg;
		public string GridURL;
		public string GridSendKey;
		public string GridRecvKey;

		public Dictionary<LLUUID, UserProfile> UserSessions = new Dictionary<LLUUID, UserProfile>();

		[STAThread]
		public static void Main( string[] args )
		{
			Console.WriteLine("OpenUser " + VersionInfo.Version + "\n");
			Console.WriteLine("Starting...\n");
			ServerConsole.MainConsole.Instance = new MServerConsole(ServerConsole.ConsoleBase.ConsoleType.Local, "", 0, "opengrid-console.log", "OpenUser", new UserConsole());

			userserver = new OpenUser_Main();
			userserver.Startup();
	
			ServerConsole.MainConsole.Instance.WriteLine("\nEnter help for a list of commands\n");
	
			while(true) {
				ServerConsole.MainConsole.Instance.MainConsolePrompt();
			}
		}
	
		public void Startup() {
			ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - Please press enter to retain default settings");

                        this.GridURL=ServerConsole.MainConsole.Instance.CmdPrompt("Grid URL: ");
			this.GridSendKey=ServerConsole.MainConsole.Instance.CmdPrompt("Key to send to grid: ");
			this.GridRecvKey=ServerConsole.MainConsole.Instance.CmdPrompt("Key to expect from grid: ");
		
			this.DefaultStartupMsg=ServerConsole.MainConsole.Instance.CmdPrompt("Default startup message for clients [Welcome to OGS!] :","Welcome to OGS!");

			ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - Creating user profile manager");
			_profilemanager = new UserProfileManager();
			_profilemanager.InitUserProfiles();
	
		
			string tempfirstname;
			string templastname;
			string tempMD5Passwd;
			ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - Please configure the grid god user:");
			tempfirstname=ServerConsole.MainConsole.Instance.CmdPrompt("First name: ");
			templastname=ServerConsole.MainConsole.Instance.CmdPrompt("Last name: ");
			tempMD5Passwd=ServerConsole.MainConsole.Instance.PasswdPrompt("Password: ");
		
			System.Security.Cryptography.MD5CryptoServiceProvider x = new System.Security.Cryptography.MD5CryptoServiceProvider();
			byte[] bs = System.Text.Encoding.UTF8.GetBytes(tempMD5Passwd);
			bs = x.ComputeHash(bs);
			System.Text.StringBuilder s = new System.Text.StringBuilder();
			foreach (byte b in bs)
			{
   				s.Append(b.ToString("x2").ToLower());
			}
			tempMD5Passwd = "$1$" + s.ToString();

			GridGod=_profilemanager.CreateNewProfile(tempfirstname,templastname,tempMD5Passwd);
			_profilemanager.SetGod(GridGod.UUID);
			GridGod.homelookat = new LLVector3(-0.57343f, -0.819255f, 0f);
			GridGod.homepos = new LLVector3(128f,128f,23f);

			ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - Starting HTTP process");
			_httpd = new UserHTTPServer();
		}	
	}
}
