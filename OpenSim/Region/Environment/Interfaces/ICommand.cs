using System;

using OpenSim.Region.Environment.Modules.ModuleFramework;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface ICommand
    {
        void AddArgument(string name, string helptext, string type);
        System.Collections.Generic.Dictionary<string, string> Arguments { get; }
        string Help { get; }
        string Name { get; }
        void Run(object[] args);
        void ShowConsoleHelp();
    }
}
