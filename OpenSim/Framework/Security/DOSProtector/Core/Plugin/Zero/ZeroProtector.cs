using System;
using OpenSim.Framework.Security.DOSProtector.SDK;

namespace OpenSim.Framework.Security.DOSProtector.Core.Plugin.Zero
{
    public class ZeroOptions : BaseDosProtectorOptions
    {
    }

    /// <summary>
    /// Zero DOS Protector - No-Op implementation that allows all requests.
    /// Useful for disabling DOS protection without changing code or for testing.
    /// </summary>
    [DOSProtectorOptions(typeof(ZeroOptions))]
    public class ZeroProtector : BaseDOSProtector
    {
        public ZeroProtector(IDOSProtectorOptions options) : base(options)
        {
            
        }

        /// <summary>
        /// Never blocks any client
        /// </summary>
        public override bool IsBlocked(string key, IDOSProtectorContext context = null)
        {
            return false;
        }

        /// <summary>
        /// Always allows the request
        /// </summary>
        public override bool Process(string key, string endpoint, IDOSProtectorContext context = null)
        {
            return true;
        }

        /// <summary>
        /// No cleanup needed
        /// </summary>
        public override void ProcessEnd(string key, string endpoint, IDOSProtectorContext context = null)
        {
            // Nothing to do
        }

        /// <summary>
        /// Creates a no-op session scope
        /// </summary>
        public override IDisposable CreateSession(string key, string endpoint, IDOSProtectorContext context = null)
        {
            return new SessionScope(this, key, endpoint, context);
        }

        /// <summary>
        /// Nothing to dispose
        /// </summary>
        public override void Dispose()
        {
            // Nothing to dispose
        }
    }
}