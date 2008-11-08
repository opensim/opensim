using OpenSim.ScriptEngine.Shared;

namespace OpenSim.ScriptEngine.Shared
{
    public interface IScriptLoader: IScriptEngineComponent
    {
        ScriptAssemblies.IScript LoadScript(ScriptStructure script);
    }
}