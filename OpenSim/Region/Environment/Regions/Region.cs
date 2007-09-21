using System.Collections.Generic;
using libsecondlife;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Terrain;
using OpenSim.Framework.Interfaces;
using System;

namespace OpenSim.Region.Environment.Regions
{
    public class Region
    {
        // This is a temporary (and real ugly) construct to emulate us really having a separate list
        // of region subscribers. It should be removed ASAP, like.

        private readonly Scene m_scene;
        private Dictionary<LLUUID, RegionSubscription> m_regionSubscriptions
        {
            get
            {
                Dictionary<LLUUID, RegionSubscription> subscriptions = new Dictionary<LLUUID, RegionSubscription>( );

                foreach( ScenePresence presence in m_scene.GetScenePresences() )
                {
                    subscriptions.Add( presence.UUID, new RegionSubscription( presence.ControllingClient ));
                }

                return subscriptions;
            }
        }

        public Region( Scene scene )
        {
            m_scene = scene; // The Scene reference should be removed.
        }

        internal void Broadcast( Action<IClientAPI> whatToDo )
        {
            foreach (RegionSubscription subscription in m_regionSubscriptions.Values )
            {
                whatToDo(subscription.Client);                
            }
        }

        internal void Remove(LLUUID agentID)
        {
            // TODO : Well, remove it!
        }
    }
}
