using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Remoting.Lifetime;
using System.Text;

namespace ScriptAssemblies
{
    public class ScriptBase : MarshalByRefObject, IScript
    {

        #region AppDomain Serialization Keep-Alive
        //
        // Never expire this object
        //
        public override Object InitializeLifetimeService()
        {
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero;
            }
            return lease;
        }
        #endregion

        public delegate void ExecuteFunctionEventDelegate(string functionName, params object[] args);
        public event ExecuteFunctionEventDelegate OnExecuteFunction;

        private List<ICommandProvider> CommandProviders = new List<ICommandProvider>();

        public ScriptBase()
        {
        }

        public void ExecuteFunction(string functionName, params object[] args)
        {
            // We got a new command, fire event
            if (OnExecuteFunction != null)
                OnExecuteFunction(functionName, args);
            
        }
    }
}
