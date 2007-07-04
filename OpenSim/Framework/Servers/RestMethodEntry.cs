using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework.Servers
{
    public class RestMethodEntry
    {
        private string m_path;
        public string Path
        {
            get { return m_path; }
        }

        private RestMethod m_restMethod;
        public RestMethod RestMethod
        {
            get { return m_restMethod; }
        }

        public RestMethodEntry(string path, RestMethod restMethod)
        {
            m_path = path;
            m_restMethod = restMethod;
        }
    }
}
