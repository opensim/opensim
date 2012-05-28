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
        private const int MaxMaterials = 7;
        // part part
/*
        private static UUID snd_StoneStone = new UUID("be7295c0-a158-11e1-b3dd-0800200c9a66");
        private static UUID snd_StoneMetal = new UUID("be7295c0-a158-11e1-b3dd-0800201c9a66");
        private static UUID snd_StoneGlass = new UUID("be7295c0-a158-11e1-b3dd-0800202c9a66");
        private static UUID snd_StoneWood = new UUID("be7295c0-a158-11e1-b3dd-0800203c9a66");
        private static UUID snd_StoneFlesh = new UUID("be7295c0-a158-11e1-b3dd-0800204c9a66");
        private static UUID snd_StonePlastic = new UUID("be7295c0-a158-11e1-b3dd-0800205c9a66");
        private static UUID snd_StoneRubber = new UUID("be7295c0-a158-11e1-b3dd-0800206c9a66");

        private static UUID snd_MetalStone = new UUID("be7295c0-a158-11e1-b3dd-0801200c9a66");
        private static UUID snd_MetalMetal = new UUID("be7295c0-a158-11e1-b3dd-0801201c9a66");
        private static UUID snd_MetalGlass = new UUID("be7295c0-a158-11e1-b3dd-0801202c9a66");
        private static UUID snd_MetalWood  = new UUID("be7295c0-a158-11e1-b3dd-0801203c9a66");
        private static UUID snd_MetalFlesh = new UUID("be7295c0-a158-11e1-b3dd-0801204c9a66");
        private static UUID snd_MetalPlastic = new UUID("be7295c0-a158-11e1-b3dd-0801205c9a66");
        private static UUID snd_MetalRubber = new UUID("be7295c0-a158-11e1-b3dd-0801206c9a66");

        private static UUID snd_GlassStone = new UUID("be7295c0-a158-11e1-b3dd-0802200c9a66");
        private static UUID snd_GlassMetal = new UUID("be7295c0-a158-11e1-b3dd-0802201c9a66");
        private static UUID snd_GlassGlass = new UUID("be7295c0-a158-11e1-b3dd-0802202c9a66");
        private static UUID snd_GlassWood = new UUID("be7295c0-a158-11e1-b3dd-0802203c9a66");
        private static UUID snd_GlassFlesh = new UUID("be7295c0-a158-11e1-b3dd-0802204c9a66");
        private static UUID snd_GlassPlastic = new UUID("be7295c0-a158-11e1-b3dd-0802205c9a66");
        private static UUID snd_GlassRubber = new UUID("be7295c0-a158-11e1-b3dd-0802206c9a66");

        private static UUID snd_WoodStone = new UUID("be7295c0-a158-11e1-b3dd-0803200c9a66");
        private static UUID snd_WoodMetal = new UUID("be7295c0-a158-11e1-b3dd-0803201c9a66");
        private static UUID snd_WoodGlass = new UUID("be7295c0-a158-11e1-b3dd-0803202c9a66");
        private static UUID snd_WoodWood = new UUID("be7295c0-a158-11e1-b3dd-0803203c9a66");
        private static UUID snd_WoodFlesh = new UUID("be7295c0-a158-11e1-b3dd-0803204c9a66");
        private static UUID snd_WoodPlastic = new UUID("be7295c0-a158-11e1-b3dd-0803205c9a66");
        private static UUID snd_WoodRubber = new UUID("be7295c0-a158-11e1-b3dd-0803206c9a66");

        private static UUID snd_FleshStone = new UUID("be7295c0-a158-11e1-b3dd-0804200c9a66");
        private static UUID snd_FleshMetal = new UUID("be7295c0-a158-11e1-b3dd-0804201c9a66");
        private static UUID snd_FleshGlass = new UUID("be7295c0-a158-11e1-b3dd-0804202c9a66");
        private static UUID snd_FleshWood = new UUID("be7295c0-a158-11e1-b3dd-0804203c9a66");
        private static UUID snd_FleshFlesh = new UUID("be7295c0-a158-11e1-b3dd-0804204c9a66");
        private static UUID snd_FleshPlastic = new UUID("be7295c0-a158-11e1-b3dd-0804205c9a66");
        private static UUID snd_FleshRubber = new UUID("be7295c0-a158-11e1-b3dd-0804206c9a66");

        private static UUID snd_PlasticStone = new UUID("be7295c0-a158-11e1-b3dd-0805200c9a66");
        private static UUID snd_PlasticMetal = new UUID("be7295c0-a158-11e1-b3dd-0805201c9a66");
        private static UUID snd_PlasticGlass = new UUID("be7295c0-a158-11e1-b3dd-0805202c9a66");
        private static UUID snd_PlasticWood = new UUID("be7295c0-a158-11e1-b3dd-0805203c9a66");
        private static UUID snd_PlasticFlesh = new UUID("be7295c0-a158-11e1-b3dd-0805204c9a66");
        private static UUID snd_PlasticPlastic = new UUID("be7295c0-a158-11e1-b3dd-0805205c9a66");
        private static UUID snd_PlasticRubber = new UUID("be7295c0-a158-11e1-b3dd-0805206c9a66");

        private static UUID snd_RubberStone = new UUID("be7295c0-a158-11e1-b3dd-0806200c9a66");
        private static UUID snd_RubberMetal = new UUID("be7295c0-a158-11e1-b3dd-0806201c9a66");
        private static UUID snd_RubberGlass = new UUID("be7295c0-a158-11e1-b3dd-0806202c9a66");
        private static UUID snd_RubberWood = new UUID("be7295c0-a158-11e1-b3dd-0806203c9a66");
        private static UUID snd_RubberFlesh = new UUID("be7295c0-a158-11e1-b3dd-0806204c9a66");
        private static UUID snd_RubberPlastic = new UUID("be7295c0-a158-11e1-b3dd-0806205c9a66");
        private static UUID snd_RubberRubber = new UUID("be7295c0-a158-11e1-b3dd-0806206c9a66");

        // terrain part
        private static UUID snd_TerrainStone = new UUID("be7295c0-a158-11e1-b3dd-0807200c9a66");
        private static UUID snd_TerrainMetal = new UUID("be7295c0-a158-11e1-b3dd-0807200c9a66");
        private static UUID snd_TerrainGlass = new UUID("be7295c0-a158-11e1-b3dd-0807200c9a66");
        private static UUID snd_TerrainWood = new UUID("be7295c0-a158-11e1-b3dd-0807200c9a66");
        private static UUID snd_TerrainFlesh = new UUID("be7295c0-a158-11e1-b3dd-0807200c9a66");
        private static UUID snd_TerrainPlastic = new UUID("be7295c0-a158-11e1-b3dd-0807200c9a66");
        private static UUID snd_TerrainRubber = new UUID("be7295c0-a158-11e1-b3dd-0807200c9a66");
*/
        private static UUID snd_StoneStone = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_StoneMetal = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_StoneGlass = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_StoneWood = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_StoneFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_StonePlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_StoneRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_MetalMetal = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_MetalGlass = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_MetalWood = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_MetalFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_MetalPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_MetalRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_GlassGlass = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_GlassWood = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_GlassFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_GlassPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_GlassRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_WoodWood = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_WoodFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_WoodPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_WoodRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_FleshFlesh = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_FleshPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_FleshRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

        private static UUID snd_PlasticPlastic = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");
        private static UUID snd_PlasticRubber = new UUID("c80260ba-41fd-8a46-768a-6bf236360e3a");

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
/*      
        //full assimetric sounds
        public static UUID[] m_PartPart = {
            snd_StoneStone, snd_StoneMetal, snd_StoneGlass, snd_StoneWood, snd_StoneFlesh, snd_StonePlastic, snd_StoneRubber,
            snd_MetalStone, snd_MetalMetal, snd_MetalGlass, snd_MetalWood, snd_MetalFlesh, snd_MetalPlastic, snd_MetalRubber,
            snd_GlassStone, snd_GlassMetal, snd_GlassGlass, snd_GlassWood, snd_GlassFlesh, snd_GlassPlastic, snd_GlassRubber,
            snd_WoodStone, snd_WoodMetal, snd_WoodGlass, snd_WoodWood, snd_WoodFlesh, snd_WoodPlastic, snd_WoodRubber,
            snd_FleshStone, snd_FleshMetal, snd_FleshGlass, snd_FleshWood, snd_FleshFlesh, snd_FleshPlastic, snd_FleshRubber,
            snd_PlasticStone, snd_PlasticMetal, snd_PlasticGlass, snd_PlasticWood, snd_PlasticFlesh, snd_PlasticPlastic, snd_PlasticRubber,
            snd_RubberStone, snd_RubberMetal, snd_RubberGlass, snd_RubberWood, snd_RubberFlesh, snd_RubberPlastic, snd_RubberRubber
            };
*/
        // simetric sounds
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
            // disable for now
            return;

            if (collidersinfolist.Count == 0 || part == null)
                return;

            if (part.VolumeDetectActive || (part.Flags & PrimFlags.Physics) == 0)
                return;

            if (part.ParentGroup == null)
                return;

            if (part.CollisionSoundType < 0)
                return;

            float volume = 0.0f;
            bool HaveSound = false;

            UUID soundID = part.CollisionSound;

            if (part.CollisionSoundType > 0)
            {
                //                soundID = part.CollisionSound;
                volume = part.CollisionSoundVolume;
                if (volume == 0.0f)
                    return;
                HaveSound = true;
            }

            bool doneownsound = false;

            int thisMaterial = (int)part.Material;
            if (thisMaterial >= MaxMaterials)
                thisMaterial = 3;
            int thisMatScaled = thisMaterial * MaxMaterials;

            CollisionForSoundInfo colInfo;
            uint id;

            for(int i = 0; i< collidersinfolist.Count; i++)
            {
                colInfo = collidersinfolist[i];

                id = colInfo.colliderID;
                if (id == 0) // terrain collision
                    {
                        if (!doneownsound)
                        {
                            if (!HaveSound)
                            {
                                volume = Math.Abs(colInfo.relativeVel);
                                if (volume < 0.2f)
                                    continue;

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
                    if (otherPart.CollisionSoundType < 0 || otherPart.VolumeDetectActive)
                        continue;

                    if (!HaveSound)
                    {
                        if (otherPart.CollisionSoundType > 0)
                        {
                            soundID = otherPart.CollisionSound;
                            volume = otherPart.CollisionSoundVolume;
                            if (volume == 0.0f)
                                continue;
                        }
                        else
                        {
                            volume = Math.Abs(colInfo.relativeVel);
                            if (volume < 0.2f)
                                continue;

                            volume *= volume * .0625f; // 4m/s == full volume
                            if (volume > 1.0f)
                                volume = 1.0f;

                            int otherMaterial = (int)otherPart.Material;
                            if (otherMaterial >= MaxMaterials)
                                otherMaterial = 3;

                            soundID = m_PartPart[thisMatScaled + otherMaterial];
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
             // disable for now
             return;

            if (collidersinfolist.Count == 0 || av == null)
                return;

            UUID soundID;
            int otherMaterial;

            int thisMaterial = 4; // flesh

            int thisMatScaled = thisMaterial * MaxMaterials;

            //            bool doneownsound = false;

            CollisionForSoundInfo colInfo;
            uint id;
            float volume;

            for(int i = 0; i< collidersinfolist.Count; i++)
            {
                colInfo = collidersinfolist[i];

                id = colInfo.colliderID;

                if (id == 0) // no terrain collision sounds for now
                {
                    continue;
//                    volume = Math.Abs(colInfo.relativeVel);
//                    if (volume < 0.2f)
//                        continue;

                }

                SceneObjectPart otherPart = av.Scene.GetSceneObjectPart(id);
                if (otherPart != null)
                {
                    if (otherPart.CollisionSoundType < 0)
                        continue;
                    if (otherPart.CollisionSoundType > 0 && otherPart.CollisionSoundVolume > 0f)
                        otherPart.SendCollisionSound(otherPart.CollisionSound, otherPart.CollisionSoundVolume, colInfo.position);
                    else
                    {
                        volume = Math.Abs(colInfo.relativeVel);
                        if (volume < 0.2f)
                            continue;

                        volume *= volume * .0625f; // 4m/s == full volume
                        if (volume > 1.0f)
                            volume = 1.0f;
                        otherMaterial = (int)otherPart.Material;
                        if (otherMaterial >= MaxMaterials)
                            otherMaterial = 3;

                        soundID = m_PartPart[thisMatScaled + otherMaterial];
                        otherPart.SendCollisionSound(soundID, volume, colInfo.position);
                    }
                    continue;
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