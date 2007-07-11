using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Framework.Servers;
using System.IO;
using System.Collections;
using libsecondlife;

namespace OpenSim.Region.Capabilities
{
    public class LLSDStreamhandler<TRequest, TResponse> : BaseStreamHandler
        where TRequest : new()
    {
        private LLSDMethod<TRequest, TResponse> m_method;

        public LLSDStreamhandler(string httpMethod, string path, LLSDMethod<TRequest, TResponse> method)
            : base(httpMethod, path )
        {
            m_method = method;
        }
        
        public override byte[] Handle(string path, Stream request)
        {
            //Encoding encoding = Encoding.UTF8;
            //StreamReader streamReader = new StreamReader(request, false);

            //string requestBody = streamReader.ReadToEnd();
            //streamReader.Close();

            Hashtable hash = (Hashtable)LLSD.LLSDDeserialize( request );
            TRequest llsdRequest = new TRequest();
            LLSDHelpers.DeserialiseLLSDMap(hash, llsdRequest);

            TResponse response = m_method(llsdRequest);

            Encoding encoding = new UTF8Encoding(false);
            
            return encoding.GetBytes( LLSDHelpers.SerialiseLLSDReply(response) );

        }
    }
}
