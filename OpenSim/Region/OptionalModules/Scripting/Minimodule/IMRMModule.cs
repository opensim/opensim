namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public interface IMRMModule
    {
        void RegisterExtension<T>(T instance);
    }
}