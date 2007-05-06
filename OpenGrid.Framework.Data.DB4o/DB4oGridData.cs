using System;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Data;
using libsecondlife;


namespace OpenGrid.Framework.Data.DB4o
{
    class DB4oGridData : IGridData
    {
        DB4oManager manager;

        public void Initialise() {
             manager = new DB4oManager("gridserver.yap");
        }

        public SimProfileData GetProfileByHandle(ulong handle) {
            lock (manager.profiles)
            {
                foreach (LLUUID UUID in manager.profiles.Keys)
                {
                    if (manager.profiles[UUID].regionHandle == handle)
                    {
                        return manager.profiles[UUID];
                    }
                }
            }
            throw new Exception("Unable to find profile with handle (" + handle.ToString() + ")");
        }

        public SimProfileData GetProfileByLLUUID(LLUUID uuid)
        {
            lock (manager.profiles)
            {
                if (manager.profiles.ContainsKey(uuid))
                    return manager.profiles[uuid];
            }
            throw new Exception("Unable to find profile with UUID (" + uuid.ToStringHyphenated() + ")");
        }

        public DataResponse AddProfile(SimProfileData profile)
        {
            lock (manager.profiles)
            {
                if (manager.AddRow(profile))
                {
                    return DataResponse.RESPONSE_OK;
                }
                else
                {
                    return DataResponse.RESPONSE_ERROR;
                }
            }
        }

        public bool AuthenticateSim(LLUUID uuid, ulong handle, string key) {
            if (manager.profiles[uuid].regionRecvKey == key)
                return true;
            return false;
        }

        public void Close()
        {
            manager = null;
        }

        public string getName()
        {
            return "DB4o Grid Provider";
        }

        public string getVersion()
        {
            return "0.1";
        }
    }
}
