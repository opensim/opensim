/*
* Copyright (c) Contributors, http://www.openmetaverse.org/
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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
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
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;
using OpenSim.Framework;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;
using libsecondlife;

namespace OpenSim.RegionServer.Simulator
{
    public partial class World
    {
        private Dictionary<string, IScriptEngine> scriptEngines = new Dictionary<string, IScriptEngine>();

        private void LoadScriptEngines()
        {
            this.LoadScriptPlugins();
        }

        public void LoadScriptPlugins()
        {
            string path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "ScriptEngines");
            string[] pluginFiles = Directory.GetFiles(path, "*.dll");


            for (int i = 0; i < pluginFiles.Length; i++)
            {
                this.AddPlugin(pluginFiles[i]);
            }
        }

        private void AddPlugin(string FileName)
        {
            Assembly pluginAssembly = Assembly.LoadFrom(FileName);

            foreach (Type pluginType in pluginAssembly.GetTypes())
            {
                if (pluginType.IsPublic)
                {
                    if (!pluginType.IsAbstract)
                    {
                        Type typeInterface = pluginType.GetInterface("IScriptEngine", true);

                        if (typeInterface != null)
                        {
                            IScriptEngine plug = (IScriptEngine)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
                            plug.Init(this);
                            this.scriptEngines.Add(plug.GetName(), plug);

                        }

                        typeInterface = null;
                    }
                }
            }

            pluginAssembly = null;
        }

        public void LoadScript(string scriptType, string scriptName, string script, Entity ent)
        {
            if(this.scriptEngines.ContainsKey(scriptType))
            {
                this.scriptEngines[scriptType].LoadScript(script, scriptName, ent.localid);
            }
        }

        #region IScriptAPI Methods

        public OSVector3 GetEntityPosition(uint localID)
        {
            OSVector3 res = new OSVector3();
           // Console.WriteLine("script-  getting entity " + localID + " position");
            foreach (Entity entity in this.Entities.Values)
            {
                if (entity.localid == localID)
                {
                    res.X = entity.Pos.X;
                    res.Y = entity.Pos.Y;
                    res.Z = entity.Pos.Z;
                }
            }
            return res;
        }

        public void SetEntityPosition(uint localID, float x , float y, float z)
        {
            foreach (Entity entity in this.Entities.Values)
            {
                if (entity.localid == localID && entity is Primitive)
                {
                    LLVector3 pos = entity.Pos;
                    pos.X = x;
                    pos.Y = y;
                   Primitive prim = entity as Primitive;
                    // Of course, we really should have asked the physEngine if this is possible, and if not, returned false.
                   prim.UpdatePosition(pos);
                   // Console.WriteLine("script- setting entity " + localID + " positon");
                }
            }
           
        }

        public uint GetRandomAvatarID()
        {
            //Console.WriteLine("script- getting random avatar id");
            uint res = 0;
            foreach (Entity entity in this.Entities.Values)
            {
                if (entity is Avatar)
                {
                    res = entity.localid;
                }
            }
            return res;
        }

        #endregion


    }
}
