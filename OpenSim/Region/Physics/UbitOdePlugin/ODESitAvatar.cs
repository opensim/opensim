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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using OpenSim.Framework;
using OpenSim.Region.Physics.Manager;
using OdeAPI;
using log4net;
using OpenMetaverse;

namespace OpenSim.Region.Physics.OdePlugin
{
    /// <summary>
    /// </summary>
    public class ODESitAvatar
    {
        private OdeScene m_scene;
        private ODERayCastRequestManager m_raymanager;

        public ODESitAvatar(OdeScene pScene, ODERayCastRequestManager raymanager)
        {
            m_scene = pScene;
            m_raymanager = raymanager;
        }

        private static Vector3 SitAjust = new Vector3(0, 0, 0.4f);

        public void Sit(PhysicsActor actor, Vector3 avPos, Vector3 avCameraPosition, Vector3 offset, Vector3 avOffset, SitAvatarCallback PhysicsSitResponse)
        {
            if (!m_scene.haveActor(actor) || !(actor is OdePrim) || ((OdePrim)actor).prim_geom == IntPtr.Zero)
            {
                PhysicsSitResponse(-1, actor.LocalID, offset, Quaternion.Identity);
                return;
            }

            IntPtr geom = ((OdePrim)actor).prim_geom;

            d.Vector3 dtmp = d.GeomGetPosition(geom);
            Vector3 geopos;
            geopos.X = dtmp.X;
            geopos.Y = dtmp.Y;
            geopos.Z = dtmp.Z;


            d.AABB aabb;
            Quaternion ori;
            d.Quaternion qtmp;
            d.GeomCopyQuaternion(geom, out qtmp);
            Quaternion geomOri;
            geomOri.X = qtmp.X;
            geomOri.Y = qtmp.Y;
            geomOri.Z = qtmp.Z;
            geomOri.W = qtmp.W;
            Quaternion geomInvOri;
            geomInvOri.X = -qtmp.X;
            geomInvOri.Y = -qtmp.Y;
            geomInvOri.Z = -qtmp.Z;
            geomInvOri.W = qtmp.W;

            Vector3 target = geopos + offset;
            Vector3 rayDir = target - avCameraPosition;
            float raylen = rayDir.Length();
            float t = 1 / raylen;
            rayDir.X *= t;
            rayDir.Y *= t;
            rayDir.Z *= t;

            raylen += 30f; // focal point may be far
            List<ContactResult> rayResults;

            rayResults = m_scene.RaycastActor(actor, avCameraPosition, rayDir , raylen, 1);
            if (rayResults.Count == 0 || rayResults[0].ConsumerID != actor.LocalID)
            {
                d.GeomGetAABB(geom,out aabb);
                offset = new Vector3(avOffset.X, 0, aabb.MaxZ + avOffset.Z - geopos.Z);
                ori = geomInvOri;
                offset *= geomInvOri;

                PhysicsSitResponse(1, actor.LocalID, offset, ori);
                return;
            }

            offset = rayResults[0].Pos - geopos;
            double ang;
            float s;
            float c;

            d.GeomClassID geoclass = d.GeomGetClass(geom);

            if (geoclass == d.GeomClassID.SphereClass)
            {
                float r = d.GeomSphereGetRadius(geom);              

                offset.Normalize();
                offset *= r;

                ang = Math.Atan2(offset.Y, offset.X);
                ang *= 0.5d;
                s = (float)Math.Sin(ang);
                c = (float)Math.Cos(ang);

                ori = new Quaternion(0, 0, s, c);

                if (r < 0.4f)
                {
                    offset = new Vector3(0, 0, r);
                }
                else if (offset.Z < 0.4f)
                {
                    t = offset.Z;
                    float rsq = r * r;

                    t = 1.0f / (rsq - t * t);
                    offset.X *= t;
                    offset.Y *= t;
                    offset.Z = 0.4f;
                    t = rsq - 0.16f;
                    offset.X *= t;
                    offset.Y *= t;
                }

                offset += avOffset * ori;

                ori = geomInvOri * ori;
                offset *= geomInvOri;

                PhysicsSitResponse(1, actor.LocalID, offset, ori);
                return;
            }

            Vector3 norm = rayResults[0].Normal;

            if (norm.Z < 0)
            {
                PhysicsSitResponse(0, actor.LocalID, offset, Quaternion.Identity);
                return;
            }

            ang = Math.Atan2(-rayDir.Y, -rayDir.X);
            ang *= 0.5d;
            s = (float)Math.Sin(ang);
            c = (float)Math.Cos(ang);

            ori = new Quaternion(0, 0, s, c);

            offset += avOffset * ori;

            ori = geomInvOri * ori;
            offset *= geomInvOri;

            PhysicsSitResponse(1, actor.LocalID, offset, ori);
            return;
        }
    }
}