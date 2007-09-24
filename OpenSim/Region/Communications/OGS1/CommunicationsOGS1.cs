using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Framework.Types;
using OpenSim.Framework.Servers;


namespace OpenSim.Region.Communications.OGS1
{
    public class CommunicationsOGS1 : CommunicationsManager
    {
        public CommunicationsOGS1(NetworkServersInfo serversInfo, BaseHttpServer httpServer, AssetCache assetCache ) :base(serversInfo, httpServer, assetCache)
        {
            OGS1GridServices gridInterComms = new OGS1GridServices(serversInfo, httpServer);
            m_gridServer = gridInterComms;
            m_interRegion = gridInterComms;

            m_inventoryServer = new OGS1InventoryService();
            m_userServer = new OGS1UserServices(this);
        }
    }
}
