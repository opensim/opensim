using System;
namespace OpenSim.Region.Environment.Modules.ExportSerialiser
{
    public interface IRegionSerialiser
    {
        System.Collections.Generic.List<string> SerialiseRegion(OpenSim.Region.Environment.Scenes.Scene scene, string saveDir);
    }
}
