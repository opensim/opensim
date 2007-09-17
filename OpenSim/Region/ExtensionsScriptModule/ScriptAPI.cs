using System;
using System.Collections.Generic;
using System.Text;
using Key = libsecondlife.LLUUID;
using Rotation = libsecondlife.LLQuaternion;
using Vector = libsecondlife.LLVector3;
using LSLList = System.Collections.Generic.List<string>;


using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.ExtensionsScriptModule
{
    // This class is to be used for engines which may not be able to access the Scene directly.
    // Scene access is preffered, but obviously not possible on some non-.NET languages.
    public class ScriptAPI
    {
        Scene scene;
        ScriptInterpretedAPI interpretedAPI;

        public ScriptAPI(Scene world, Key taskID)
        {
            scene = world;
            interpretedAPI = new ScriptInterpretedAPI(world, taskID);
        }

        public Object CallMethod(String method, Object[] args)
        {
            return null;
        }
    }
}
