using System;
using System.Collections.Generic;
using libsecondlife;
using libsecondlife.Packets;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Interfaces;
using Nini.Config;

namespace OpenSim.Region.Environment.Modules.LandManagement
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
                   
            m_scene.EventManager.OnParcelPrimCountAdd += landChannel.addPrimToLandPrimCounts;
            m_scene.EventManager.OnParcelPrimCountUpdate += landChannel.updateLandPrimCounts;
            m_scene.EventManager.OnAvatarEnteringNewParcel += new EventManager.AvatarEnteringNewParcel(landChannel.handleAvatarChangingParcel);
            m_scene.EventManager.OnClientMovement += new EventManager.ClientMovement(landChannel.handleAnyClientMovement);

            lock (m_scene)
            {
                m_scene.LandChannel = (ILandChannel)landChannel;
            }
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
