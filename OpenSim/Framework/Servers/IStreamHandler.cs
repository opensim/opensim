using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace OpenSim.Framework.Servers
{
    public interface IStreamHandler
    {
        void Handle(string path, Stream request, Stream response);
    }
}
