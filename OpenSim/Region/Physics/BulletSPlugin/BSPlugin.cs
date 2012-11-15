/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyrightD
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
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OpenMetaverse;

namespace OpenSim.Region.Physics.BulletSPlugin
{
    /// <summary>
    /// Entry for a port of Bullet (http://bulletphysics.org/) to OpenSim.
    /// This module interfaces to an unmanaged C++ library which makes the
    /// actual calls into the Bullet physics engine.
    /// The unmanaged library is found in opensim-libs::trunk/unmanaged/BulletSim/.
    /// The unmanaged library is compiled and linked statically with Bullet
    /// to create BulletSim.dll and libBulletSim.so (for both 32 and 64 bit).
    /// </summary>
public class BSPlugin : IPhysicsPlugin
{
    //private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

    private BSScene _mScene;

    public BSPlugin()
    {
    }

    public bool Init()
    {
        return true;
    }

    public PhysicsScene GetScene(String sceneIdentifier)
    {
        if (_mScene == null)
        {
            if (Util.IsWindows())
                Util.LoadArchSpecificWindowsDll("BulletSim.dll");
            // If not Windows, loading is performed by the
            // Mono loader as specified in
            // "bin/Physics/OpenSim.Region.Physics.BulletSPlugin.dll.config".

            _mScene = new BSScene(sceneIdentifier);
        }
        return (_mScene);
    }

    public string GetName()
    {
        return ("BulletSim");
    }

    public void Dispose()
    {
    }
}
}
