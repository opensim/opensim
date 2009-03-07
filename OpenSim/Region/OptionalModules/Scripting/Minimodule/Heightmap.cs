using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    public class Heightmap : IHeightmap
    {
        private Scene m_scene;

        public Heightmap(Scene scene)
        {
            m_scene = scene;
        }

        public int Height
        {
            get { return m_scene.Heightmap.Height; }
        }

        public int Width
        {
            get { return m_scene.Heightmap.Width; }
        }

        public double Get(int x, int y)
        {
            return m_scene.Heightmap[x, y];
        }

        public void Set(int x, int y, double val)
        {
            m_scene.Heightmap[x, y] = val;
        }
    }
}
