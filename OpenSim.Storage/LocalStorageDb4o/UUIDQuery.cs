using System;
using System.Collections.Generic;
using System.Text;
using Db4objects.Db4o;
using Db4objects.Db4o.Query;
using libsecondlife;
using OpenSim.Framework.Interfaces;
using OpenSim.Framework.Assets;

namespace OpenSim.Storage.LocalStorageDb4o
{
    public class UUIDQuery : Predicate
    {
        private LLUUID _findID;

        public UUIDQuery(LLUUID find)
        {
            _findID = find;
        }
        public bool Match(PrimData prim)
        {
            return (prim.FullID == _findID);
        }
    }
}
