using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using log4net;
using OpenSim.ScriptEngine.Shared;

namespace OpenSim.ScriptEngine.Components.DotNetEngine.Commands_LSL
{
    public class Script : IScriptCommandProvider
    {
        internal static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public void llSay(int channelID, string text)
        {
            m_log.InfoFormat("[{0}] llSay({1}, \"{2}\")", "(Commands_LSL)OpenSim.ScriptEngine.Components.DotNetEngine.Commands_LSL.Script", channelID, text);
        }

        public void ExecuteCommand(string functionName, params object[] args)
        {
            
        }

        public string Name
        {
            get { return "SECS.DotNetEngine.Commands_LSL.Script"; }
        }
    }
}