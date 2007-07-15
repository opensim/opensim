using OpenSim.Framework.Console;
using OpenSim.Framework;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Scripting.Examples
{
    public class LSLExportScript : IScript
    {
        ScriptInfo script;

        public string getName()
        {
            return "LSL Export Script 0.1";
        }

        public void Initialise(ScriptInfo scriptInfo)
        {
            script = scriptInfo;
            
            script.events.OnScriptConsole += new EventManager.OnScriptConsoleDelegate(events_OnScriptConsole);
        }

        void events_OnScriptConsole(string[] args)
        {
            if (args[0].ToLower() == "lslexport")
            {
                
            }
        }
    }
}