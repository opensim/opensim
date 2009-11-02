using System;

namespace OpenSim.Region.CoreModules.Framework.Monitoring
{
    internal delegate void Alert(Type reporter, string reason, bool fatal);

    interface IAlert
    {
        string GetName();
        void Test();
        event Alert OnTriggerAlert;
    }
}
