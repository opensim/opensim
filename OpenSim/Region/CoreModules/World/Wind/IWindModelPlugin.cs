using System;
using System.Collections.Generic;

using Nini.Config;
using OpenSim.Framework;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Wind
{
    public interface IWindModelPlugin : IPlugin
    {
        /// <summary>
        /// Brief description of this plugin's wind model
        /// </summary>
        string Description { get; }

        /// <summary>
        /// Provides access to the wind configuration, if any.
        /// </summary>
        void WindConfig(Scene scene, IConfig windConfig);

        /// <summary>
        /// Update wind.
        /// </summary>
        void WindUpdate(uint frame);

        /// <summary>
        /// Returns the wind vector at the given local region coordinates.
        /// </summary>
        Vector3 WindSpeed(float x, float y, float z);

        /// <summary>
        /// Generate a 16 x 16 Vector2 array of wind speeds for LL* based viewers
        /// </summary>
        /// <returns>Must return a Vector2[256]</returns>
        Vector2[] WindLLClientArray();

        /// <summary>
        /// Retrieve a list of parameter/description pairs.
        /// </summary>
        /// <returns></returns>
        Dictionary<string, string> WindParams();

        /// <summary>
        /// Set the specified parameter
        /// </summary>
        void WindParamSet(string param, float value);

        /// <summary>
        /// Get the specified parameter
        /// </summary>
        float WindParamGet(string param);

    }
}
