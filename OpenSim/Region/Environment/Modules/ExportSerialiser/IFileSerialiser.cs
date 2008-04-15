using System;
using System.Collections.Generic;
using System.Text;

using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules.ExportSerialiser
{
    interface IFileSerialiser
    {
        string WriteToFile(Scene scene, string dir);
    }
}
