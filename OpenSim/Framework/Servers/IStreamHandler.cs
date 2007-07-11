using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace OpenSim.Framework.Servers
{
    public interface IStreamHandler
    {
        // Handle request stream, return byte array
        byte[] Handle(string path, Stream request );
        
        // Return response content type
        string ContentType { get; }
        
        // Return required http method
        string HttpMethod { get;}

        // Return path
        string Path { get; }
    }
}
