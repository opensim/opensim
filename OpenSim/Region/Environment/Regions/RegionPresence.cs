using OpenSim.Framework.Interfaces;

namespace OpenSim.Region.Environment.Regions
{
    public class RegionPresence
    {
        private IClientAPI m_client;

        public RegionPresence(IClientAPI client )
        {
            m_client = client;
        }
    }
}
