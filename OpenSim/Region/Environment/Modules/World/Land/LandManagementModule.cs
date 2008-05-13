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
 *     * Neither the name of the OpenSim Project nor the
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

using Nini.Config;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework;

namespace OpenSim.Region.Environment.Modules.World.Land
{
    public class LandManagementModule : IRegionModule
    {
        private LandChannel landChannel;
        private Scene m_scene;

        #region IRegionModule Members

        public void Initialise(Scene scene, IConfigSource source)
        {
            m_scene = scene;
            landChannel = new LandChannel(scene);

            m_scene.EventManager.OnParcelPrimCountAdd += landChannel.AddPrimToLandPrimCounts;
            m_scene.EventManager.OnParcelPrimCountUpdate += landChannel.UpdateLandPrimCounts;
            m_scene.EventManager.OnAvatarEnteringNewParcel += new EventManager.AvatarEnteringNewParcel(landChannel.handleAvatarChangingParcel);
            m_scene.EventManager.OnClientMovement += new EventManager.ClientMovement(landChannel.handleAnyClientMovement);
            m_scene.EventManager.OnValidateLandBuy += landChannel.handleLandValidationRequest;
            m_scene.EventManager.OnLandBuy += landChannel.handleLandBuyRequest;
            m_scene.EventManager.OnNewClient += new EventManager.OnNewClientDelegate(EventManager_OnNewClient);
            m_scene.EventManager.OnSignificantClientMovement += landChannel.handleSignificantClientMovement;
            lock (m_scene)
            {
                m_scene.LandChannel = (ILandChannel) landChannel;
            }
        }

        void EventManager_OnNewClient(IClientAPI client)
        {
            //Register some client events
            client.OnParcelPropertiesRequest += new ParcelPropertiesRequest(landChannel.handleParcelPropertiesRequest);
            client.OnParcelDivideRequest += new ParcelDivideRequest(landChannel.handleParcelDivideRequest);
            client.OnParcelJoinRequest += new ParcelJoinRequest(landChannel.handleParcelJoinRequest);
            client.OnParcelPropertiesUpdateRequest += new ParcelPropertiesUpdateRequest(landChannel.handleParcelPropertiesUpdateRequest);
            client.OnParcelSelectObjects += new ParcelSelectObjects(landChannel.handleParcelSelectObjectsRequest);
            client.OnParcelObjectOwnerRequest += new ParcelObjectOwnerRequest(landChannel.handleParcelObjectOwnersRequest);
            client.OnParcelAccessListRequest += new ParcelAccessListRequest(landChannel.handleParcelAccessRequest);
            client.OnParcelAccessListUpdateRequest += new ParcelAccessListUpdateRequest(landChannel.handleParcelAccessUpdateRequest);
            client.OnParcelAbandonRequest += new ParcelAbandonRequest(landChannel.handleParcelAbandonRequest);
        }

        

        public void PostInitialise()
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "LandManagementModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        #endregion
    }
}