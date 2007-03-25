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
using System.IO;
using System.Text;
using libsecondlife;
using ServerConsole;

namespace OpenGridServices
{
	/// <summary>
	/// </summary>
	public class OpenGrid_Main
	{
		
		public static OpenGrid_Main thegrid;
		public string GridOwner;
		public string DefaultStartupMsg;
		public string DefaultAssetServer;
		public string AssetSendKey;
		public string AssetRecvKey;
		public string DefaultUserServer;
		public string UserSendKey;
		public string UserRecvKey;
	
		public GridHTTPServer _httpd;
		public SimProfileManager _regionmanager;

		[STAThread]
		public static void Main( string[] args )
		{
			Console.WriteLine("Starting...\n");
			ServerConsole.MainConsole.Instance = new MServerConsole(ServerConsole.ConsoleBase.ConsoleType.Local, "", 0, "opengrid-console.log", "OpenGrid", new GridConsole());

			thegrid = new OpenGrid_Main();
			thegrid.Startup();
	
			ServerConsole.MainConsole.Instance.WriteLine("\nEnter help for a list of commands\n");
	
			while(true) {
				ServerConsole.MainConsole.Instance.MainConsolePrompt();
			}
		}
	
		public void Startup() {
			ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - Please press enter to retain default settings");

                        this.GridOwner=ServerConsole.MainConsole.Instance.CmdPrompt("Grid owner [OGS development team]: ","OGS development team");
			this.DefaultStartupMsg=ServerConsole.MainConsole.Instance.CmdPrompt("Default startup message for clients [Welcome to OGS!]: ","Welcome to OGS!");
			
			this.DefaultAssetServer=ServerConsole.MainConsole.Instance.CmdPrompt("Default asset server [no default]: ");
			this.AssetSendKey=ServerConsole.MainConsole.Instance.CmdPrompt("Key to send to asset server: ");
			this.AssetRecvKey=ServerConsole.MainConsole.Instance.CmdPrompt("Key to expect from asset server: ");
		
			this.DefaultUserServer=ServerConsole.MainConsole.Instance.CmdPrompt("Default user server [no default]: ");
			this.UserSendKey=ServerConsole.MainConsole.Instance.CmdPrompt("Key to send to user server: ");
			this.UserRecvKey=ServerConsole.MainConsole.Instance.CmdPrompt("Key to expect from user server: ");
	
			ServerConsole.MainConsole.Instance.WriteLine("Main.cs:Startup() - Starting HTTP process");
			_httpd = new GridHTTPServer();
			
			this._regionmanager=new SimProfileManager();
			_regionmanager.CreateNewProfile("OpenSim Test", "http://there-is-no-caps.com", "4.78.190.75", 9000, 997, 996, this.UserSendKey, this.UserRecvKey);

		}	
	}
	/// <summary>
	/// Description of ServerConsole.
	/// </summary>
	public class MServerConsole : ConsoleBase
	{
			
		private ConsoleType ConsType;
		StreamWriter Log;
		public conscmd_callback cmdparser;
		public string componentname;
		
		// STUPID HACK ALERT!!!! STUPID HACK ALERT!!!!!
		// constype - the type of console to use (see enum ConsoleType)
		// sparam - depending on the console type:
		//		TCP - the IP to bind to (127.0.0.1 if blank)
		//		Local - param ignored
		// and for the iparam:
		//		TCP - the port to bind to
		//		Local - param ignored
		// LogFile - duh
		// componentname - which component of the OGS system? (user, asset etc)
		// cmdparser - a reference to a conscmd_callback object
	
		public MServerConsole(ConsoleType constype, string sparam, int iparam, string LogFile, string componentname, conscmd_callback cmdparser) {
			ConsType = constype;
			this.componentname = componentname;
			this.cmdparser = cmdparser;
			switch(constype) {
				case ConsoleType.Local:
				Console.WriteLine("ServerConsole.cs - creating new local console");
				Console.WriteLine("Logs will be saved to current directory in " + LogFile);
				Log=File.AppendText(LogFile);
				Log.WriteLine("========================================================================");
				Log.WriteLine(componentname + " Started at " + DateTime.Now.ToString());
				break;
				
				case ConsoleType.TCP:
				break;
				
				default:
					Console.WriteLine("ServerConsole.cs - what are you smoking? that isn't a valid console type!");
				break;
			}
		}

		public override void Close() {
			Log.WriteLine("Shutdown at " + DateTime.Now.ToString());
			Log.Close();
		}
	
		// You know what ReadLine() and WriteLine() do, right? And Read() and Write()? Right, you do actually know C#, right? Are you actually a programmer? Do you know english? Do you find my sense of humour in comments irritating? Good, glad you're still here
		public override void WriteLine(string Line) {
			Log.WriteLine(Line);
			Console.WriteLine(Line);
			return;
		}
		
		public override string ReadLine() {
			string TempStr=Console.ReadLine();
			Log.WriteLine(TempStr);
			return TempStr;
		}

		public override int Read() {
			int TempInt= Console.Read();
			Log.Write((char)TempInt);
			return TempInt;
		}

		public override void Write(string Line) {
			Console.Write(Line);
			Log.Write(Line);
			return;
		}

		
		// Displays a prompt and waits for the user to enter a string, then returns that string
		// Done with no echo and suitable for passwords
                public override string PasswdPrompt(string prompt) {
                        // FIXME: Needs to be better abstracted
			Log.WriteLine(prompt);
			this.Write(prompt);
                	ConsoleColor oldfg=Console.ForegroundColor;
			Console.ForegroundColor=Console.BackgroundColor;
			string temp=Console.ReadLine();
			Console.ForegroundColor=oldfg;
                	return temp;
		}

		// Displays a command prompt and waits for the user to enter a string, then returns that string
		public override string CmdPrompt(string prompt) {
			this.Write(prompt);
			return this.ReadLine();
		}

		// Displays a command prompt and returns a default value if the user simply presses enter
		public override string CmdPrompt(string prompt, string defaultresponse) {
			string temp=CmdPrompt(prompt);
			if(temp=="") {
				 return defaultresponse;
			} else {
				return temp;
			}
		}

		// Displays a command prompt and returns a default value, user may only enter 1 of 2 options
		public override string CmdPrompt(string prompt, string defaultresponse, string OptionA, string OptionB) {
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
		public override Object RunCmd(string Cmd, string[] cmdparams) {
			cmdparser.RunCmd(Cmd, cmdparams);
			return null;
		}

		// Shows data about something
		public override void ShowCommands(string ShowWhat) {
			cmdparser.Show(ShowWhat);
		}

                public override void MainConsolePrompt() {
                        string[] tempstrarray;
                        string tempstr = this.CmdPrompt(this.componentname + "# ");
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
