using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Scenes;
using OpenSim.Framework.Console;

namespace OpenSim.Scripting
{
    /// <summary>
    /// Class which provides access to the world
    /// </summary>
    public class ScriptInfo
    {
        // Reference to world.eventsManager provided for convenience
        public EventManager events;

        // The main world
        public Scene world;

        // The console
        public LogBase logger;

        public ScriptInfo(Scene scene)
        {
            world = scene;
            events = world.eventManager;
            logger = OpenSim.Framework.Console.MainLog.Instance;
        }
    }
}
