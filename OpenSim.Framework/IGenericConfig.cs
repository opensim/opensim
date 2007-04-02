using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Interfaces
{
    public interface IGenericConfig
    {
        void LoadData();
        string GetAttribute(string attributeName);
        bool SetAttribute(string attributeName, string attributeValue);
        void Commit();
        void Close();
    }
}
