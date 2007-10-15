/*
* Copyright (c) Contributors, http://opensimulator.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/

using OpenSim.Framework.Console;
using OpenSim.Framework;
using OpenSim.Region.Environment;
using OpenSim.Region.Environment.Scenes;

using System.Collections.Generic;
using libsecondlife;

namespace OpenSim.Region.ExtensionsScriptModule.CSharp.Examples
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
            
            script.events.OnPluginConsole += new EventManager.OnPluginConsoleDelegate(ProcessConsoleMsg);
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
