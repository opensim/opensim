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


        ScriptServerInterfaces.RemoteEvents ScriptServerInterfaces.ServerRemotingObject.Events()
        {
            return ScriptServerMain.Engine.EventManager();
        }
    }
}
