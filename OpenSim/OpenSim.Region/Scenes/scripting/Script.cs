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
        string getName();
    }

    public class TestScript : IScript
    {
        ScriptInfo script;

        public string getName()
        {
            return "TestScript 0.1";
        }

        public void Initialise(ScriptInfo scriptInfo)
        {
            script = scriptInfo;
            script.events.OnFrame += new OpenSim.Region.Scenes.EventManager.OnFrameDelegate(events_OnFrame);
            script.events.OnNewPresence += new EventManager.OnNewPresenceDelegate(events_OnNewPresence);
        }

        void events_OnNewPresence(ScenePresence presence)
        {
            script.logger.Verbose("Hello " + presence.firstname.ToString() + "!");
        }

        void events_OnFrame()
        {
            //script.logger.Verbose("Hello World!");
        }
    }
}
