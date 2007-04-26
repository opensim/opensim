using System;
using System.IO;

namespace OpenSim.Framework.Console
{
    public class ConsoleBase
    {
        StreamWriter Log;
        public conscmd_callback cmdparser;
        public string componentname;
        private bool disableOutput;

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

        public ConsoleBase(string LogFile, string componentname, conscmd_callback cmdparser, bool disableSystemConsole )
        {
            this.componentname = componentname;
            this.cmdparser = cmdparser;
            this.disableOutput = disableSystemConsole;
            System.Console.WriteLine("ServerConsole.cs - creating new local console");
            System.Console.WriteLine("Logs will be saved to current directory in " + LogFile);
            Log = File.AppendText(LogFile);
            Log.WriteLine("========================================================================");
            Log.WriteLine(componentname + " Started at " + DateTime.Now.ToString());
        }

        public void Close()
        {
            Log.WriteLine("Shutdown at " + DateTime.Now.ToString());
            Log.Close();
        }

        public void Write(string format, params object[] args)
        {
            Log.Write(format, args);
            if(!disableOutput)
            {
            System.Console.Write(format, args);
            }
            return;
        }

        public void WriteLine(string format, params object[] args)
        {
            Log.WriteLine(format, args);
            if(!disableOutput)
            {
            System.Console.WriteLine(format, args);
            }
            return;
        }

        public string ReadLine()
        {
            string TempStr = System.Console.ReadLine();
            Log.WriteLine(TempStr);
            return TempStr;
        }

        public int Read()
        {
            int TempInt = System.Console.Read();
            Log.Write((char)TempInt);
            return TempInt;
        }

        // Displays a prompt and waits for the user to enter a string, then returns that string
        // Done with no echo and suitable for passwords
        public string PasswdPrompt(string prompt)
        {
            // FIXME: Needs to be better abstracted
            Log.WriteLine(prompt);
            this.Write(prompt);
            ConsoleColor oldfg = System.Console.ForegroundColor;
            System.Console.ForegroundColor = System.Console.BackgroundColor;
            string temp = System.Console.ReadLine();
            System.Console.ForegroundColor = oldfg;
            return temp;
        }

        // Displays a command prompt and waits for the user to enter a string, then returns that string
        public string CmdPrompt(string prompt)
        {
            this.Write(String.Format("{0}: ", prompt));
            return this.ReadLine();
        }

        // Displays a command prompt and returns a default value if the user simply presses enter
        public string CmdPrompt(string prompt, string defaultresponse)
        {
            string temp = CmdPrompt(String.Format( "{0} [{1}]", prompt, defaultresponse ));
            if (temp == "")
            {
                return defaultresponse;
            }
            else
            {
                return temp;
            }
        }

        // Displays a command prompt and returns a default value, user may only enter 1 of 2 options
        public string CmdPrompt(string prompt, string defaultresponse, string OptionA, string OptionB)
        {
            bool itisdone = false;
            string temp = CmdPrompt(prompt, defaultresponse);
            while (itisdone == false)
            {
                if ((temp == OptionA) || (temp == OptionB))
                {
                    itisdone = true;
                }
                else
                {
                    this.WriteLine("Valid options are " + OptionA + " or " + OptionB);
                    temp = CmdPrompt(prompt, defaultresponse);
                }
            }
            return temp;
        }

        // Runs a command with a number of parameters
        public Object RunCmd(string Cmd, string[] cmdparams)
        {
            cmdparser.RunCmd(Cmd, cmdparams);
            return null;
        }

        // Shows data about something
        public void ShowCommands(string ShowWhat)
        {
            cmdparser.Show(ShowWhat);
        }

        public void MainConsolePrompt()
        {
            string[] tempstrarray;
            string tempstr = this.CmdPrompt(this.componentname + "# ");
            tempstrarray = tempstr.Split(' ');
            string cmd = tempstrarray[0];
            Array.Reverse(tempstrarray);
            Array.Resize<string>(ref tempstrarray, tempstrarray.Length - 1);
            Array.Reverse(tempstrarray);
            string[] cmdparams = (string[])tempstrarray;
            RunCmd(cmd, cmdparams);
        }
    }
}
