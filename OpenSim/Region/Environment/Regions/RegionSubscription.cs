using OpenSim.Framework.Interfaces;

namespace OpenSim.Region.Environment.Regions
{
    public class RegionSubscription
    {
        private readonly IClientAPI m_client;

        public RegionSubscription(IClientAPI client )
        {
            m_client = client;
        }

        public IClientAPI Client
        {
            get { return m_client; }
        }
    }
}
