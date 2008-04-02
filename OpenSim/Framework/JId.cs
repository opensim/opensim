using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Framework
{
    public class JId
    {
        public string ServerIP = String.Empty;
        public int ServerPort = 0;
        public string username = String.Empty;
        public string resource = String.Empty;

        public JId()
        {

        }
        public JId(string sJId)
        {
            // user@address:port/resource
            string[] jidsplit = sJId.Split('@');
            if (jidsplit.GetUpperBound(0) == 2)
            {
                string[] serversplit = jidsplit[1].Split(':');
                if (serversplit.GetUpperBound(0) == 2)
                {
                    ServerIP = serversplit[0];
                    string[] resourcesplit = serversplit[1].Split('/');

                    ServerPort = Convert.ToInt32(resourcesplit[0]);
                    
                    if (resourcesplit.GetUpperBound(0) == 2)
                        resource = resourcesplit[1];

                    username = jidsplit[0];

                }
            }
        }

    }
}
