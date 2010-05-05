using System;
using System.Threading;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Console
{
    public class MockConsole : CommandConsole
    {
        public MockConsole(string defaultPrompt) : base(defaultPrompt)
        {
        }
        public override void Output(string text)
        {
        }
        public override void Output(string text, string level)
        {
        }

        public override string ReadLine(string p, bool isCommand, bool e)
        {
            //Thread.CurrentThread.Join(1000);
            return string.Empty;
        }
        public override void UnlockOutput()
        {
        }
        public override void LockOutput()
        {
        }
    }
}
