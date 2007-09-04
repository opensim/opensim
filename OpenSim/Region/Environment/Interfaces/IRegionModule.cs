using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.Environment.Interfaces
{
    public interface IRegionModule
    {
        void Initialise(Scenes.Scene scene);
        void PostInitialise();
        void CloseDown();
        string GetName();
        bool IsSharedModule();
    }
}
