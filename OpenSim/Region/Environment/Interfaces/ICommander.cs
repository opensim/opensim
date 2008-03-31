using System;

using OpenSim.Region.Environment.Modules.ModuleFramework;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface ICommander
    {
        void ProcessConsoleCommand(string function, string[] args);
        void RegisterCommand(string commandName, ICommand command);
        void Run(string function, object[] args);
        string GenerateRuntimeAPI();
    }
}
