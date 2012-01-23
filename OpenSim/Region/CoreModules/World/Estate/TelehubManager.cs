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
using OpenMetaverse;
using System.Collections.Generic;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules.World.Estate
{
    public class TelehubManager
    {
        Scene m_Scene;

        public TelehubManager(Scene scene)
        {
            m_Scene = scene;
        }

        // Connect the Telehub
        public bool Connect(SceneObjectPart part)
        {
            bool result = false;

            if (m_Scene.RegionInfo.RegionSettings.HasTelehub)
                return result;

            try
            {
                m_Scene.RegionInfo.RegionSettings.ClearSpawnPoints();
    
                m_Scene.RegionInfo.RegionSettings.TelehubObject = part.UUID;
                m_Scene.RegionInfo.RegionSettings.TelehubName = part.Name;
                m_Scene.RegionInfo.RegionSettings.TelehubPos = part.AbsolutePosition;
                m_Scene.RegionInfo.RegionSettings.TelehubRot = part.GetWorldRotation();
                m_Scene.RegionInfo.RegionSettings.AddSpawnPoint(new Vector3(0.0f,0.0f,0.0f));
                m_Scene.RegionInfo.RegionSettings.HasTelehub = true;
                m_Scene.RegionInfo.RegionSettings.Save();

                result = true;
            }
            catch (Exception ex)
            {
                result = false;
            }

            return result;
        }

        // Disconnect the Telehub:
        public bool DisConnect(SceneObjectPart part)
        {
            bool result = false;

            if (!m_Scene.RegionInfo.RegionSettings.HasTelehub)
                return result;

            try
            {
                m_Scene.RegionInfo.RegionSettings.TelehubObject = UUID.Zero;
                m_Scene.RegionInfo.RegionSettings.TelehubName = String.Empty;
                m_Scene.RegionInfo.RegionSettings.TelehubPos = Vector3.Zero;
                m_Scene.RegionInfo.RegionSettings.ClearSpawnPoints();
                m_Scene.RegionInfo.RegionSettings.HasTelehub = false;
                m_Scene.RegionInfo.RegionSettings.Save();

                result = true;
            }
            catch (Exception ex)
            {
                result = false;
            }

            return result;
        }

        // Add a SpawnPoint to the Telehub
        public bool AddSpawnPoint(Vector3 point)
        {
            bool result = false;

            if (!m_Scene.RegionInfo.RegionSettings.HasTelehub)
                return result;

            try
            {
                Vector3 thPos = m_Scene.RegionInfo.RegionSettings.TelehubPos;
                Quaternion thRot = m_Scene.RegionInfo.RegionSettings.TelehubRot;

                float dist = (float) Util.GetDistanceTo(thPos, point);
                Vector3 nvec = Util.GetNormalizedVector(point - thPos);
                Vector3 spoint = nvec * dist;

                m_Scene.RegionInfo.RegionSettings.AddSpawnPoint(spoint);
                m_Scene.RegionInfo.RegionSettings.Save();
                result = true;
            }
            catch (Exception ex)
            {
                result = false;
            }

            return result;
        }

        // Remove a SpawnPoint from the Telehub
        public bool RemoveSpawnPoint(int spawnpoint)
        {
            if (!m_Scene.RegionInfo.RegionSettings.HasTelehub)
                return false;

            m_Scene.RegionInfo.RegionSettings.RemoveSpawnPoint(spawnpoint);
            m_Scene.RegionInfo.RegionSettings.Save();

            return true;
        }
    }
}
