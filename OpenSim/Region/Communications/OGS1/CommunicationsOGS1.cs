using OpenSim.Framework.Communications;
using OpenSim.Framework.Types;

namespace OpenSim.Region.Communications.OGS1
{
    public class CommunicationsOGS1 : CommunicationsManager
    {
        
        public CommunicationsOGS1(NetworkServersInfo serversInfo) :base(serversInfo)
        {
            OGS1GridServices gridInterComms = new OGS1GridServices(serversInfo);
            GridServer = gridInterComms;
            InterRegion = gridInterComms;
            UserServer = new OGS1UserServices(this);
        }
    }
}
