using System;

namespace OpenSim.Framework.Console
{
    public abstract class ConsoleBase
    {

        public enum ConsoleType
        {
            Local,		// Use stdio
            TCP,		// Use TCP/telnet
            SimChat		// Use in-world chat (for gods)
        }

        public abstract void Close();

        public abstract void Write(string format, params object[] args);

        public abstract void WriteLine(string format, params object[] args);

        public abstract string ReadLine();

        public abstract int Read();

        // Displays a command prompt and waits for the user to enter a string, then returns that string
        public abstract string CmdPrompt(string prompt);

        // Displays a command prompt and returns a default value if the user simply presses enter
        public abstract string CmdPrompt(string prompt, string defaultresponse);

        // Displays a command prompt and returns a default value, user may only enter 1 of 2 options
        public abstract string CmdPrompt(string prompt, string defaultresponse, string OptionA, string OptionB);

        // Runs a command with a number of parameters
        public abstract Object RunCmd(string Cmd, string[] cmdparams);

        // Shows data about something
        public abstract void ShowCommands(string ShowWhat);

        // Displays a prompt to the user and then runs the command they entered
        public abstract void MainConsolePrompt();

        public abstract void SetStatus(string status);
    }
}
