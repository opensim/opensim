using System;
using System.Collections.Generic;
using System.Collections.Specialized;

namespace OpenSim.Framework.ServiceAuth
{
    public delegate void AddHeaderDelegate(string key, string value);

    public interface  IServiceAuth
    {
        bool Authenticate(string data);
        bool Authenticate(NameValueCollection headers, AddHeaderDelegate d);
        void AddAuthorization(NameValueCollection headers);
    }
}
