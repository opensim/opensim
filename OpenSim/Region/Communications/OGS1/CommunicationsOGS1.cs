using OpenSim.Framework.Communications;
using OpenSim.Framework.Types;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Communications.Caches;
 

namespace OpenSim.Region.Communications.OGS1
{
    public class CommunicationsOGS1 : CommunicationsManager
    {
        public OGS1InventoryService InvenService;

        public CommunicationsOGS1(NetworkServersInfo serversInfo, BaseHttpServer httpServer, AssetCache assetCache ) :base(serversInfo, httpServer, assetCache)
        {
            OGS1GridServices gridInterComms = new OGS1GridServices(serversInfo, httpServer);
            GridServer = gridInterComms;
            InterRegion = gridInterComms;

            InvenService = new OGS1InventoryService();
            InventoryServer = InvenService;

            UserServer = new OGS1UserServices(this);
        }
    }
}
