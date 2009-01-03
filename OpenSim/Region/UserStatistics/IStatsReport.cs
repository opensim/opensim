using System.Collections;

namespace OpenSim.Region.UserStatistics
{
    public interface IStatsController
    {
        Hashtable ProcessModel(Hashtable pParams);
        string RenderView(Hashtable pModelResult);
    }
}
