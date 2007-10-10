using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IRegionModule
    {
        void Initialise(Scene scene);
        void PostInitialise();
        void Close();
        string Name { get; }
        bool IsSharedModule { get; }
    }
}