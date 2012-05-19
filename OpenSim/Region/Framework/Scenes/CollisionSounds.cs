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
// Ubit 2012

using System;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Scenes
{
    public struct CollisionForSoundInfo
    {
        public uint colliderID;
        public Vector3 position;
        public float relativeVel;
    }

    public static class CollisionSounds
    {

        

    // defines for cases
    // only know one UUID for now (woodflesh)

        private const int MaxMaterials = 7;
        // part part
        private static UUID snd_StoneStone = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_StoneMetal = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_StoneGlass = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_StoneWood  = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_StoneFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_StonePlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_StoneRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_MetalMetal = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_MetalGlass = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_MetalWood  = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_MetalFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_MetalPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_MetalRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_GlassGlass = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_GlassWood  = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_GlassFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_GlassPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_GlassRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_WoodWood = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_WoodFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_WoodPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_WoodRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_FleshFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_FleshPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_FleshRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_PlasticPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID  snd_PlasticRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_RubberRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        // terrain part
        private static UUID snd_TerrainStone = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_TerrainMetal = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_TerrainGlass = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_TerrainWood = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_TerrainFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_TerrainPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_TerrainRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        public static UUID[] m_TerrainPart = {
            snd_TerrainStone,
            snd_TerrainMetal,
            snd_TerrainGlass,
            snd_TerrainWood,
            snd_TerrainFlesh,
            snd_TerrainPlastic,
            snd_TerrainRubber
            };

        public static UUID[] m_PartPart = {
            snd_StoneStone, snd_StoneMetal, snd_StoneGlass, snd_StoneWood, snd_StoneFlesh, snd_StonePlastic, snd_StoneRubber,
            snd_StoneMetal, snd_MetalMetal, snd_MetalGlass, snd_MetalWood, snd_MetalFlesh, snd_MetalPlastic, snd_MetalRubber,
            snd_StoneGlass, snd_MetalGlass, snd_GlassGlass, snd_GlassWood, snd_GlassFlesh, snd_GlassPlastic, snd_GlassRubber,
            snd_StoneWood, snd_MetalWood, snd_GlassWood, snd_WoodWood, snd_WoodFlesh, snd_WoodPlastic, snd_WoodRubber,
            snd_StoneFlesh, snd_MetalFlesh, snd_GlassFlesh, snd_WoodFlesh, snd_FleshFlesh, snd_FleshPlastic, snd_FleshRubber,
            snd_StonePlastic, snd_MetalPlastic, snd_GlassPlastic, snd_WoodPlastic, snd_FleshPlastic, snd_PlasticPlastic, snd_PlasticRubber,
            snd_StoneRubber, snd_MetalRubber, snd_GlassRubber, snd_WoodRubber, snd_FleshRubber, snd_PlasticRubber, snd_RubberRubber
            };

        public static void PartCollisionSound(SceneObjectPart part, List<CollisionForSoundInfo> collidersinfolist)
        {
            if (collidersinfolist.Count == 0 || part == null)
                return;

            if (part.VolumeDetectActive || (part.Flags & PrimFlags.Physics) == 0)
                return;

            if (part.ParentGroup == null)
                return;

            UUID soundID = part.CollisionSound;
            if (soundID == part.invalidCollisionSoundUUID)
                return;

            float volume = 0.0f;
            int otherMaterial;
            bool HaveSound = false;

            if (soundID != UUID.Zero)
            {
                volume = part.CollisionSoundVolume;
                if (volume == 0.0f)
                    return;
                HaveSound = true;
            }

            int thisMaterial = (int) part.Material;
            if (thisMaterial >= MaxMaterials)
                thisMaterial = 3;

            int thisMatScaled = thisMaterial * MaxMaterials;
            int index;

            bool doneownsound = false;

            CollisionForSoundInfo colInfo;
            uint id;

            for(int i = 0; i< collidersinfolist.Count; i++)
            {
                colInfo = collidersinfolist[i];

                if (!HaveSound)
                {
                    volume = Math.Abs(colInfo.relativeVel);
                    if (volume < 0.2f)
                        continue;
                }

                id = colInfo.colliderID;
                if (id == 0)
                    {
                        if (!doneownsound)
                        {
                            if (!HaveSound)
                            {
                                volume *= volume * .0625f; // 4m/s == full volume
                                if (volume > 1.0f)
                                    volume = 1.0f;
                                soundID = m_TerrainPart[thisMaterial];
                            }
                            part.SendCollisionSound(soundID, volume, colInfo.position);
                            doneownsound = true;
                        }
                        continue;
                    }

                SceneObjectPart otherPart = part.ParentGroup.Scene.GetSceneObjectPart(id);
                if (otherPart != null)
                {
                    if (otherPart.CollisionSound == part.invalidCollisionSoundUUID || otherPart.VolumeDetectActive)
                        continue;

                    if (!HaveSound)
                    {
                        if (otherPart.CollisionSound != UUID.Zero)
                        {
                            soundID = otherPart.CollisionSound;
                            volume = otherPart.CollisionSoundVolume;
                            if (volume == 0.0f)
                                continue;
                        }
                        else
                        {
                            volume *= volume * .0625f; // 4m/s == full volume
                            if (volume > 1.0f)
                                volume = 1.0f;
                            otherMaterial = (int)otherPart.Material;
                            if (otherMaterial >= MaxMaterials)
                                otherMaterial = 3;
                            index = thisMatScaled + otherMaterial;
                            soundID = m_PartPart[index];
                        }
                    }

                    if (doneownsound)
                        otherPart.SendCollisionSound(soundID, volume, colInfo.position);
                    else
                    {
                        part.SendCollisionSound(soundID, volume, colInfo.position);
                        doneownsound = true;
                    }
                }
            }
        }

        public static void AvatarCollisionSound(ScenePresence av, List<CollisionForSoundInfo> collidersinfolist)
        {
            // temporary mute sounds
//            return;

            if (collidersinfolist.Count == 0 || av == null)
                return;

            UUID soundID;
            int otherMaterial;

            int thisMaterial = 3;

            int thisMatScaled = thisMaterial * MaxMaterials;
            int index;
//            bool doneownsound = false;

            CollisionForSoundInfo colInfo;
            uint id;
            float volume;

            for(int i = 0; i< collidersinfolist.Count; i++)
            {
                colInfo = collidersinfolist[i];

                volume = Math.Abs(colInfo.relativeVel);
                if(volume < 0.2f)
                    continue;

                id = colInfo.colliderID;

                if (id == 0) // no terrain collision sounds for now
                {
                    continue;
                }

                SceneObjectPart otherPart = av.Scene.GetSceneObjectPart(id);
                if (otherPart != null)
                {
                    if (otherPart.CollisionSound == otherPart.invalidCollisionSoundUUID)
                        continue;
                    if (otherPart.CollisionSound != UUID.Zero)
                        otherPart.SendCollisionSound(otherPart.CollisionSound, otherPart.CollisionSoundVolume, colInfo.position);
                    else
                    {
                        volume *= volume * .0625f; // 4m/s == full volume
                        if (volume > 1.0f)
                            volume = 1.0f;
                        otherMaterial = (int)otherPart.Material;
                        if (otherMaterial >= MaxMaterials)
                            otherMaterial = 3;
                        index = thisMatScaled + otherMaterial;
                        soundID = m_PartPart[index];
                        otherPart.SendCollisionSound(soundID, volume, colInfo.position);
                    }
                }
/*
                else if (!doneownsound)
                {
                    ScenePresence otherav = av.Scene.GetScenePresence(Id);
                    if (otherav != null && (!otherav.IsChildAgent))
                    {
                        soundID = snd_FleshFlesh;
                        av.SendCollisionSound(soundID, 1.0);
                        doneownsound = true;
                    }
                }
 */
            }
        }

    }
}