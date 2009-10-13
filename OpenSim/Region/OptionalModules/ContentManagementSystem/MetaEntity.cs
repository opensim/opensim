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
using System.Collections.Generic;
using System.Drawing;

using OpenMetaverse;

using Nini.Config;

using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.Framework.Scenes.Serialization;
using OpenSim.Region.Physics.Manager;

using log4net;

namespace OpenSim.Region.OptionalModules.ContentManagement
{
    public class MetaEntity
    {
        #region Constants

        public const float INVISIBLE = .95f;

        // Settings for transparency of metaentity
        public const float NONE = 0f;
        public const float TRANSLUCENT = .5f;

        #endregion Constants

        #region Static Fields

        //private static readonly ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        #endregion Static Fields

        #region Fields

        protected SceneObjectGroup m_Entity = null; // The scene object group that represents this meta entity.
        protected uint m_metaLocalid;

        #endregion Fields

        #region Constructors

        public MetaEntity()
        {
        }

        /// <summary>
        /// Makes a new meta entity by copying the given scene object group.
        /// The physics boolean is just a stub right now.
        /// </summary>
        public MetaEntity(SceneObjectGroup orig, bool physics)
        {
            m_Entity = orig.Copy(orig.RootPart.OwnerID, orig.RootPart.GroupID, false);
            Initialize(physics);
        }

        /// <summary>
        /// Takes an XML description of a scene object group and converts it to a meta entity.
        /// </summary>
        public MetaEntity(string objectXML, Scene scene, bool physics)
        {
            m_Entity = SceneObjectSerializer.FromXml2Format(objectXML);
            m_Entity.SetScene(scene);
            Initialize(physics);
        }

        #endregion Constructors

        #region Public Properties

        public Dictionary<UUID, SceneObjectPart> Children
        {
            get { return m_Entity.Children; }
            set { m_Entity.Children = value; }
        }

        public uint LocalId
        {
            get { return m_Entity.LocalId; }
            set { m_Entity.LocalId = value; }
        }

        public SceneObjectGroup ObjectGroup
        {
            get { return m_Entity; }
        }

        public int PrimCount
        {
            get { return m_Entity.PrimCount; }
        }

        public SceneObjectPart RootPart
        {
            get { return m_Entity.RootPart; }
        }

        public Scene Scene
        {
            get { return m_Entity.Scene; }
        }

        public UUID UUID
        {
            get { return m_Entity.UUID; }
            set { m_Entity.UUID = value; }
        }

        #endregion Public Properties

        #region Protected Methods

        // The metaentity objectgroup must have unique localids as well as unique uuids.
        // localids are used by the client to refer to parts.
        // uuids are sent to the client and back to the server to identify parts on the server side.
        /// <summary>
        /// Changes localids and uuids of m_Entity.
        /// </summary>
        protected void Initialize(bool physics)
        {
            //make new uuids
            Dictionary<UUID, SceneObjectPart> parts = new Dictionary<UUID, SceneObjectPart>();
            foreach (SceneObjectPart part in m_Entity.Children.Values)
            {
                part.ResetIDs(part.LinkNum);
                parts.Add(part.UUID, part);
            }

            //finalize
            m_Entity.RootPart.PhysActor = null;
            m_Entity.Children = parts;
        }

        #endregion Protected Methods

        #region Public Methods

        /// <summary>
        /// Hides the metaentity from a single client.
        /// </summary>
        public virtual void Hide(IClientAPI client)
        {
            //This deletes the group without removing from any databases.
            //This is important because we are not IN any database.
            //m_Entity.FakeDeleteGroup();
            foreach (SceneObjectPart part in m_Entity.Children.Values)
                client.SendKillObject(m_Entity.RegionHandle, part.LocalId);
        }

        /// <summary>
        /// Sends a kill object message to all clients, effectively "hiding" the metaentity even though it's still on the server.
        /// </summary>
        public virtual void HideFromAll()
        {
            foreach (SceneObjectPart part in m_Entity.Children.Values)
                m_Entity.Scene.ClientManager.ForEach(
                    delegate(IClientAPI controller)
                    { controller.SendKillObject(m_Entity.RegionHandle, part.LocalId); }
                );
        }

        public void SendFullUpdate(IClientAPI client)
        {
            // Not sure what clientFlags should be but 0 seems to work
            SendFullUpdate(client, 0);
        }

        public void SendFullUpdate(IClientAPI client, uint clientFlags)
        {
            m_Entity.SendFullUpdateToClient(client);
        }

        public void SendFullUpdateToAll()
        {
            m_Entity.Scene.ClientManager.ForEach(
                delegate(IClientAPI controller)
                { m_Entity.SendFullUpdateToClient(controller); }
            );
        }

        /// <summary>
        /// Makes a single SceneObjectPart see through.
        /// </summary>
        /// <param name="part">
        /// A <see cref="SceneObjectPart"/>
        /// The part to make see through
        /// </param>
        /// <param name="transparencyAmount">
        /// A <see cref="System.Single"/>
        /// The degree of transparency to imbue the part with, 0f being solid and .95f being invisible.
        /// </param>
        public static void SetPartTransparency(SceneObjectPart part, float transparencyAmount)
        {
            Primitive.TextureEntry tex = null;
            Color4 texcolor;
            try
            {
                tex = part.Shape.Textures;
                texcolor = new Color4();
            }
            catch(Exception)
            {
                //m_log.ErrorFormat("[Content Management]: Exception thrown while accessing textures of scene object: " + e);
                return;
            }

            for (uint i = 0; i < tex.FaceTextures.Length; i++)
            {
                try {
                    if (tex.FaceTextures[i] != null)
                    {
                        texcolor = tex.FaceTextures[i].RGBA;
                        texcolor.A = transparencyAmount;
                        tex.FaceTextures[i].RGBA = texcolor;
                    }
                }
                catch (Exception)
                {
                    //m_log.ErrorFormat("[Content Management]: Exception thrown while accessing different face textures of object: " + e);
                    continue;
                }
            }
            try {
                texcolor = tex.DefaultTexture.RGBA;
                texcolor.A = transparencyAmount;
                tex.DefaultTexture.RGBA = texcolor;
                part.Shape.TextureEntry = tex.GetBytes();
            }
            catch (Exception)
            {
                //m_log.Info("[Content Management]: Exception thrown while accessing default face texture of object: " + e);
            }
        }

        #endregion Public Methods
    }
}
