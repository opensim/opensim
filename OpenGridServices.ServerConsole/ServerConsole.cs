/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
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
* 
*/
using System;

namespace ServerConsole
{
	public class MainConsole {
		
		private static ConsoleBase instance;
		
		public static ConsoleBase Instance 
		{
			get 
			{
				return instance;
			}
			set
			{
				instance = value;
			}
		}
		
		public MainConsole()
		{
			
		}
	}
	
	public abstract class conscmd_callback {
		public abstract void RunCmd(string cmd, string[] cmdparams);
		public abstract void Show(string ShowWhat);
	}

	public abstract class ConsoleBase
	{
		
		public enum ConsoleType {
			Local,		// Use stdio
			TCP,		// Use TCP/telnet
			SimChat		// Use in-world chat (for gods)
		}

		public abstract void Close();
	
		// You know what ReadLine() and WriteLine() do, right? And Read() and Write()? Right, you do actually know C#, right? Are you actually a programmer? Do you know english? Do you find my sense of humour in comments irritating? Good, glad you're still here
		public abstract void WriteLine(string Line) ;
		
		public abstract string ReadLine();

		public abstract int Read() ;

		public abstract void Write(string Line) ;

		public abstract string PasswdPrompt(string prompt);

		// Displays a command prompt and waits for the user to enter a string, then returns that string
		public abstract string CmdPrompt(string prompt) ;
		
		// Displays a command prompt and returns a default value if the user simply presses enter
		public abstract string CmdPrompt(string prompt, string defaultresponse);

		// Displays a command prompt and returns a default value, user may only enter 1 of 2 options
		public abstract string CmdPrompt(string prompt, string defaultresponse, string OptionA, string OptionB) ;
		
		// Runs a command with a number of parameters
		public abstract Object RunCmd(string Cmd, string[] cmdparams) ;

		// Shows data about something
		public abstract void ShowCommands(string ShowWhat) ;
	
		// Displays a prompt to the user and then runs the command they entered
		public abstract void MainConsolePrompt() ;
	}
}
