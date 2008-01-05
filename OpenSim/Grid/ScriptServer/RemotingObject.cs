using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.ScriptEngine.Common;

namespace OpenSim.Grid.ScriptServer
{
    public class RemotingObject : MarshalByRefObject, ScriptServerInterfaces.ServerRemotingObject
    {
        // This object will be exposed over remoting. It is a singleton, so it exists only in as one instance.

        // Expose ScriptEngine directly for now ... this is not very secure :)
        // NOTE! CURRENTLY JUST HARDWIRED DOTNETENGINE!
        //private OpenSim.Region.ScriptEngine.DotNetEngine.ScriptEngine SE =
        //    new OpenSim.Region.ScriptEngine.DotNetEngine.ScriptEngine();
        //public OpenSim.Region.ScriptEngine.Common.ScriptServerInterfaces.RemoteEvents Events = 
        //    (OpenSim.Region.ScriptEngine.Common.ScriptServerInterfaces.RemoteEvents)SE.m_EventManager;

        //private ScriptServerInterfaces.RemoteEvents _events = new abc;

        ScriptServerInterfaces.RemoteEvents ScriptServerInterfaces.ServerRemotingObject.Events()
        {
            return null;
        }
    }
}
