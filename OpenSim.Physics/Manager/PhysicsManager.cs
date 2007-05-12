/*
* Copyright (c) OpenSim project, http://sim.opensecondlife.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY <copyright holder> ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
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
using System.Collections;
using System.IO;
using System.Reflection;
using Axiom.MathLib;
using OpenSim.Framework.Console;

namespace OpenSim.Physics.Manager
{
	/// <summary>
	/// Description of MyClass.
	/// </summary>
	public class PhysicsManager
	{
		private Dictionary<string, IPhysicsPlugin> _plugins=new Dictionary<string, IPhysicsPlugin>();
		
		public PhysicsManager()
		{
			
		}
		
		public PhysicsScene GetPhysicsScene(string engineName)
		{
            if (String.IsNullOrEmpty(engineName))
            {
                return new NullPhysicsScene();
            }

			if(_plugins.ContainsKey(engineName))
			{
				OpenSim.Framework.Console.MainConsole.Instance.WriteLine(LogPriority.LOW,"creating "+engineName);
				return _plugins[engineName].GetScene();
			}
			else
            {
                OpenSim.Framework.Console.MainConsole.Instance.WriteLine(LogPriority.MEDIUM,"couldn't find physicsEngine: {0}",engineName);
                throw new ArgumentException(String.Format("couldn't find physicsEngine: {0}",engineName));
			}
		}
		
		public void LoadPlugins()
		{
			string path = Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory ,"Physics");
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
							
						}	
						
						typeInterface = null; 			
					}				
				}			
			}
			
			pluginAssembly = null; 
		}
	}

	public interface IPhysicsPlugin
	{
		bool Init();
		PhysicsScene GetScene();
		string GetName();
		void Dispose();
	}
}
