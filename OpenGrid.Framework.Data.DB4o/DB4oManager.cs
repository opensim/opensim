using System;
using System.Collections.Generic;
using System.Text;
using Db4objects.Db4o;
using OpenGrid.Framework.Data;
using libsecondlife;

namespace OpenGrid.Framework.Data.DB4o
{
    class DB4oManager
    {
        public Dictionary<LLUUID, SimProfileData> profiles = new Dictionary<LLUUID, SimProfileData>();
        string dbfl;

        public DB4oManager(string db4odb)
        {
            dbfl = db4odb;
            IObjectContainer database;
            database = Db4oFactory.OpenFile(dbfl);
            IObjectSet result = database.Get(typeof(SimProfileData));
            foreach(SimProfileData row in result) {
                profiles.Add(row.UUID, row);
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
            profiles.Add(row.UUID, row);

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
