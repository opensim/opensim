using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;
using OpenSim.Region.ScriptEngine.Shared;

namespace OpenSim.ScriptEngine.Shared
{
    /// <summary>
    /// Holds all the data required to execute a scripting event.
    /// </summary>
    public class EventParams
    {
        public string EventName;
        public Object[] Params;
        public Region.ScriptEngine.Shared.DetectParams[] DetectParams;
        public uint LocalID;
        public UUID ItemID;

        public EventParams(uint localID, UUID itemID, string eventName, Object[] eventParams, DetectParams[] detectParams)
        {
            LocalID = localID;
            ItemID = itemID;
            EventName = eventName;
            Params = eventParams;
            DetectParams = detectParams;
        }
        public EventParams(uint localID, string eventName, Object[] eventParams, DetectParams[] detectParams)
        {
            LocalID = localID;
            EventName = eventName;
            Params = eventParams;
            DetectParams = detectParams;
        }
        public void test(params object[] args)
        {
            string functionName = "test";
            test2(functionName, args);
        }
        public void test2(string functionName, params object[] args)
        {
            System.Console.WriteLine(functionName, args);
        }


    }
}