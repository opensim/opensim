using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.Interfaces;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class ExtensionHandler : IExtension 
    {
        private readonly Dictionary<Type, object> m_instances;

        public ExtensionHandler(Dictionary<Type, object> instances)
        {
            m_instances = instances;
        }

        public T Get<T>()
        {
            return (T) m_instances[typeof (T)];
        }

        public bool TryGet<T>(out T extension)
        {
            if (!m_instances.ContainsKey(typeof(T)))
            {
                extension = default(T);
                return false;
            }

            extension = Get<T>();
            return true;
        }

        public bool Has<T>()
        {
            return m_instances.ContainsKey(typeof (T));
        }
    }
}
