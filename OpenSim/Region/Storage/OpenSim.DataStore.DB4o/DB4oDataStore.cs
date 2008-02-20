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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
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

using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.LandManagement;
using OpenSim.Region.Environment;
using OpenSim.Region.Interfaces;
using OpenSim.Framework.Console;
using libsecondlife;

using Db4objects.Db4o;
using Db4objects.Db4o.Query;

namespace OpenSim.DataStore.DB4oStorage
{
    public class SceneObjectQuery : Predicate
    {
        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private LLUUID globalIDSearch;

        public SceneObjectQuery(LLUUID find)
        {
            globalIDSearch = find;
        }

        public bool Match(SceneObjectGroup obj)
        {
            return obj.UUID == globalIDSearch;
        }
    }

    public class DB4oDataStore : IRegionDataStore
    {
        private IObjectContainer db;

        public void Initialise(string dbfile, string dbname)
        {
            m_log.Info("[DATASTORE]: DB4O - Opening " + dbfile);
            db = Db4oFactory.OpenFile(dbfile);
        }

        public void StoreObject(SceneObjectGroup obj, LLUUID regionUUID)
        {
            db.Set(obj);
        }

        public void RemoveObject(LLUUID obj, LLUUID regionUUID)
        {
            IObjectSet result = db.Query(new SceneObjectQuery(obj));
            if (result.Count > 0)
            {
                SceneObjectGroup item = (SceneObjectGroup)result.Next();
                db.Delete(item);
            }
        }

        public List<SceneObjectGroup> LoadObjects(LLUUID regionUUID)
        {
            IObjectSet result = db.Get(typeof(SceneObjectGroup));
            List<SceneObjectGroup> retvals = new List<SceneObjectGroup>();

            m_log.Info("[DATASTORE]: DB4O - LoadObjects found " + result.Count.ToString() + " objects");

            foreach (Object obj in result)
            {
                retvals.Add((SceneObjectGroup)obj);
            }

            return retvals;
        }

        public void StoreTerrain(double[,] ter)
        {
        }

        public double[,] LoadTerrain()
        {
            return null;
        }

        public void RemoveLandObject(uint id)
        {
        }

        public void StoreParcel(Land parcel)
        {
        }

        public List<Land> LoadLandObjects()
        {
            return new List<Land>();
        }

        public void Shutdown()
        {
            if (db != null)
            {
                db.Commit();
                db.Close();
            }
        }
    }
}
