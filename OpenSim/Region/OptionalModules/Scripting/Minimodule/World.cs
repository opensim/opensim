using System.Collections.Generic;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public class World : IWorld 
    {
        private readonly Scene m_internalScene;
        private readonly Heightmap m_heights;

        public World(Scene internalScene)
        {
            m_internalScene = internalScene;
            m_heights = new Heightmap(m_internalScene);
        }

        public IObject[] Objects
        {
            get
            {
                List<EntityBase> ents = m_internalScene.Entities.GetAllByType<SceneObjectGroup>();
                IObject[] rets = new IObject[ents.Count];

                for (int i = 0; i < ents.Count; i++)
                {
                    EntityBase ent = ents[i];
                    rets[i] = new SOPObject(m_internalScene, ent.LocalId);
                }

                return rets;
            }
        }

        public IHeightmap Terrain
        {
            get { return m_heights; }
        }
    }
}
