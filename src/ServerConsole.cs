/*
Copyright (c) OpenSim project, http://osgrid.org/

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
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Net;
using libsecondlife;
using libsecondlife.Packets;

namespace OpenSim
{
	public class ServerConsole {
		private ConsoleType ConsType;
		StreamWriter Log;
		
		public enum ConsoleType {
			Local,		// Use stdio
			TCP,		// Use TCP/telnet
			SimChat		// Use in-world chat (for gods)
		}

		
		// STUPID HACK ALERT!!!! STUPID HACK ALERT!!!!!
		// constype - the type of console to use (see enum ConsoleType)
		// sparam - depending on the console type:
		//		TCP - the IP to bind to (127.0.0.1 if blank)
		//		Local - param ignored
		//		SimChat - the AgentID of this sim's admin
		// and for the iparam:
		//		TCP - the port to bind to
		//		Local - param ignored
		//		SimChat - the chat channel to accept commands from
		public ServerConsole(ConsoleType constype, string sparam, int iparam) {
			ConsType = constype;
			switch(constype) {
				case ConsoleType.Local:
				Console.WriteLine("ServerConsole.cs - creating new local console");
				Console.WriteLine("Logs will be saved to current directory in opensim-console.log");
				Log=File.AppendText("opensim-console.log");
				Log.WriteLine("========================================================================");
				Log.WriteLine("OpenSim " + VersionInfo.Version + " Started at " + DateTime.Now.ToString());
				break;
				case ConsoleType.TCP:
				break;
				case ConsoleType.SimChat:
				break;
				
				default:
					Console.WriteLine("ServerConsole.cs - what are you smoking? that isn't a valid console type!");
				break;
			}
		}

		public void Close() {
			Log.WriteLine("OpenSim shutdown at " + DateTime.Now.ToString());
			Log.Close();
		}
	
		// You know what ReadLine() and WriteLine() do, right? And Read() and Write()? Right, you do actually know C#, right? Are you actually a programmer? Do you know english? Do you find my sense of humour in comments irritating? Good, glad you're still here
		public void WriteLine(string Line) {
			Log.WriteLine(Line);
			Console.WriteLine(Line);
			return;
		}
		
		public string ReadLine() {
			string TempStr=Console.ReadLine();
			Log.WriteLine(TempStr);
			return TempStr;
		}

		public int Read() {
			int TempInt= Console.Read();
			Log.Write((char)TempInt);
			return TempInt;
		}

		public void Write(string Line) {
			Console.Write(Line);
			Log.Write(Line);
			return;
		}

		// Displays a command prompt and waits for the user to enter a string, then returns that string
		public string CmdPrompt(string prompt) {
			this.Write(prompt);
			return this.ReadLine();
		}

		// Displays a command prompt and returns a default value if the user simply presses enter
		public string CmdPrompt(string prompt, string defaultresponse) {
			string temp=CmdPrompt(prompt);
			if(temp=="") {
				 return defaultresponse;
			} else {
				return temp;
			}
		}

		// Displays a command prompt and returns a default value, user may only enter 1 of 2 options
		public string CmdPrompt(string prompt, string defaultresponse, string OptionA, string OptionB) {
			bool itisdone=false;
			string temp=CmdPrompt(prompt,defaultresponse);
			while(itisdone==false) {
				if((temp==OptionA) || (temp==OptionB)) {
					itisdone=true;
				} else {
					this.WriteLine("Valid options are " + OptionA + " or " + OptionB);
					temp=CmdPrompt(prompt,defaultresponse);
				}
			}
			return temp;
		}

		// Runs a command with a number of parameters
		public Object RunCmd(string Cmd, string[] cmdparams) {
			switch(Cmd) {
				case "help":
				this.WriteLine("show users - show info about connected users");
				this.WriteLine("shutdown - disconnect all clients and shutdown");
				break;
				
				case "show":
				ShowCommands(cmdparams[0]);
				break;
				
				case "shutdown":
				OpenSim_Main.Shutdown();
				break;
			}
			return null;
		}

		// Shows data about something
		public void ShowCommands(string ShowWhat) {
			switch(ShowWhat) {
                                case "uptime":
				this.WriteLine("OpenSim has been running since " + OpenSim_Main.startuptime.ToString());
                                this.WriteLine("That is " + (DateTime.Now-OpenSim_Main.startuptime).ToString());
				break;
				case "users":
				OpenSim.world.Avatar TempAv;
				this.WriteLine(String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}","Firstname", "Lastname","Agent ID", "Session ID", "Circuit", "IP"));
				foreach (libsecondlife.LLUUID UUID in OpenSim_Main.local_world.Entities.Keys) {
					TempAv=(OpenSim.world.Avatar)OpenSim_Main.local_world.Entities[UUID];
					this.WriteLine(String.Format("{0,-16}{1,-16}{2,-25}{3,-25}{4,-16},{5,-16}",TempAv.firstname, TempAv.lastname,UUID, TempAv.ControllingClient.SessionID, TempAv.ControllingClient.CircuitCode, TempAv.ControllingClient.userEP.ToString()));
				}
				break;
			}
		}
	
		// Displays a prompt to the user and then runs the command they entered
		public void MainConsolePrompt() {
			string[] tempstrarray;
			string tempstr = this.CmdPrompt("OpenSim-" + OpenSim_Main.cfg.RegionHandle.ToString() + " # ");
			tempstrarray = tempstr.Split(' ');
			string cmd=tempstrarray[0];
			Array.Reverse(tempstrarray);
			Array.Resize<string>(ref tempstrarray,tempstrarray.Length-1);
			Array.Reverse(tempstrarray);
			string[] cmdparams=(string[])tempstrarray;
			RunCmd(cmd,cmdparams);
		}
	}

}
