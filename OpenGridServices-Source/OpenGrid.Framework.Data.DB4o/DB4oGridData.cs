using System;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Data;
using libsecondlife;


namespace OpenGrid.Framework.Data.DB4o
{
    class DB4oGridData : IGridData
    {
        DB4oGridManager manager;

        public void Initialise() {
             manager = new DB4oGridManager("gridserver.yap");
        }

        public SimProfileData[] GetProfilesInRange(uint a, uint b, uint c, uint d)
        {
            return null;
        }

        public SimProfileData GetProfileByHandle(ulong handle) {
            lock (manager.simProfiles)
            {
                foreach (LLUUID UUID in manager.simProfiles.Keys)
                {
                    if (manager.simProfiles[UUID].regionHandle == handle)
                    {
                        return manager.simProfiles[UUID];
                    }
                }
            }
            throw new Exception("Unable to find profile with handle (" + handle.ToString() + ")");
        }

        public SimProfileData GetProfileByLLUUID(LLUUID uuid)
        {
            lock (manager.simProfiles)
            {
                if (manager.simProfiles.ContainsKey(uuid))
                    return manager.simProfiles[uuid];
            }
            throw new Exception("Unable to find profile with UUID (" + uuid.ToStringHyphenated() + ")");
        }

        public DataResponse AddProfile(SimProfileData profile)
        {
            lock (manager.simProfiles)
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
            if (manager.simProfiles[uuid].regionRecvKey == key)
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
