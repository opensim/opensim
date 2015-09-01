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

using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.CoreModules.World.Land;

namespace OpenSim.Tests.Common
{
    /// <summary>
    /// Land channel for test purposes
    /// </summary>
    public class TestLandChannel : ILandChannel
    {
        private Scene m_scene;
        private List<ILandObject> m_parcels;

        public TestLandChannel(Scene scene)
        {
            m_scene = scene;
            m_parcels = new List<ILandObject>();
            SetupDefaultParcel();
        }

        private void SetupDefaultParcel()
        {
            ILandObject obj = new LandObject(UUID.Zero, false, m_scene);
            obj.LandData.Name = "Your Parcel";
            m_parcels.Add(obj);
        }

        public List<ILandObject> ParcelsNearPoint(Vector3 position)
        {
            return new List<ILandObject>();
        }

        public List<ILandObject> AllParcels()
        {
            return m_parcels;
        }
        
        public void Clear(bool setupDefaultParcel)
        {
            m_parcels.Clear();

            if (setupDefaultParcel)
                SetupDefaultParcel();
        }

        protected ILandObject GetNoLand()
        {
            ILandObject obj = new LandObject(UUID.Zero, false, m_scene);
            obj.LandData.Name = "NO LAND";
            return obj;
        }

        public ILandObject GetLandObject(Vector3 position)
        {
            return GetLandObject(position.X, position.Y);
        }

        public ILandObject GetLandObject(int x, int y)
        {
            return GetNoLand();
        }

        public ILandObject GetLandObject(int localID)
        {
            return GetNoLand();
        }

        public ILandObject GetLandObject(float x, float y)
        {
            return GetNoLand();
        }

        public bool IsLandPrimCountTainted() { return false; }
        public bool IsForcefulBansAllowed() { return false; }
        public void UpdateLandObject(int localID, LandData data) {}
        public void ReturnObjectsInParcel(int localID, uint returnType, UUID[] agentIDs, UUID[] taskIDs, IClientAPI remoteClient) {}
        public void setParcelObjectMaxOverride(overrideParcelMaxPrimCountDelegate overrideDel) {}
        public void setSimulatorObjectMaxOverride(overrideSimulatorMaxPrimCountDelegate overrideDel) {}
        public void SetParcelOtherCleanTime(IClientAPI remoteClient, int localID, int otherCleanTime) {}

        public void Join(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id) {}
        public void Subdivide(int start_x, int start_y, int end_x, int end_y, UUID attempting_user_id) {}
    }
}