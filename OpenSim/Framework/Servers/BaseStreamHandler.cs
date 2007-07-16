using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace OpenSim.Framework.Servers
{
    public abstract class BaseStreamHandler : IStreamHandler
    {
        virtual public string ContentType
        {
            get { return "application/xml"; }
        }

        private string m_httpMethod;
        virtual public string HttpMethod
        {
            get { return m_httpMethod; }
        }

        private string m_path;
        virtual public string Path
        {
            get { return m_path; }
        }
	
        protected string GetParam( string path )
        {
           return path.Substring( m_path.Length );
        }
        
        public abstract byte[] Handle(string path, Stream request);

        protected BaseStreamHandler(string httpMethod, string path)
        {
            m_httpMethod = httpMethod;
            m_path = path;
        }
    }
}
