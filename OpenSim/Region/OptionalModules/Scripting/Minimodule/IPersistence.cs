using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    interface IPersistence
    {
        Object Get(MRMBase state, Guid storageID);
        Object Get(MRMBase state);

        /// <summary>
        /// Stores 'data' into the persistence system
        /// associated with this object, however saved
        /// under the ID 'storageID'. This data may
        /// be accessed by other scripts however.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="storageID"></param>
        /// <param name="data"></param>
        void Put(MRMBase state, Guid storageID, Object data);

        /// <summary>
        /// Stores 'data' into the persistence system
        /// using the default ID for this script.
        /// </summary>
        /// <param name="state"></param>
        /// <param name="data"></param>
        void Put(MRMBase state, Object data);
    }
}
