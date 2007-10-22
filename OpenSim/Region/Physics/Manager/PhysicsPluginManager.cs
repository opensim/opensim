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
using System.IO;
using System.Reflection;
using OpenSim.Framework.Console;

namespace OpenSim.Region.Physics.Manager
{
	/// <summary>
	/// Description of MyClass.
	/// </summary>
	public class PhysicsPluginManager
	{
		private Dictionary<string, IPhysicsPlugin> _plugins=new Dictionary<string, IPhysicsPlugin>();
		
		public PhysicsPluginManager()
		{
			
		}
		
		public PhysicsScene GetPhysicsScene(string engineName)
		{
            if (String.IsNullOrEmpty(engineName))
            {
                return PhysicsScene.Null;
            }

			if(_plugins.ContainsKey(engineName))
			{
				MainLog.Instance.Verbose("PHYSICS","creating "+engineName);
				return _plugins[engineName].GetScene();
			}
			else
            {
                MainLog.Instance.Warn("PHYSICS", "couldn't find physicsEngine: {0}", engineName);
                throw new ArgumentException(String.Format("couldn't find physicsEngine: {0}",engineName));
			}
		}
		
		public void LoadPlugins()
		{
			string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory ,"Physics");
       		string[] pluginFiles = Directory.GetFiles(path, "*.dll");
        

        	for(int i= 0; i<pluginFiles.Length; i++)
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
						Type typeInterface = pluginType.GetInterface("IPhysicsPlugin", true);
						
						if (typeInterface != null)
						{
							IPhysicsPlugin plug = (IPhysicsPlugin)Activator.CreateInstance(pluginAssembly.GetType(pluginType.ToString()));
							plug.Init();
							this._plugins.Add(plug.GetName(),plug);
                            OpenSim.Framework.Console.MainLog.Instance.Verbose("PHYSICS","Added physics engine: " + plug.GetName());
							
						}	
						
						typeInterface = null; 			
					}				
				}			
			}
			
			pluginAssembly = null; 
		}
        //---
        public static void PhysicsPluginMessage(string message, bool isWarning)
        {
            if (isWarning)
            {
                MainLog.Instance.Warn("PHYSICS", message);
            }
            else
            {
                MainLog.Instance.Verbose("PHYSICS", message);
            }
        }
        //---
	}

	public interface IPhysicsPlugin
	{
		bool Init();
		PhysicsScene GetScene();
		string GetName();
		void Dispose();
	}
}
