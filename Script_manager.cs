/*
 * Copyright (c) 2007 Michael Wright

* Copyright (c) <year>, <copyright holder>
* All rights reserved.
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

  */

using System;
using System.Collections;
//using LuaInterface;
using libsecondlife;

namespace OpenSim
{
	/// <summary>
	/// Description of Script_manager.
	/// </summary>
	public class ScriptManager
	{
		//public LuaInterface.Lua Lu;
		//private ArrayList scripts;
		//private prim_info current_prim;
		
		public ScriptManager()
		{
		}
		
		/*public void start_up (Lua lua, App ap)
		{
			this.Lu=lua;
			//register any lua routines , like check finish script one
			Lu.OpenMathLib();
		}*/
		
		private void RegisterFunctions()
		{
			//lu.RegisterFunction( "RegisterScript",this,this.GetType().GetMethod("ScriptRegister"));
			//lu.RegisterFunction( "MoveObject",this,this.GetType().GetMethod("MoveObject"));
			//lu.RegisterFunction( "Say",this,this.GetType().GetMethod("Say"));
		
		}
		
		public void Call_tick(PrimInfo prim)
		{
			//set current prim and then call tick function in linked script
		}
		public void Call_touch(PrimInfo prim)
		{
			//set current prim and then call clicked function in linked script
	
		}
		public void Call_on_rex(PrimInfo prim)
		{
			//set current prim and then call clicked function in linked script
	
		}
		
		#region Lua Functions
		
		public void ScriptRegister( script_object_interface script)
		{
			//called by scripts to register themselves
		}
		public void MoveObject(float x ,float y, float z)
		{
		
		}
		public void Say(string message)
		{
			
		}
		#endregion
		
	}
	
	public interface script_object_interface
	{
		void frame_tick();
		void touch(int num);
		void on_rex(int num);
	}
}
