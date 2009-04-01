using System;
using System.Collections.Generic;

using OpenMetaverse;


namespace OpenSim.Region.CoreModules.World.Wind.Plugins
{
    class SimpleRandomWind : Mono.Addins.TypeExtensionNode, IWindModelPlugin
    {
        private Vector2[] m_windSpeeds = new Vector2[16 * 16];
        private float m_strength = 1.0f;
        private Random m_rndnums = new Random(Environment.TickCount);


        #region IPlugin Members

        public string Version
        {
            get { return "1.0.0.0"; }
        }

        public string Name
        {
            get { return "SimpleRandomWind"; }
        }

        public void Initialise()
        {
            
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            m_windSpeeds = null;
        }

        #endregion

        #region IWindModelPlugin Members

        public void WindConfig(OpenSim.Region.Framework.Scenes.Scene scene, Nini.Config.IConfig windConfig)
        {
            if( windConfig != null )
            {
                if( windConfig.Contains("strength") )
                {
                    m_strength = windConfig.GetFloat("strength", 1.0F);
                }
            }
        }

        public void WindUpdate(uint frame)
        {
            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    m_windSpeeds[y * 16 + x].X = (float)(m_rndnums.NextDouble() * 2d - 1d); // -1 to 1
                    m_windSpeeds[y * 16 + x].Y = (float)(m_rndnums.NextDouble() * 2d - 1d); // -1 to 1
                    m_windSpeeds[y * 16 + x].X *= m_strength;
                    m_windSpeeds[y * 16 + x].Y *= m_strength;
                }
            }            
        }

        public Vector3 WindSpeed(float fX, float fY, float fZ)
        {
            Vector3 windVector = new Vector3(0.0f, 0.0f, 0.0f);

            int x = (int)fX / 16;
            int y = (int)fY / 16;

            if (x < 0) x = 0;
            if (x > 15) x = 15;
            if (y < 0) y = 0;
            if (y > 15) y = 15;

            if (m_windSpeeds != null)
            {
                windVector.X = m_windSpeeds[y * 16 + x].X;
                windVector.Y = m_windSpeeds[y * 16 + x].Y;
            }

            return windVector;
            
        }

        public Vector2[] WindLLClientArray()
        {
            return m_windSpeeds;
        }

        public string Description
        {
            get
            {
                return "Provides a simple wind model that creates random wind of a given strength in 16m x 16m patches.";
            }
        }

        public System.Collections.Generic.Dictionary<string, string> WindParams()
        {
            Dictionary<string, string> Params = new Dictionary<string, string>();

            Params.Add("strength", "wind strength");

            return Params;
        }

        public void WindParamSet(string param, float value)
        {
            switch (param)
            {
                case "strength":
                    m_strength = value;
                    break;
            }
        }

        public float WindParamGet(string param)
        {
            switch (param)
            {
                case "strength":
                    return m_strength;
                default:
                    throw new Exception(String.Format("Unknown {0} parameter {1}", this.Name, param));
            }
        }

        #endregion

    }
}
