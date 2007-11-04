namespace OpenSim.Framework.Servers
{
    public class BaseRequestHandler
    {
        public virtual string ContentType
        {
            get { return "application/xml"; }
        }

        private readonly string m_httpMethod;

        public virtual string HttpMethod
        {
            get { return m_httpMethod; }
        }

        private readonly string m_path;

        protected BaseRequestHandler(string httpMethod, string path)
        {
            m_httpMethod = httpMethod;
            m_path = path;
        }

        public virtual string Path
        {
            get { return m_path; }
        }

        protected string GetParam(string path)
        {
            return path.Substring(m_path.Length);
        }
    }
}