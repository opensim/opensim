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
        
        public DB4oManager(string db4odb)
        {
            IObjectContainer database;
            database = Db4oFactory.OpenFile(db4odb);
            IObjectSet result = database.Get(typeof(SimProfileData));
            foreach(SimProfileData row in result) {
                profiles.Add(row.UUID, row);
            }
            database.Close();
        }


    }
}
