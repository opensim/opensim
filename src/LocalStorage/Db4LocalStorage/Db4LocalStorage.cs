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
using Db4objects.Db4o;
using Db4objects.Db4o.Query;
using libsecondlife;
using GridInterfaces;

namespace Db4LocalStorage
{
	/// <summary>
	/// 
	/// </summary>
	public class Db4LocalStorage : ILocalStorage
	{
		private IObjectContainer db;
		
		public Db4LocalStorage()
		{
			try 
			{
				db = Db4oFactory.OpenFile("localworld.yap");
				ServerConsole.MainConsole.Instance.WriteLine("Db4LocalStorage creation");
			}
			catch(Exception e) 
			{
				db.Close();
				ServerConsole.MainConsole.Instance.WriteLine("Db4LocalStorage :Constructor - Exception occured");
				ServerConsole.MainConsole.Instance.WriteLine(e.ToString());
			}
		}
		
		public void StorePrim(PrimStorage prim)
		{
			IObjectSet result = db.Query(new UUIDQuery(prim.FullID));
			if(result.Count>0)
			{
				//prim already in storage
				//so update it
				PrimStorage found = (PrimStorage) result.Next();
				found.Data = prim.Data;
				found.Position = prim.Position;
				found.Rotation = prim.Rotation;
				db.Set(found);
			}
			else
			{
				//not in storage
				db.Set(prim);
			}
		}
		
		public void RemovePrim(LLUUID primID)
		{
			IObjectSet result = db.Query(new UUIDQuery(primID));
			if(result.Count>0)
			{
				PrimStorage found = (PrimStorage) result.Next();
				db.Delete(found);
			}
		}
		
		
		public void LoadPrimitives(ILocalStorageReceiver receiver)
		{
			IObjectSet result = db.Get(typeof(PrimStorage));
			ServerConsole.MainConsole.Instance.WriteLine("Db4LocalStorage.cs: LoadPrimitives() - number of prims in storages is "+result.Count);
			foreach (PrimStorage prim in result) {
				receiver.PrimFromStorage(prim);
			}
		}
		
		public void ShutDown()
		{
			db.Commit();
			db.Close();
		}
	}
	
	public class UUIDQuery : Predicate
	{
		private LLUUID _findID;
		
		public UUIDQuery(LLUUID find)
		{
			_findID = find;
		}
		public bool Match(PrimStorage prim)
		{
			return (prim.FullID == _findID);
		}
	}
	
}
