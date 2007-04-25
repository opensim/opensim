using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Types;

namespace OpenSim.Framework.Interfaces
{
    public interface IScriptAPI
    {
        OSVector3 GetEntityPosition(uint localID);
        void SetEntityPosition(uint localID, float x, float y, float z);
        uint GetRandomAvatarID();
    }
}
