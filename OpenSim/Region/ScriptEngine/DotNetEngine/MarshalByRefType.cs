using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace OpenSim.Region.ScriptEngine.DotNetEngine
{
    // Because this class is derived from MarshalByRefObject, a proxy 
    // to a MarshalByRefType object can be returned across an AppDomain 
    // boundary.
    public class MarshalByRefType : MarshalByRefObject
    {
        //  Call this method via a proxy.
        public void SomeMethod(string callingDomainName)
        {
            // Get this AppDomain's settings and display some of them.
            AppDomainSetup ads = AppDomain.CurrentDomain.SetupInformation;
            Console.WriteLine("AppName={0}, AppBase={1}, ConfigFile={2}",
                ads.ApplicationName,
                ads.ApplicationBase,
                ads.ConfigurationFile
            );

            // Display the name of the calling AppDomain and the name 
            // of the second domain.
            // NOTE: The application's thread has transitioned between 
            // AppDomains.
            Console.WriteLine("Calling from '{0}' to '{1}'.",
                callingDomainName,
                Thread.GetDomain().FriendlyName
            );
        }
    }
}
