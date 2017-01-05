/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

using Nini.Config;

using OpenSim.Framework;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Region.PhysicsModule.Meshing;
using OpenSim.Region.Framework.Interfaces;

using OpenMetaverse;

namespace OpenSim.Region.PhysicsModule.BulletS.Tests
{
// Utility functions for building up and tearing down the sample physics environments
public static class BulletSimTestsUtil
{
    // 'engineName' is the Bullet engine to use. Either null (for unmanaged), "BulletUnmanaged" or "BulletXNA"
    // 'params' is a set of keyValue pairs to set in the engine's configuration file (override defaults)
    //      May be 'null' if there are no overrides.
    public static BSScene CreateBasicPhysicsEngine(Dictionary<string,string> paramOverrides)
    {
        IConfigSource openSimINI = new IniConfigSource();
        IConfig startupConfig = openSimINI.AddConfig("Startup");
        startupConfig.Set("physics", "BulletSim");
        startupConfig.Set("meshing", "Meshmerizer");
        startupConfig.Set("cacheSculptMaps", "false");  // meshmerizer shouldn't save maps

        IConfig bulletSimConfig = openSimINI.AddConfig("BulletSim");
        // If the caller cares, specify the bullet engine otherwise it will default to "BulletUnmanaged".
        // bulletSimConfig.Set("BulletEngine", "BulletUnmanaged");
        // bulletSimConfig.Set("BulletEngine", "BulletXNA");
        bulletSimConfig.Set("MeshSculptedPrim", "false");
        bulletSimConfig.Set("ForceSimplePrimMeshing", "true");
        if (paramOverrides != null)
        {
            foreach (KeyValuePair<string, string> kvp in paramOverrides)
            {
                bulletSimConfig.Set(kvp.Key, kvp.Value);
            }
        }

        // If a special directory exists, put detailed logging therein.
        // This allows local testing/debugging without having to worry that the build engine will output logs.
        if (Directory.Exists("physlogs"))
        {
            bulletSimConfig.Set("PhysicsLoggingDir","./physlogs");
            bulletSimConfig.Set("PhysicsLoggingEnabled","True");
            bulletSimConfig.Set("PhysicsLoggingDoFlush","True");
            bulletSimConfig.Set("VehicleLoggingEnabled","True");
        }

        Vector3 regionExtent = new Vector3(Constants.RegionSize, Constants.RegionSize, Constants.RegionHeight);

        RegionInfo info = new RegionInfo();
        info.RegionName = "BSTestRegion";
        info.RegionSizeX = info.RegionSizeY = info.RegionSizeZ = Constants.RegionSize;
        OpenSim.Region.Framework.Scenes.Scene scene = new OpenSim.Region.Framework.Scenes.Scene(info);

        IMesher mesher = new OpenSim.Region.PhysicsModule.Meshing.Meshmerizer();
        INonSharedRegionModule mod = mesher as INonSharedRegionModule;
        mod.Initialise(openSimINI);
        mod.AddRegion(scene);
        mod.RegionLoaded(scene);

        BSScene pScene = new BSScene();
        mod = (pScene as INonSharedRegionModule);
        mod.Initialise(openSimINI);
        mod.AddRegion(scene);
        mod.RegionLoaded(scene);

        // Since the asset requestor is not initialized, any mesh or sculptie will be a cube.
        // In the future, add a fake asset fetcher to get meshes and sculpts.
        // bsScene.RequestAssetMethod = ???;

        return pScene;
    }

}
}
