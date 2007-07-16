using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Framework.Console;
using OpenSim.Framework;
using OpenSim.Region;
using OpenSim.Region.Scenes;

namespace OpenSim.Scripting
{
    public interface IScript
    {
        void Initialise(ScriptInfo scriptInfo);
    }

    public class TestScript : IScript
    {
        ScriptInfo script;

        public void Initialise(ScriptInfo scriptInfo)
        {
            script = scriptInfo;
            script.events.OnFrame += new OpenSim.Region.Scenes.EventManager.OnFrameDelegate(events_OnFrame);
        }

        void events_OnFrame()
        {
            script.logger.Verbose("Hello World!");
        }
    }
}
