using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class SPAvatar : IAvatar
    {
        private readonly Scene m_rootScene;
        private readonly UUID m_ID;

        public SPAvatar(Scene scene, UUID ID)
        {
            m_rootScene = scene;
            m_ID = ID;
        }

        private ScenePresence GetSP()
        {
            return m_rootScene.GetScenePresence(m_ID);
        }

        public string Name
        {
            get { return GetSP().Name; }
        }

        public UUID GlobalID
        {
            get { return m_ID; }
        }

        public Vector3 Position
        {
            get { return GetSP().AbsolutePosition; }
        }
    }
}
