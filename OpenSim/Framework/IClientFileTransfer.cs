using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Framework
{
    public delegate void UploadComplete(string filename, UUID fileID, byte[] fileData, IClientAPI remoteClient);
    public delegate void UploadAborted(string filename, ulong id, IClientAPI remoteClient);

    public interface IClientFileTransfer
    {
        bool RequestUpload(string clientFileName, UploadComplete uploadCompleteCallback, UploadAborted abortCallback);
    }
}
