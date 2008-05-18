using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;

namespace OpenSim.Region.DataSnapshot.Interfaces
{
    public interface IDataSnapshot
    {
        XmlDocument GetSnapshot(string regionName);
        void MakeEverythingStale();
    }
}
