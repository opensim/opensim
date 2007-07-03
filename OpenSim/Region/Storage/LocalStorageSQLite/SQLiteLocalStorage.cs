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

// SQLite Support
// A bad idea, but the IRC people told me to!

using System;
using System.Data;
using System.Data.SQLite;
using libsecondlife;
using OpenSim.Framework.Console;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Storage.LocalStorageSQLite
{
    public class SQLiteLocalStorage : ILocalStorage
    {
        IDbConnection db;

        public SQLiteLocalStorage()
        {
            try
            {
                string connectionstring = "URI=file:localsim.sdb";
                db = (IDbConnection)new SQLiteConnection(connectionstring);
                db.Open();
            }
            catch (Exception e)
            {
                db.Close();
                MainLog.Instance.Warn("SQLiteLocalStorage :Constructor - Exception occured");
                MainLog.Instance.Warn(e.ToString());
            }
        }

        public void Initialise(string file)
        {
            // Blank
        }

        public void StorePrim(PrimData prim)
        {
            IDbCommand cmd = db.CreateCommand();

            //SECURITY WARNING:
            // These parameters wont produce SQL injections since they are all integer based, however.
            // if inserting strings such as name or description, you will need to use appropriate
            // measures to prevent SQL injection (although the value of SQL injection in this is limited).

            string sql = "REPLACE INTO prim (OwnerID,PCode,PathBegin,PathEnd,PathScaleX,PathScaleY,PathShearX,PathShearY,PathSkew,ProfileBegin,ProfileEnd,Scale,PathCurve,ProfileCurve,ParentID,ProfileHollow,PathRadiusOffset,PathRevolutions,PathTaperX,PathTaperY,PathTwist,PathTwistBegin,Texture,CreationDate,OwnerMask,NextOwnerMask,GroupMask,EveryoneMask,BaseMask,Position,Rotation,LocalID,FullID) ";
            sql += "VALUES (";
            sql += "\"" + prim.OwnerID.ToStringHyphenated() + "\","; // KILL ME NOW!
            sql += "\"" + prim.PCode.ToString() + "\",";
            sql += "\"" + prim.PathBegin.ToString() + "\",";
            sql += "\"" + prim.PathEnd.ToString() + "\",";
            sql += "\"" + prim.PathScaleX.ToString() + "\",";
            sql += "\"" + prim.PathScaleY.ToString() + "\",";
            sql += "\"" + prim.PathShearX.ToString() + "\",";
            sql += "\"" + prim.PathShearY.ToString() + "\",";
            sql += "\"" + prim.PathSkew.ToString() + "\",";
            sql += "\"" + prim.ProfileBegin.ToString() + "\",";
            sql += "\"" + prim.ProfileEnd.ToString() + "\",";
            sql += "\"" + prim.Scale.ToString() + "\",";
            sql += "\"" + prim.PathCurve.ToString() + "\",";
            sql += "\"" + prim.ProfileCurve.ToString() + "\",";
            sql += "\"" + prim.ParentID.ToString() + "\",";
            sql += "\"" + prim.ProfileHollow.ToString() + "\",";
            sql += "\"" + prim.PathRadiusOffset.ToString() + "\",";
            sql += "\"" + prim.PathRevolutions.ToString() + "\",";
            sql += "\"" + prim.PathTaperX.ToString() + "\",";
            sql += "\"" + prim.PathTaperY.ToString() + "\",";
            sql += "\"" + prim.PathTwist.ToString() + "\",";
            sql += "\"" + prim.PathTwistBegin.ToString() + "\",";
            sql += "\"" + prim.TextureEntry.ToString() + "\",";
            sql += "\"" + prim.CreationDate.ToString() + "\",";
            sql += "\"" + prim.OwnerMask.ToString() + "\",";
            sql += "\"" + prim.NextOwnerMask.ToString() + "\",";
            sql += "\"" + prim.GroupMask.ToString() + "\",";
            sql += "\"" + prim.EveryoneMask.ToString() + "\",";
            sql += "\"" + prim.BaseMask.ToString() + "\",";
            sql += "\"" + prim.Position.ToString() + "\",";
            sql += "\"" + prim.Rotation.ToString() + "\",";
            sql += "\"" + prim.LocalID.ToString() + "\",";
            sql += "\"" + prim.FullID.ToString() + "\")";

            cmd.CommandText = sql;

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("SQLiteLocalStorage :StorePrim - Exception occured");
                MainLog.Instance.Warn(e.ToString());
            }

            cmd.Dispose();
            cmd = null;
        }

        public void RemovePrim(LLUUID primID)
        {
            IDbCommand cmd = db.CreateCommand();

            //SECURITY WARNING:
            // These parameters wont produce SQL injections since they are all integer based, however.
            // if inserting strings such as name or description, you will need to use appropriate
            // measures to prevent SQL injection (although the value of SQL injection in this is limited).

            string sql = "DELETE FROM prim WHERE FullID = \"" + primID.ToStringHyphenated() + "\"";

            cmd.CommandText = sql;

            try
            {
                cmd.ExecuteNonQuery();
            }
            catch (Exception e)
            {
                MainLog.Instance.Warn("SQLiteLocalStorage :RemovePrim - Exception occured");
                MainLog.Instance.Warn(e.ToString());
            }

            cmd.Dispose();
            cmd = null;
        }

        public void LoadPrimitives(ILocalStorageReceiver receiver)
        {

        }

        public float[] LoadWorld()
        {
            return new float[65536];
        }

        public void SaveMap(float[] heightmap)
        {

        }

        public void SaveParcels(ParcelData[] parcel_manager)
        {

        }

        public void SaveParcel(ParcelData parcel)
        {
        }

        public void RemoveParcel(ParcelData parcel)
        {
        }

        public void RemoveAllParcels()
        {
        }

        public void LoadParcels(ILocalStorageParcelReceiver recv)
        {
            recv.NoParcelDataFromStorage();
        }

        public void ShutDown()
        {
            db.Close();
            db = null;
        }
    }
}