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
using System.Net;
using System.Reflection;
using log4net;
using Nini.Config;
using OpenMetaverse;
using OpenMetaverse.StructuredData;
using Mono.Addins;
using OpenSim.Framework;
using OpenSim.Framework.Servers.HttpServer;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using Caps=OpenSim.Framework.Capabilities.Caps;

namespace OpenSim.Region.ClientStack.Linden
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "ObjectAdd")]
    public class ObjectAdd : INonSharedRegionModule
    {
        // private static readonly ILog m_log =
        //     LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private Scene m_scene;

        #region INonSharedRegionModule Members
        public void Initialise(IConfigSource pSource)
        {
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            m_scene.EventManager.OnRegisterCaps += RegisterCaps;
        }

        public void RemoveRegion(Scene scene)
        {
            if (m_scene == scene)
            {
                m_scene.EventManager.OnRegisterCaps -= RegisterCaps;
                m_scene = null;
            }
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "ObjectAddModule"; }
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        #endregion

        public void RegisterCaps(UUID agentID, Caps caps)
        {
            // m_log.InfoFormat("[OBJECTADD]: {0}", "/CAPS/OA/" + capuuid + "/");

            caps.RegisterSimpleHandler("ObjectAdd", new SimpleOSDMapHandler("POST", "/" + UUID.Random(),
                delegate (IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map)
                {
                    ProcessAdd(httpRequest, httpResponse, map, agentID);
                }));
        }

        public void ProcessAdd(IOSHttpRequest httpRequest, IOSHttpResponse httpResponse, OSDMap map, UUID avatarID)
        {
            httpResponse.KeepAlive = false;
            if(!m_scene.TryGetScenePresence(avatarID, out ScenePresence sp))
            {
                httpResponse.StatusCode = (int)HttpStatusCode.Gone;
                return;
            }

            //UUID session_id = UUID.Zero;
            bool bypass_raycast = false;
            uint everyone_mask = 0;
            uint group_mask = 0;
            uint next_owner_mask = 0;
            uint flags = 0;
            UUID group_id = UUID.Zero;
            int hollow = 0;
            int material = 0;
            int p_code = 0;
            int path_begin = 0;
            int path_curve = 0;
            int path_end = 0;
            int path_radius_offset = 0;
            int path_revolutions = 0;
            int path_scale_x = 0;
            int path_scale_y = 0;
            int path_shear_x = 0;
            int path_shear_y = 0;
            int path_skew = 0;
            int path_taper_x = 0;
            int path_taper_y = 0;
            int path_twist = 0;
            int path_twist_begin = 0;
            int profile_begin = 0;
            int profile_curve = 0;
            int profile_end = 0;
            Vector3 ray_end = Vector3.Zero;
            bool ray_end_is_intersection = false;
            Vector3 ray_start = Vector3.Zero;
            UUID ray_target_id = UUID.Zero;
            Quaternion rotation = Quaternion.Identity;
            Vector3 scale = Vector3.Zero;
            int state = 0;
            int lastattach = 0;

            OSD tmpOSD;
            if (map.TryGetValue("ObjectData", out tmpOSD)) //v2
            {
                if (tmpOSD.Type != OSDType.Map)
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                OSDMap ObjMap = (OSDMap)tmpOSD;

                bypass_raycast = ObjMap["BypassRaycast"].AsBoolean();
                everyone_mask = ReadUIntVal(ObjMap["EveryoneMask"]);
                flags = ReadUIntVal(ObjMap["Flags"]);
                group_mask = ReadUIntVal(ObjMap["GroupMask"]);
                material = ObjMap["Material"].AsInteger();
                next_owner_mask = ReadUIntVal(ObjMap["NextOwnerMask"]);
                p_code = ObjMap["PCode"].AsInteger();

                if (ObjMap.TryGetValue("Path", out tmpOSD))
                {
                    if (tmpOSD.Type != OSDType.Map)
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }

                    OSDMap PathMap = (OSDMap)tmpOSD;
                    path_begin = PathMap["Begin"].AsInteger();
                    path_curve = PathMap["Curve"].AsInteger();
                    path_end = PathMap["End"].AsInteger();
                    path_radius_offset = PathMap["RadiusOffset"].AsInteger();
                    path_revolutions = PathMap["Revolutions"].AsInteger();
                    path_scale_x = PathMap["ScaleX"].AsInteger();
                    path_scale_y = PathMap["ScaleY"].AsInteger();
                    path_shear_x = PathMap["ShearX"].AsInteger();
                    path_shear_y = PathMap["ShearY"].AsInteger();
                    path_skew = PathMap["Skew"].AsInteger();
                    path_taper_x = PathMap["TaperX"].AsInteger();
                    path_taper_y = PathMap["TaperY"].AsInteger();
                    path_twist = PathMap["Twist"].AsInteger();
                    path_twist_begin = PathMap["TwistBegin"].AsInteger();
                }

                if (ObjMap.TryGetValue("Profile", out tmpOSD))
                {
                    if (tmpOSD.Type != OSDType.Map)
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }

                    OSDMap ProfileMap = (OSDMap)tmpOSD;

                    profile_begin = ProfileMap["Begin"].AsInteger();
                    profile_curve = ProfileMap["Curve"].AsInteger();
                    profile_end = ProfileMap["End"].AsInteger();
                    hollow = ProfileMap["Hollow"].AsInteger();
                }
                ray_end_is_intersection = ObjMap["RayEndIsIntersection"].AsBoolean();

                ray_target_id = ObjMap["RayTargetId"].AsUUID();
                state = ObjMap["State"].AsInteger();
                lastattach = ObjMap["LastAttachPoint"].AsInteger();
                try
                {
                    ray_end = ((OSDArray)ObjMap["RayEnd"]).AsVector3();
                    ray_start = ((OSDArray)ObjMap["RayStart"]).AsVector3();
                    scale = ((OSDArray)ObjMap["Scale"]).AsVector3();
                    rotation = ((OSDArray)ObjMap["Rotation"]).AsQuaternion();
                }
                catch (Exception)
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                if (map.TryGetValue("AgentData", out tmpOSD))
                {
                    if (tmpOSD.Type != OSDType.Map)
                    {
                        httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                        return;
                    }

                    OSDMap AgentDataMap = (OSDMap)tmpOSD;

                    //session_id = AgentDataMap["SessionId"].AsUUID();
                    group_id = AgentDataMap["GroupId"].AsUUID();
                }

            }
            else
            { //v1
                bypass_raycast = map["bypass_raycast"].AsBoolean();

                everyone_mask = ReadUIntVal(map["everyone_mask"]);
                flags = ReadUIntVal(map["flags"]);
                group_id = map["group_id"].AsUUID();
                group_mask = ReadUIntVal(map["group_mask"]);
                hollow = map["hollow"].AsInteger();
                material = map["material"].AsInteger();
                next_owner_mask = ReadUIntVal(map["next_owner_mask"]);
                hollow = map["hollow"].AsInteger();
                p_code = map["p_code"].AsInteger();
                path_begin = map["path_begin"].AsInteger();
                path_curve = map["path_curve"].AsInteger();
                path_end = map["path_end"].AsInteger();
                path_radius_offset = map["path_radius_offset"].AsInteger();
                path_revolutions = map["path_revolutions"].AsInteger();
                path_scale_x = map["path_scale_x"].AsInteger();
                path_scale_y = map["path_scale_y"].AsInteger();
                path_shear_x = map["path_shear_x"].AsInteger();
                path_shear_y = map["path_shear_y"].AsInteger();
                path_skew = map["path_skew"].AsInteger();
                path_taper_x = map["path_taper_x"].AsInteger();
                path_taper_y = map["path_taper_y"].AsInteger();
                path_twist = map["path_twist"].AsInteger();
                path_twist_begin = map["path_twist_begin"].AsInteger();
                profile_begin = map["profile_begin"].AsInteger();
                profile_curve = map["profile_curve"].AsInteger();
                profile_end = map["profile_end"].AsInteger();

                ray_end_is_intersection = map["ray_end_is_intersection"].AsBoolean();

                ray_target_id = map["ray_target_id"].AsUUID();


                //session_id = rm["session_id"].AsUUID();
                state = map["state"].AsInteger();
                lastattach = map["last_attach_point"].AsInteger();
                try
                {
                    ray_end = ((OSDArray)map["ray_end"]).AsVector3();
                    ray_start = ((OSDArray)map["ray_start"]).AsVector3();
                    rotation = ((OSDArray)map["rotation"]).AsQuaternion();
                    scale = ((OSDArray)map["scale"]).AsVector3();
                }
                catch
                {
                    httpResponse.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }
            }

            Vector3 pos = m_scene.GetNewRezLocation(ray_start, ray_end, ray_target_id, rotation, (bypass_raycast) ? (byte)1 : (byte)0, (ray_end_is_intersection) ? (byte)1 : (byte)0, true, scale, false);
            if (!m_scene.Permissions.CanRezObject(1, avatarID, pos))
            {
                httpResponse.StatusCode = (int)HttpStatusCode.Unauthorized;
                return;
            }

            PrimitiveBaseShape pbs = PrimitiveBaseShape.CreateBox();

            pbs.PathBegin = (ushort)path_begin;
            pbs.PathCurve = (byte)path_curve;
            pbs.PathEnd = (ushort)path_end;
            pbs.PathRadiusOffset = (sbyte)path_radius_offset;
            pbs.PathRevolutions = (byte)path_revolutions;
            pbs.PathScaleX = (byte)path_scale_x;
            pbs.PathScaleY = (byte)path_scale_y;
            pbs.PathShearX = (byte)path_shear_x;
            pbs.PathShearY = (byte)path_shear_y;
            pbs.PathSkew = (sbyte)path_skew;
            pbs.PathTaperX = (sbyte)path_taper_x;
            pbs.PathTaperY = (sbyte)path_taper_y;
            pbs.PathTwist = (sbyte)path_twist;
            pbs.PathTwistBegin = (sbyte)path_twist_begin;
            pbs.HollowShape = (HollowShape)hollow;
            pbs.PCode = (byte)p_code;
            pbs.ProfileBegin = (ushort)profile_begin;
            pbs.ProfileCurve = (byte)profile_curve;
            pbs.ProfileEnd = (ushort)profile_end;
            pbs.Scale = scale;
            pbs.State = (byte)state;
            pbs.LastAttachPoint = (byte)lastattach;

            SceneObjectGroup obj = m_scene.AddNewPrim(avatarID, group_id, pos, rotation, pbs);

            SceneObjectPart rootpart = obj.RootPart;
            rootpart.Shape = pbs;
            rootpart.Flags |= (PrimFlags)flags;
            rootpart.EveryoneMask = everyone_mask;
            rootpart.GroupID = group_id;
            rootpart.GroupMask = group_mask;
            rootpart.NextOwnerMask = next_owner_mask;
            rootpart.Material = (byte)material;

            obj.InvalidateDeepEffectivePerms();

            m_scene.PhysicsScene.AddPhysicsActorTaint(rootpart.PhysActor);

            httpResponse.StatusCode = (int)HttpStatusCode.OK;
            httpResponse.RawBuffer = Util.UTF8NBGetbytes(String.Format("<llsd><map><key>local_id</key>{0}</map></llsd>", ConvertUintToBytes(obj.LocalId)));
         }

        private uint ReadUIntVal(OSD obj)
        {
            byte[] tmp = obj.AsBinary();
            if (BitConverter.IsLittleEndian)
                Array.Reverse(tmp);
            return Utils.BytesToUInt(tmp);

        }
        private string ConvertUintToBytes(uint val)
        {
            byte[] resultbytes = Utils.UIntToBytes(val);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(resultbytes);
            return String.Format("<binary>{0}</binary>", Convert.ToBase64String(resultbytes));
        }
    }
}
