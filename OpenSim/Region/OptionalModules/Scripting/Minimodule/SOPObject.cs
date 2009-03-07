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
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{
    class SOPObject : IObject
    {
        private readonly Scene m_rootScene;
        private readonly uint m_localID;

        public SOPObject(Scene rootScene, uint localID)
        {
            m_rootScene = rootScene;
            m_localID = localID;
        }

        /// <summary>
        /// This needs to run very, very quickly.
        /// It is utilized in nearly every property and method.
        /// </summary>
        /// <returns></returns>
        private SceneObjectPart GetSOP()
        {
            if (m_rootScene.Entities.ContainsKey(m_localID))
                return ((SceneObjectGroup) m_rootScene.Entities[m_localID]).RootPart;

            return null;
        }

        public bool Exists
        {
            get { return GetSOP() != null; }
        }

        public uint LocalID
        {
            get { return m_localID; }
        }

        public UUID GlobalID
        {
            get { return GetSOP().UUID; }
        }

        public IObject[] Children
        {
            get
            {
                SceneObjectPart my = GetSOP();
                int total = my.ParentGroup.Children.Count;

                IObject[] rets = new IObject[total];

                int i = 0;
                foreach (KeyValuePair<UUID, SceneObjectPart> pair in my.ParentGroup.Children)
                {
                    rets[i++] = new SOPObject(m_rootScene, pair.Value.LocalId);
                }

                return rets;
            }
        }

        public IObject Root
        {
            get { return new SOPObject(m_rootScene, GetSOP().ParentGroup.RootPart.LocalId); }
        }

        public IObjectFace[] Faces
        {
            get
            {
                SceneObjectPart sop = GetSOP();
                IObjectFace[] rets = new IObjectFace[getNumberOfSides(sop)];

                for (int i = 0; i < rets.Length;i++ )
                {
                    //rets[i] = new ObjectFace 
                }

                return rets;
            }
        }

        public Vector3 Scale
        {
            get { return GetSOP().Scale; }
            set { GetSOP().Scale = value; }
        }

        public Quaternion Rotation
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public Vector3 SitTarget
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public string SitTargetText
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public string TouchText
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public string Text
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsPhysical
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsPhantom
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsRotationLockedX
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsRotationLockedY
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsRotationLockedZ
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsSandboxed
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsImmotile
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsAlwaysReturned
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsTemporary
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public bool IsFlexible
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }

        public PrimType PrimShape
        {
            get { return (PrimType) getScriptPrimType(GetSOP().Shape); }
            set { throw new System.NotImplementedException(); }
        }

        public Material Material
        {
            get { throw new System.NotImplementedException(); }
            set { throw new System.NotImplementedException(); }
        }


        #region Supporting Functions

        // Helper functions to understand if object has cut, hollow, dimple, and other affecting number of faces
        private static void hasCutHollowDimpleProfileCut(int primType, PrimitiveBaseShape shape, out bool hasCut, out bool hasHollow,
            out bool hasDimple, out bool hasProfileCut)
        {
            if (primType == (int)PrimType.Box
                ||
                primType == (int)PrimType.Cylinder
                ||
                primType == (int)PrimType.Prism)

                hasCut = (shape.ProfileBegin > 0) || (shape.ProfileEnd > 0);
            else
                hasCut = (shape.PathBegin > 0) || (shape.PathEnd > 0);

            hasHollow = shape.ProfileHollow > 0;
            hasDimple = (shape.ProfileBegin > 0) || (shape.ProfileEnd > 0); // taken from llSetPrimitiveParms
            hasProfileCut = hasDimple; // is it the same thing?

        }

        private static int getScriptPrimType(PrimitiveBaseShape primShape)
        {
            if (primShape.SculptEntry)
                return (int) PrimType.Sculpt;
            if ((primShape.ProfileCurve & 0x07) == (byte) ProfileShape.Square)
            {
                if (primShape.PathCurve == (byte) Extrusion.Straight)
                    return (int) PrimType.Box;
                if (primShape.PathCurve == (byte) Extrusion.Curve1)
                    return (int) PrimType.Tube;
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte) ProfileShape.Circle)
            {
                if (primShape.PathCurve == (byte) Extrusion.Straight)
                    return (int) PrimType.Cylinder;
                if (primShape.PathCurve == (byte) Extrusion.Curve1)
                    return (int) PrimType.Torus;
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte) ProfileShape.HalfCircle)
            {
                if (primShape.PathCurve == (byte) Extrusion.Curve1 || primShape.PathCurve == (byte) Extrusion.Curve2)
                    return (int) PrimType.Sphere;
            }
            else if ((primShape.ProfileCurve & 0x07) == (byte) ProfileShape.EquilateralTriangle)
            {
                if (primShape.PathCurve == (byte) Extrusion.Straight)
                    return (int) PrimType.Prism;
                if (primShape.PathCurve == (byte) Extrusion.Curve1)
                    return (int) PrimType.Ring;
            }
            return (int) PrimType.NotPrimitive;
        }

        private static int getNumberOfSides(SceneObjectPart part)
        {
            int ret;
            bool hasCut;
            bool hasHollow;
            bool hasDimple;
            bool hasProfileCut;

            int primType = getScriptPrimType(part.Shape);
            hasCutHollowDimpleProfileCut(primType, part.Shape, out hasCut, out hasHollow, out hasDimple, out hasProfileCut);

            switch (primType)
            {
                default:
                case (int) PrimType.Box:
                    ret = 6;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Cylinder:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Prism:
                    ret = 5;
                    if (hasCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Sphere:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasDimple) ret += 2;
                    if (hasHollow)
                        ret += 1; // GOTCHA: LSL shows 2 additional sides here. 
                                  // This has been fixed, but may cause porting issues.
                    break;
                case (int) PrimType.Torus:
                    ret = 1;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Tube:
                    ret = 4;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Ring:
                    ret = 3;
                    if (hasCut) ret += 2;
                    if (hasProfileCut) ret += 2;
                    if (hasHollow) ret += 1;
                    break;
                case (int) PrimType.Sculpt:
                    ret = 1;
                    break;
            }
            return ret;
        }


        #endregion

    }
}
