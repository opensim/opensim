using System.Collections;
using System.Text;
using libsecondlife;
using OpenSim.Region.Capabilities;

namespace OpenSim.Framework.Servers
{
    public class LlsdMethodEntry<TResponse, TRequest> : ILlsdMethodHandler
        where TRequest : new()
    {
        private LlsdMethod<TResponse, TRequest> m_method;


        public LlsdMethodEntry( )
        {
 
        }
        
        public LlsdMethodEntry(LlsdMethod<TResponse, TRequest> method)
        {
            m_method = method;
        }

        #region ILlsdMethodHandler Members

        public string Handle(string body, string path)
        {
            Encoding _enc = Encoding.UTF8;
            Hashtable hash = (Hashtable)LLSD.LLSDDeserialize(_enc.GetBytes( body ));
            TRequest request = new TRequest();
            
            LLSDHelpers.DeserialiseLLSDMap(hash, request );

            TResponse response = m_method(request);
            
            return LLSDHelpers.SerialiseLLSDReply( response );   
        }

        #endregion
    }
}
