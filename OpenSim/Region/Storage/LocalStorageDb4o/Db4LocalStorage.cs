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
using Db4objects.Db4o;
using libsecondlife;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Storage.LocalStorageDb4o
{
    /// <summary>
    /// 
    /// </summary>
    public class Db4LocalStorage : ILocalStorage
    {
        private IObjectContainer db;
        private string datastore;

        public Db4LocalStorage()
        {

        }

        public void Initialise(string dfile)
        {
            MainLog.Instance.Warn("Db4LocalStorage Opening " + dfile);
            datastore = dfile;
            try
            {
                db = Db4oFactory.OpenFile(datastore);
                MainLog.Instance.Verbose("Db4LocalStorage creation");
            }
            catch (Exception e)
            {
                db.Close();
                MainLog.Instance.Warn("Db4LocalStorage :Constructor - Exception occured");
                MainLog.Instance.Warn(e.ToString());
            }
        }

        public void StorePrim(PrimData prim)
        {
            IObjectSet result = db.Query(new UUIDPrimQuery(prim.FullID));
            if (result.Count > 0)
            {
                //prim already in storage
                //so update it
                PrimData found = (PrimData)result.Next();
                found.PathBegin = prim.PathBegin;
                found.PathCurve = prim.PathCurve;
                found.PathEnd = prim.PathEnd;
                found.PathRadiusOffset = prim.PathRadiusOffset;
                found.PathRevolutions = prim.PathRevolutions;
                found.PathScaleX = prim.PathScaleX;
                found.PathScaleY = prim.PathScaleY;
                found.PathShearX = prim.PathShearX;
                found.PathShearY = prim.PathShearY;
                found.PathSkew = prim.PathSkew;
                found.PathTaperX = prim.PathTaperX;
                found.PathTaperY = prim.PathTaperY;
                found.PathTwist = prim.PathTwist;
                found.PathTwistBegin = prim.PathTwistBegin;
                found.PCode = prim.PCode;
                found.ProfileBegin = prim.ProfileBegin;
                found.ProfileCurve = prim.ProfileCurve;
                found.ProfileEnd = prim.ProfileEnd;
                found.ProfileHollow = prim.ProfileHollow;
                found.Position = prim.Position;
                found.Rotation = prim.Rotation;
                found.TextureEntry = prim.TextureEntry;
                db.Set(found);
                db.Commit();
            }
            else
            {
                //not in storage
                db.Set(prim);
                db.Commit();
            }
        }

        public void RemovePrim(LLUUID primID)
        {
            IObjectSet result = db.Query(new UUIDPrimQuery(primID));
            if (result.Count > 0)
            {
                PrimData found = (PrimData)result.Next();
                db.Delete(found);
            }
        }


        public void LoadPrimitives(ILocalStorageReceiver receiver)
        {
            IObjectSet result = db.Get(typeof(PrimData));
            MainLog.Instance.Verbose("Db4LocalStorage.cs: LoadPrimitives() - number of prims in storages is " + result.Count);
            foreach (PrimData prim in result)
            {
                receiver.PrimFromStorage(prim);
            }
        }

        public float[] LoadWorld()
        {
            MainLog.Instance.Verbose("LoadWorld() - Loading world....");
            float[] heightmap = null;
            MainLog.Instance.Verbose("LoadWorld() - Looking for a heightmap in local DB");
            IObjectSet world_result = db.Get(typeof(MapStorage));
            if (world_result.Count > 0)
            {
                MainLog.Instance.Verbose("LoadWorld() - Found a heightmap in local database, loading");
                MapStorage map = (MapStorage)world_result.Next();
                //blank.LandMap = map.Map;
                heightmap = map.Map;
            }
            return heightmap;
        }

        public void SaveMap(float[] heightmap)
        {
            IObjectSet world_result = db.Get(typeof(MapStorage));
            if (world_result.Count > 0)
            {
                MainLog.Instance.Verbose("SaveWorld() - updating saved copy of heightmap in local database");
                MapStorage map = (MapStorage)world_result.Next();
                db.Delete(map);
            }
            MapStorage map1 = new MapStorage();
            map1.Map = heightmap; //OpenSim_Main.local_world.LandMap;
            db.Set(map1);
            db.Commit();
        }

        public void SaveLandObject(LandData landObject)
        {
            IObjectSet result = db.Query(new UUIDLandQuery(landObject.globalID));
            if (result.Count > 0)
            {
                //Old Land
                LandData updateLand = (LandData)result.Next();
                updateLand.AABBMax = landObject.AABBMax;
                updateLand.AABBMin = landObject.AABBMin;
                updateLand.area = landObject.area;
                updateLand.auctionID = landObject.auctionID;
                updateLand.authBuyerID = landObject.authBuyerID;
                updateLand.category = landObject.category;
                updateLand.claimDate = landObject.claimDate;
                updateLand.claimPrice = landObject.claimPrice;
                updateLand.groupID = landObject.groupID;
                updateLand.groupPrims = landObject.groupPrims;
                updateLand.isGroupOwned = landObject.isGroupOwned;
                updateLand.landingType = landObject.landingType;
                updateLand.mediaAutoScale = landObject.mediaAutoScale;
                updateLand.mediaID = landObject.mediaID;
                updateLand.mediaURL = landObject.mediaURL;
                updateLand.musicURL = landObject.musicURL;
                updateLand.localID = landObject.localID;
                updateLand.ownerID = landObject.ownerID;
                updateLand.passHours = landObject.passHours;
                updateLand.passPrice = landObject.passPrice;
                updateLand.landBitmapByteArray = (byte[])landObject.landBitmapByteArray.Clone();
                updateLand.landDesc = landObject.landDesc;
                updateLand.landFlags = landObject.landFlags;
                updateLand.landName = landObject.landName;
                updateLand.landStatus = landObject.landStatus;
                updateLand.salePrice = landObject.salePrice;
                updateLand.snapshotID = landObject.snapshotID;
                updateLand.userLocation = landObject.userLocation;
                updateLand.userLookAt = landObject.userLookAt;

                db.Set(updateLand);
            }
            else
            {
                db.Set(landObject);
            }
            db.Commit();
        }

        public void SaveLandObjects(LandData[] landObject_data)
        {
            MainLog.Instance.Notice("Land Backup: Saving Lands...");
            int i;
            for (i = 0; i < landObject_data.GetLength(0); i++)
            {

                SaveLandObject(landObject_data[i]);

            }
            MainLog.Instance.Notice("Land Backup: Land Save Complete");
        }

        public void RemoveLandObject(LandData landObject)
        {
            IObjectSet result = db.Query(new UUIDLandQuery(landObject.globalID));
            if (result.Count > 0)
            {
                db.Delete(result[0]);
            }
            db.Commit();
        }
        public void RemoveAllLandObjects()
        {
            MainLog.Instance.Notice("Land Backup: Removing all land objects...");
            IObjectSet result = db.Get(typeof(LandData));
            if (result.Count > 0)
            {
                foreach (LandData landData in result)
                {
                    RemoveLandObject(landData);
                }
            }
        }

        public void LoadLandObjects(ILocalStorageLandObjectReceiver recv)
        {
            MainLog.Instance.Notice("Land Backup: Loading Lands...");
            IObjectSet result = db.Get(typeof(LandData));
            if (result.Count > 0)
            {
                MainLog.Instance.Notice("Land Backup: Lands exist in database.");
                foreach (LandData landData in result)
                {

                    recv.LandFromStorage(landData);
                }
            }
            else
            {
                MainLog.Instance.Notice("Land Backup: No landObjects exist. Creating basic landObject.");
                recv.NoLandDataFromStorage();
            }
            MainLog.Instance.Notice("Land Backup: Lands Restored");
        }
        public void ShutDown()
        {
            db.Commit();
            db.Close();
        }
    }
}