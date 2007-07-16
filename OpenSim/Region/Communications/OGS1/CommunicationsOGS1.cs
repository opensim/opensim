using OpenSim.Framework.Communications;
using OpenSim.Framework.Types;
using OpenSim.Framework.Servers;

namespace OpenSim.Region.Communications.OGS1
{
    public class CommunicationsOGS1 : CommunicationsManager
    {
        
        public CommunicationsOGS1(NetworkServersInfo serversInfo, BaseHttpServer httpServer ) :base(serversInfo, httpServer)
        {
            OGS1GridServices gridInterComms = new OGS1GridServices(serversInfo, httpServer);
            GridServer = gridInterComms;
            InterRegion = gridInterComms;
            UserServer = new OGS1UserServices(this);
        }
    }
}
