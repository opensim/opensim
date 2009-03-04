using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.ObjectModules
{
    interface IObjectModule
    {
        void Add(EntityBase entity, Scene scene);
        void Start();
        void Stop();
        void Tick();

        string ClassName { get; }
        bool IsShared { get; }
    }
}
