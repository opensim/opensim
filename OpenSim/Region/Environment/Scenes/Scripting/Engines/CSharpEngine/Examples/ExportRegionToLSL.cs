using OpenSim.Framework.Console;
using OpenSim.Framework;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;

using System.Collections.Generic;
using libsecondlife;

namespace OpenSim.Region.Scripting.Examples
{
    public class LSLExportScript : IScript
    {
        ScriptInfo script;

        public string Name
        {
            get { return "LSL Export Script 0.1"; }
        }

        public void Initialise(ScriptInfo scriptInfo)
        {
            script = scriptInfo;
            
            script.events.OnScriptConsole += new EventManager.OnScriptConsoleDelegate(ProcessConsoleMsg);
        }

        void ProcessConsoleMsg(string[] args)
        {
            /*if (args[0].ToLower() == "lslexport")
            {
                string sequence = "";

                foreach (KeyValuePair<LLUUID, SceneObject> obj in script.world.Objects)
                {
                    SceneObject root = obj.Value;

                    sequence += "NEWOBJ::" + obj.Key.ToStringHyphenated() + "\n";

                    string rootPrim = processPrimitiveToString(root.rootPrimitive);

                    sequence += "ROOT:" + rootPrim;

                    foreach (KeyValuePair<LLUUID, OpenSim.Region.Environment.Scenes.Primitive> prim in root.Children)
                    {
                        string child = processPrimitiveToString(prim.Value);
                        sequence += "CHILD:" + child;
                    }
                }

                System.Console.WriteLine(sequence);
            }*/
        }

        string processPrimitiveToString(OpenSim.Region.Environment.Scenes.SceneObjectPart prim)
        {
            /*string desc = prim.Description;
            string name = prim.Name;
            LLVector3 pos = prim.Pos;
            LLQuaternion rot = new LLQuaternion(prim.Rotation.x, prim.Rotation.y, prim.Rotation.z, prim.Rotation.w);
            LLVector3 scale = prim.Scale;
            LLVector3 rootPos = prim.WorldPos;

            string setPrimParams = "";

            setPrimParams += "[PRIM_SCALE, " + scale.ToString() + ", PRIM_POS, " + rootPos.ToString() + ", PRIM_ROTATION, " + rot.ToString() + "]\n";

            return setPrimParams;
              */
            return "";
        }
    }
}