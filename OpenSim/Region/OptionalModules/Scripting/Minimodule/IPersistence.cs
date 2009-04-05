using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    interface IPersistence
    {
        T Get<T>(Guid storageID);
        T Get<T>();

        /// <summary>
        /// Stores 'data' into the persistence system
        /// associated with this object, however saved
        /// under the ID 'storageID'. This data may
        /// be accessed by other scripts however.
        /// </summary>
        /// <param name="storageID"></param>
        /// <param name="data"></param>
        void Put<T>(Guid storageID, T data);

        /// <summary>
        /// Stores 'data' into the persistence system
        /// using the default ID for this script.
        /// </summary>
        /// <param name="data"></param>
        void Put<T>(T data);
    }
}
