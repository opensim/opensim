using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim
{
    class MiniModule : MRMBase
    {
        public override void Start()
        {
            Host.Console.Info("Hello World!");
        }

        public override void Stop()
        {
            
        }
    }
}
