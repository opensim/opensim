using OpenSim.Framework;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.Grid.Interregion
{
    public interface IInterregionModule
    {
        void RegisterMethod<T>(T e);
        bool HasInterface<T>(Location loc);
        T RequestInterface<T>(Location loc);
        T[] RequestInterface<T>();
        Location GetLocationByDirection(Scene scene, InterregionModule.Direction dir);
        void internal_CreateRemotingObjects();
    }
}