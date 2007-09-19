using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IRegionModule
    {
        void Initialise(Scene scene);
        void PostInitialise();
        void CloseDown();
        string GetName();
        bool IsSharedModule();
    }
}