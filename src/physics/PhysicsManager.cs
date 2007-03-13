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

namespace PhysicsSystem
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
		    if( String.IsNullOrEmpty( engineName ) )
		    {
                return new NullPhysicsScene();
		    }

		    if(_plugins.ContainsKey(engineName))
			{
				ServerConsole.MainConsole.Instance.WriteLine("creating "+engineName);
				return _plugins[engineName].GetScene();
			}
			else
			{
                string error = String.Format("couldn't find physicsEngine: {0}", engineName);
				ServerConsole.MainConsole.Instance.WriteLine( error );
                throw new ArgumentException( error );
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
	
	public abstract class PhysicsScene
	{
        public static PhysicsScene Null
        {
            get
            {                
                return new NullPhysicsScene();
            }
        }
	
		public abstract PhysicsActor AddAvatar(PhysicsVector position);
		
		public abstract PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size);
		
		public abstract void Simulate(float timeStep);
		
		public abstract void GetResults();
		
		public abstract void SetTerrain(float[] heightMap);
		
		public abstract bool IsThreaded
		{
			get;
		}
	}
	
    public class NullPhysicsScene : PhysicsScene
    {
        private static int m_workIndicator;
        
        public override PhysicsActor AddAvatar(PhysicsVector position)
        {
            ServerConsole.MainConsole.Instance.WriteLine("NullPhysicsScene : AddAvatar({0})", position );
            return PhysicsActor.Null;
        }

        public override PhysicsActor AddPrim(PhysicsVector position, PhysicsVector size)
        {
            ServerConsole.MainConsole.Instance.WriteLine("NullPhysicsScene : AddPrim({0},{1})", position, size );
            return PhysicsActor.Null;            
        }

        public override void Simulate(float timeStep)
        {
            m_workIndicator = ( m_workIndicator + 1 ) % 10;

            ServerConsole.MainConsole.Instance.SetStatus( m_workIndicator.ToString() );
        }

        public override void GetResults()
        {
            ServerConsole.MainConsole.Instance.WriteLine("NullPhysicsScene : GetResults()" );
        }

        public override void SetTerrain(float[] heightMap)
        {
            ServerConsole.MainConsole.Instance.WriteLine("NullPhysicsScene : SetTerrain({0} items)", heightMap.Length );
        }

        public override bool IsThreaded
        {
            get { return false; }
        }
    }
    
	public abstract class PhysicsActor
	{
        public static readonly PhysicsActor Null = new NullPhysicsActor();
	    
		public abstract PhysicsVector Position
		{
			get;
			set;
		}
		
		public abstract PhysicsVector Velocity
		{
			get;
			set;
		}
		
		public abstract PhysicsVector Acceleration
		{
			get;
		}
		public abstract bool Flying
		{
			get;
			set;
		}
		
		public abstract void AddForce(PhysicsVector force);
		
		public abstract void SetMomentum(PhysicsVector momentum);
	}

    public class NullPhysicsActor : PhysicsActor
    {
        public override PhysicsVector Position
        {
            get
            {
                return PhysicsVector.Zero;
            }
            set
            {
                return;
            }
        }

        public override PhysicsVector Velocity
        {
            get
            {
                return PhysicsVector.Zero;
            }
            set
            {
                return;
            }
        }

        public override PhysicsVector Acceleration
        {
            get { return PhysicsVector.Zero; }
        }

        public override bool Flying
        {
            get
            {
                return false;
            }
            set
            {
                return;
            }
        }

        public override void AddForce(PhysicsVector force)
        {
            return;
        }

        public override void SetMomentum(PhysicsVector momentum)
        {
            return;
        }
    }

    public class PhysicsVector
	{
		public float X;
		public float Y;
		public float Z;
		
		public PhysicsVector()
		{
			
		}
		
		public PhysicsVector(float x, float y, float z)
		{
			X = x;
			Y = y;
			Z = z;
		}

        public static readonly PhysicsVector Zero = new PhysicsVector(0f, 0f, 0f);
	}
}
