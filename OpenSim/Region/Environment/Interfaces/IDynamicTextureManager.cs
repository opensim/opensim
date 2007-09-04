using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using libsecondlife;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IDynamicTextureManager
    {
        void RegisterRender(string handleType, IDynamicTextureRender render);
        void ReturnData(LLUUID id, byte[] data);
        LLUUID AddDynamicTextureURL(LLUUID simID, LLUUID primID, string contentType, string url, string extraParams, int updateTimer);
        LLUUID AddDynamicTextureData(LLUUID simID, LLUUID primID, string contentType, string data, string extraParams, int updateTimer);
    }

    public interface IDynamicTextureRender
    {
        string GetName();
        string GetContentType();
        bool SupportsAsynchronous();
        byte[] ConvertUrl(string url, string extraParams);
        byte[] ConvertStream(Stream data, string extraParams);
        bool AsyncConvertUrl(LLUUID id, string url, string extraParams);
        bool AsyncConvertData(LLUUID id, string bodyData, string extraParams);
    }
}
