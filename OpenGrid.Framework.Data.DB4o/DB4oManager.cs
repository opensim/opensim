using System;
using System.Collections.Generic;
using System.Text;
using Db4objects.Db4o;
using OpenGrid.Framework.Data;
using libsecondlife;

namespace OpenGrid.Framework.Data.DB4o
{
    class DB4oGridManager
    {
        public Dictionary<LLUUID, SimProfileData> simProfiles = new Dictionary<LLUUID, SimProfileData>();
        string dbfl;

        public DB4oGridManager(string db4odb)
        {
            dbfl = db4odb;
            IObjectContainer database;
            database = Db4oFactory.OpenFile(dbfl);
            IObjectSet result = database.Get(typeof(SimProfileData));
            foreach(SimProfileData row in result) {
                simProfiles.Add(row.UUID, row);
            }
            database.Close();
        }

        /// <summary>
        /// Adds a new profile to the database (Warning: Probably slow.)
        /// </summary>
        /// <param name="row">The profile to add</param>
        /// <returns>Successful?</returns>
        public bool AddRow(SimProfileData row)
        {
            if (simProfiles.ContainsKey(row.UUID))
            {
                simProfiles[row.UUID] = row;
            }
            else
            {
                simProfiles.Add(row.UUID, row);
            }

            try
            {
                IObjectContainer database;
                database = Db4oFactory.OpenFile(dbfl);
                database.Set(row);
                database.Close();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }


    }

    class DB4oUserManager
    {
        public Dictionary<LLUUID, UserProfileData> userProfiles = new Dictionary<LLUUID, UserProfileData>();
        string dbfl;

        public DB4oUserManager(string db4odb)
        {
            dbfl = db4odb;
            IObjectContainer database;
            database = Db4oFactory.OpenFile(dbfl);
            IObjectSet result = database.Get(typeof(UserProfileData));
            foreach (UserProfileData row in result)
            {
                userProfiles.Add(row.UUID, row);
            }
            database.Close();
        }

        /// <summary>
        /// Adds a new profile to the database (Warning: Probably slow.)
        /// </summary>
        /// <param name="row">The profile to add</param>
        /// <returns>Successful?</returns>
        public bool AddRow(UserProfileData row)
        {
            if (userProfiles.ContainsKey(row.UUID))
            {
                userProfiles[row.UUID] = row;
            }
            else
            {
                userProfiles.Add(row.UUID, row);
            }

            try
            {
                IObjectContainer database;
                database = Db4oFactory.OpenFile(dbfl);
                database.Set(row);
                database.Close();
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }


    }
}
