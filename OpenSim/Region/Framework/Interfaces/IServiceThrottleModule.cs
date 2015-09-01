using System;
using System.Collections.Generic;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IServiceThrottleModule
    {
        /// <summary>
        /// Enqueue a continuation meant to get a resource from elsewhere.
        /// As usual with CPS, caller beware: if that continuation is a never-ending computation,
        /// the whole thread will be blocked, and no requests are processed
        /// </summary>
        /// <param name="category">Category of the resource (e.g. name, region)</param>
        /// <param name="itemid">The resource identifier</param>
        /// <param name="continuation">The continuation to be executed</param>
        void Enqueue(string category, string itemid, Action continuation);
    }

}
