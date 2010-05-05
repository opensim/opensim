using System;
using System.Threading;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Console
{
    /// <summary>
    /// This is a Fake console that's used when setting up the Scene in Unit Tests
    /// Don't use this except for Unit Testing or you're in for a world of hurt when the 
    /// sim gets to ReadLine
    /// </summary>
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
