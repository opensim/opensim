using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    public class TempWorldInterfaceEventDelegates
    {
        public delegate void touch_start(string ObjectID);
    }
    public interface TempWorldInterface
    {
        event TempWorldInterfaceEventDelegates.touch_start touch_start;
    }
}
