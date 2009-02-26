using System;
using OpenSim.Framework.Servers;

namespace OpenSim.Grid.Framework
{
    public interface IGridServiceModule
    {
        void Close();
        void Initialise(IGridServiceCore core);
        void PostInitialise();
        void RegisterHandlers(BaseHttpServer httpServer);
    }
}
