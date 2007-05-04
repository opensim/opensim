using System;
using System.Collections.Generic;
using System.Text;

namespace OpenGrid.Framework.Data
{
    public interface IGridData
    {
        SimProfileData GetProfileByHandle(ulong regionHandle);
        SimProfileData GetProfileByLLUUID(libsecondlife.LLUUID UUID);
        bool AuthenticateSim(libsecondlife.LLUUID UUID, ulong regionHandle, string simrecvkey);
        void Initialise();
    }
}
