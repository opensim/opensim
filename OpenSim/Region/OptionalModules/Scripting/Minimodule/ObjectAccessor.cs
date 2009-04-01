using System;
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;
using IEnumerable=System.Collections.IEnumerable;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{

    internal class IObjEnum : IEnumerator<IObject>
    {
        private readonly Scene m_scene;
        private readonly IEnumerator<EntityBase> m_sogEnum;

        public IObjEnum(Scene scene)
        {
            m_scene = scene;
            m_sogEnum = m_scene.Entities.GetAllByType<SceneObjectGroup>().GetEnumerator();
        }

        public void Dispose()
        {
            m_sogEnum.Dispose();
        }

        public bool MoveNext()
        {
            return m_sogEnum.MoveNext();
        }

        public void Reset()
        {
            m_sogEnum.Reset();
        }

        public IObject Current
        {
            get
            {
                return new SOPObject(m_scene, m_sogEnum.Current.LocalId);
            }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }
    }

    public class ObjectAccessor : IObjectAccessor
    {
        private readonly Scene m_scene;

        public ObjectAccessor(Scene scene)
        {
            m_scene = scene;
        }

        public IObject this[int index]
        {
            get
            {
                return new SOPObject(m_scene, m_scene.Entities[(uint)index].LocalId);
            }
        }

        public IObject this[uint index]
        {
            get
            {
                return new SOPObject(m_scene, m_scene.Entities[index].LocalId);
            }
        }

        public IObject this[UUID index]
        {
            get
            {
                return new SOPObject(m_scene, m_scene.Entities[index].LocalId);
            }
        }

        public IEnumerator<IObject> GetEnumerator()
        {
            return new IObjEnum(m_scene);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(IObject item)
        {
            throw new NotSupportedException("Collection is read-only. This is an API TODO FIX, creation of objects is presently impossible.");
        }

        public void Clear()
        {
            throw new NotSupportedException("Collection is read-only. TODO FIX.");
        }

        public bool Contains(IObject item)
        {
            return m_scene.Entities.ContainsKey(item.LocalID);
        }

        public void CopyTo(IObject[] array, int arrayIndex)
        {
            for (int i = arrayIndex; i < Count + arrayIndex; i++)
            {
                array[i] = this[i - arrayIndex];
            }
        }

        public bool Remove(IObject item)
        {
            throw new NotSupportedException("Collection is read-only. TODO FIX.");
        }

        public int Count
        {
            get { return m_scene.Entities.Count; }
        }

        public bool IsReadOnly
        {
            get { return true; }
        }
    }
}
