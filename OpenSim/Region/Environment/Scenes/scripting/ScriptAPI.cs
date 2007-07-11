using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Scripting
{
    // This class is to be used for engines which may not be able to access the Scene directly.
    // Scene access is preffered, but obviously not possible on some non-.NET languages.
    public class ScriptAPI
    {
        Scene scene;

        public ScriptAPI(Scene world)
        {
            scene = world;
        }

        public Object CallMethod(String method, Object[] args)
        {
            return null;
        }
    }
}
