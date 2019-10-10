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
using vector = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Vector3;
using rotation = OpenSim.Region.ScriptEngine.Shared.LSL_Types.Quaternion;
using LSLInteger = OpenSim.Region.ScriptEngine.Shared.LSL_Types.LSLInteger;

namespace OpenSim.Region.ScriptEngine.Shared.ScriptBase
{
    public partial class ScriptBaseClass
    {
        // SCRIPTS CONSTANTS
        public static readonly LSLInteger OS_APIVERSION = 9;

        public static readonly LSLInteger TRUE = 1;
        public static readonly LSLInteger FALSE = 0;

        public const int STATUS_PHYSICS = 1;
        public const int STATUS_ROTATE_X = 2;
        public const int STATUS_ROTATE_Y = 4;
        public const int STATUS_ROTATE_Z = 8;
        public const int STATUS_PHANTOM = 16;
        public const int STATUS_SANDBOX = 32;
        public const int STATUS_BLOCK_GRAB = 64;
        public const int STATUS_DIE_AT_EDGE = 128;
        public const int STATUS_RETURN_AT_EDGE = 256;
        public const int STATUS_CAST_SHADOWS = 512;
        public const int STATUS_BLOCK_GRAB_OBJECT = 1024;

        public const int AGENT = 1;
        public const int AGENT_BY_LEGACY_NAME = 1;
        public const int AGENT_BY_USERNAME = 0x10;
        public const int NPC = 0x20;
        //ApiDesc Objects running a script or physically moving
        public const int ACTIVE = 2;
        public const int PASSIVE = 4;
        public const int SCRIPTED = 8;

        public const int CONTROL_FWD = 1;
        public const int CONTROL_BACK = 2;
        public const int CONTROL_LEFT = 4;
        public const int CONTROL_RIGHT = 8;
        public const int CONTROL_UP = 16;
        public const int CONTROL_DOWN = 32;
        public const int CONTROL_ROT_LEFT = 256;
        public const int CONTROL_ROT_RIGHT = 512;
        public const int CONTROL_LBUTTON = 268435456;
        public const int CONTROL_ML_LBUTTON = 1073741824;

        //Permissions
        public const int PERMISSION_DEBIT = 2;
        public const int PERMISSION_TAKE_CONTROLS = 4;
        public const int PERMISSION_REMAP_CONTROLS = 8;
        public const int PERMISSION_TRIGGER_ANIMATION = 16;
        public const int PERMISSION_ATTACH = 32;
        public const int PERMISSION_RELEASE_OWNERSHIP = 64;
        public const int PERMISSION_CHANGE_LINKS = 128;
        public const int PERMISSION_CHANGE_JOINTS = 256;
        public const int PERMISSION_CHANGE_PERMISSIONS = 512;
        public const int PERMISSION_TRACK_CAMERA = 1024;
        public const int PERMISSION_CONTROL_CAMERA = 2048;
        public const int PERMISSION_TELEPORT = 4096;
        public const int PERMISSION_OVERRIDE_ANIMATIONS = 0x8000;

        public const int AGENT_FLYING = 0x1;
        //ApiDesc The agent has attachments
        public const int AGENT_ATTACHMENTS = 0x2;
        //ApiDesc The agent has scripted attachments
        public const int AGENT_SCRIPTED = 0x4;
        public const int AGENT_MOUSELOOK = 0x8;
        public const int AGENT_SITTING = 0x10;
        public const int AGENT_ON_OBJECT = 0x20;
        public const int AGENT_AWAY = 0x40;
        public const int AGENT_WALKING = 0x80;
        public const int AGENT_IN_AIR = 0x100;
        public const int AGENT_TYPING = 0x200;
        public const int AGENT_CROUCHING = 0x400;
        public const int AGENT_BUSY = 0x800;
        public const int AGENT_ALWAYS_RUN = 0x1000;
        public const int AGENT_AUTOPILOT = 0x2000;
        public const int AGENT_MALE = 0x40000000;

        //Particle Systems
        public const int PSYS_PART_INTERP_COLOR_MASK = 1;
        public const int PSYS_PART_INTERP_SCALE_MASK = 2;
        public const int PSYS_PART_BOUNCE_MASK = 4;
        public const int PSYS_PART_WIND_MASK = 8;
        public const int PSYS_PART_FOLLOW_SRC_MASK = 16;
        public const int PSYS_PART_FOLLOW_VELOCITY_MASK = 32;
        public const int PSYS_PART_TARGET_POS_MASK = 64;
        public const int PSYS_PART_TARGET_LINEAR_MASK = 128;
        public const int PSYS_PART_EMISSIVE_MASK = 256;
        public const int PSYS_PART_RIBBON_MASK = 1024;
        public const int PSYS_PART_FLAGS = 0;
        public const int PSYS_PART_START_COLOR = 1;
        public const int PSYS_PART_START_ALPHA = 2;
        public const int PSYS_PART_END_COLOR = 3;
        public const int PSYS_PART_END_ALPHA = 4;
        public const int PSYS_PART_START_SCALE = 5;
        public const int PSYS_PART_END_SCALE = 6;
        public const int PSYS_PART_MAX_AGE = 7;
        public const int PSYS_SRC_ACCEL = 8;
        public const int PSYS_SRC_PATTERN = 9;
        public const int PSYS_SRC_INNERANGLE = 10;
        public const int PSYS_SRC_OUTERANGLE = 11;
        public const int PSYS_SRC_TEXTURE = 12;
        public const int PSYS_SRC_BURST_RATE = 13;
        public const int PSYS_SRC_BURST_PART_COUNT = 15;
        public const int PSYS_SRC_BURST_RADIUS = 16;
        public const int PSYS_SRC_BURST_SPEED_MIN = 17;
        public const int PSYS_SRC_BURST_SPEED_MAX = 18;
        public const int PSYS_SRC_MAX_AGE = 19;
        public const int PSYS_SRC_TARGET_KEY = 20;
        public const int PSYS_SRC_OMEGA = 21;
        public const int PSYS_SRC_ANGLE_BEGIN = 22;
        public const int PSYS_SRC_ANGLE_END = 23;
        public const int PSYS_PART_BLEND_FUNC_SOURCE = 24;
        public const int PSYS_PART_BLEND_FUNC_DEST = 25;
        public const int PSYS_PART_START_GLOW = 26;
        public const int PSYS_PART_END_GLOW = 27;
        public const int PSYS_PART_BF_ONE = 0;
        public const int PSYS_PART_BF_ZERO = 1;
        public const int PSYS_PART_BF_DEST_COLOR = 2;
        public const int PSYS_PART_BF_SOURCE_COLOR = 3;
        public const int PSYS_PART_BF_ONE_MINUS_DEST_COLOR = 4;
        public const int PSYS_PART_BF_ONE_MINUS_SOURCE_COLOR = 5;
        public const int PSYS_PART_BF_SOURCE_ALPHA = 7;
        public const int PSYS_PART_BF_ONE_MINUS_SOURCE_ALPHA = 9;
        public const int PSYS_SRC_PATTERN_DROP = 1;
        public const int PSYS_SRC_PATTERN_EXPLODE = 2;
        public const int PSYS_SRC_PATTERN_ANGLE = 4;
        public const int PSYS_SRC_PATTERN_ANGLE_CONE = 8;
        public const int PSYS_SRC_PATTERN_ANGLE_CONE_EMPTY = 16;

        public const int VEHICLE_TYPE_NONE = 0;
        public const int VEHICLE_TYPE_SLED = 1;
        public const int VEHICLE_TYPE_CAR = 2;
        public const int VEHICLE_TYPE_BOAT = 3;
        public const int VEHICLE_TYPE_AIRPLANE = 4;
        public const int VEHICLE_TYPE_BALLOON = 5;
        public const int VEHICLE_LINEAR_FRICTION_TIMESCALE = 16;
        public const int VEHICLE_ANGULAR_FRICTION_TIMESCALE = 17;
        public const int VEHICLE_LINEAR_MOTOR_DIRECTION = 18;
        public const int VEHICLE_LINEAR_MOTOR_OFFSET = 20;
        public const int VEHICLE_ANGULAR_MOTOR_DIRECTION = 19;
        public const int VEHICLE_HOVER_HEIGHT = 24;
        public const int VEHICLE_HOVER_EFFICIENCY = 25;
        public const int VEHICLE_HOVER_TIMESCALE = 26;
        public const int VEHICLE_BUOYANCY = 27;
        public const int VEHICLE_LINEAR_DEFLECTION_EFFICIENCY = 28;
        public const int VEHICLE_LINEAR_DEFLECTION_TIMESCALE = 29;
        public const int VEHICLE_LINEAR_MOTOR_TIMESCALE = 30;
        public const int VEHICLE_LINEAR_MOTOR_DECAY_TIMESCALE = 31;
        public const int VEHICLE_ANGULAR_DEFLECTION_EFFICIENCY = 32;
        public const int VEHICLE_ANGULAR_DEFLECTION_TIMESCALE = 33;
        public const int VEHICLE_ANGULAR_MOTOR_TIMESCALE = 34;
        public const int VEHICLE_ANGULAR_MOTOR_DECAY_TIMESCALE = 35;
        public const int VEHICLE_VERTICAL_ATTRACTION_EFFICIENCY = 36;
        public const int VEHICLE_VERTICAL_ATTRACTION_TIMESCALE = 37;
        public const int VEHICLE_BANKING_EFFICIENCY = 38;
        public const int VEHICLE_BANKING_MIX = 39;
        public const int VEHICLE_BANKING_TIMESCALE = 40;
        public const int VEHICLE_REFERENCE_FRAME = 44;
        public const int VEHICLE_RANGE_BLOCK = 45;
        public const int VEHICLE_ROLL_FRAME = 46;
        public const int VEHICLE_FLAG_NO_DEFLECTION_UP = 1;
        public const int VEHICLE_FLAG_NO_FLY_UP = 1;  //legacy
        public const int VEHICLE_FLAG_LIMIT_ROLL_ONLY = 2;
        public const int VEHICLE_FLAG_HOVER_WATER_ONLY = 4;
        public const int VEHICLE_FLAG_HOVER_TERRAIN_ONLY = 8;
        public const int VEHICLE_FLAG_HOVER_GLOBAL_HEIGHT = 16;
        public const int VEHICLE_FLAG_HOVER_UP_ONLY = 32;
        public const int VEHICLE_FLAG_LIMIT_MOTOR_UP = 64;
        public const int VEHICLE_FLAG_MOUSELOOK_STEER = 128;
        public const int VEHICLE_FLAG_MOUSELOOK_BANK = 256;
        public const int VEHICLE_FLAG_CAMERA_DECOUPLED = 512;
        public const int VEHICLE_FLAG_NO_X = 1024;
        public const int VEHICLE_FLAG_NO_Y = 2048;
        public const int VEHICLE_FLAG_NO_Z = 4096;
        public const int VEHICLE_FLAG_LOCK_HOVER_HEIGHT = 8192;
        public const int VEHICLE_FLAG_NO_DEFLECTION = 16392;
        public const int VEHICLE_FLAG_LOCK_ROTATION = 32784;

        public const int INVENTORY_ALL = -1;
        public const int INVENTORY_NONE = -1;
        public const int INVENTORY_TEXTURE = 0;
        public const int INVENTORY_SOUND = 1;
        public const int INVENTORY_LANDMARK = 3;
        public const int INVENTORY_CLOTHING = 5;
        public const int INVENTORY_OBJECT = 6;
        public const int INVENTORY_NOTECARD = 7;
        public const int INVENTORY_SCRIPT = 10;
        public const int INVENTORY_BODYPART = 13;
        public const int INVENTORY_ANIMATION = 20;
        public const int INVENTORY_GESTURE = 21;

        public const int ATTACH_CHEST = 1;
        public const int ATTACH_HEAD = 2;
        public const int ATTACH_LSHOULDER = 3;
        public const int ATTACH_RSHOULDER = 4;
        public const int ATTACH_LHAND = 5;
        public const int ATTACH_RHAND = 6;
        public const int ATTACH_LFOOT = 7;
        public const int ATTACH_RFOOT = 8;
        public const int ATTACH_BACK = 9;
        public const int ATTACH_PELVIS = 10;
        public const int ATTACH_MOUTH = 11;
        public const int ATTACH_CHIN = 12;
        public const int ATTACH_LEAR = 13;
        public const int ATTACH_REAR = 14;
        public const int ATTACH_LEYE = 15;
        public const int ATTACH_REYE = 16;
        public const int ATTACH_NOSE = 17;
        public const int ATTACH_RUARM = 18;
        public const int ATTACH_RLARM = 19;
        public const int ATTACH_LUARM = 20;
        public const int ATTACH_LLARM = 21;
        public const int ATTACH_RHIP = 22;
        public const int ATTACH_RULEG = 23;
        public const int ATTACH_RLLEG = 24;
        public const int ATTACH_LHIP = 25;
        public const int ATTACH_LULEG = 26;
        public const int ATTACH_LLLEG = 27;
        public const int ATTACH_BELLY = 28;
        public const int ATTACH_RPEC = 29;
        public const int ATTACH_LPEC = 30;
        public const int ATTACH_LEFT_PEC = 29; // Same value as ATTACH_RPEC, see https://jira.secondlife.com/browse/SVC-580
        public const int ATTACH_RIGHT_PEC = 30; // Same value as ATTACH_LPEC, see https://jira.secondlife.com/browse/SVC-580
        public const int ATTACH_HUD_CENTER_2 = 31;
        public const int ATTACH_HUD_TOP_RIGHT = 32;
        public const int ATTACH_HUD_TOP_CENTER = 33;
        public const int ATTACH_HUD_TOP_LEFT = 34;
        public const int ATTACH_HUD_CENTER_1 = 35;
        public const int ATTACH_HUD_BOTTOM_LEFT = 36;
        public const int ATTACH_HUD_BOTTOM = 37;
        public const int ATTACH_HUD_BOTTOM_RIGHT = 38;
        public const int ATTACH_NECK = 39;
        public const int ATTACH_AVATAR_CENTER = 40;
        public const int ATTACH_LHAND_RING1 = 41;
        public const int ATTACH_RHAND_RING1 = 42;
        public const int ATTACH_TAIL_BASE = 43;
        public const int ATTACH_TAIL_TIP = 44;
        public const int ATTACH_LWING = 45;
        public const int ATTACH_RWING = 46;
        public const int ATTACH_FACE_JAW = 47;
        public const int ATTACH_FACE_LEAR = 48;
        public const int ATTACH_FACE_REAR = 49;
        public const int ATTACH_FACE_LEYE = 50;
        public const int ATTACH_FACE_REYE = 51;
        public const int ATTACH_FACE_TONGUE = 52;
        public const int ATTACH_GROIN = 53;
        public const int ATTACH_HIND_LFOOT = 54;
        public const int ATTACH_HIND_RFOOT = 55;

        #region osMessageAttachments constants

        /// <summary>
        /// Instructs osMessageAttachements to send the message to attachments
        ///     on every point.
        /// </summary>
        /// <remarks>
        /// One might expect this to be named OS_ATTACH_ALL, but then one might
        ///     also expect functions designed to attach or detach or get
        ///     attachments to work with it too. Attaching a no-copy item to
        ///     many attachments could be dangerous.
        /// when combined with OS_ATTACH_MSG_INVERT_POINTS, will prevent the
        ///     message from being sent.
        /// if combined with OS_ATTACH_MSG_OBJECT_CREATOR or
        ///     OS_ATTACH_MSG_SCRIPT_CREATOR, could result in no message being
        ///     sent- this is expected behaviour.
        /// </remarks>
        public const int OS_ATTACH_MSG_ALL = -65535;

        /// <summary>
        /// Instructs osMessageAttachements to invert how the attachment points
        ///     list should be treated (e.g. go from inclusive operation to
        ///     exclusive operation).
        /// </summary>
        /// <remarks>
        /// This might be used if you want to deliver a message to one set of
        ///     attachments and a different message to everything else. With
        ///     this flag, you only need to build one explicit list for both calls.
        /// </remarks>
        public const int OS_ATTACH_MSG_INVERT_POINTS = 1;

        /// <summary>
        /// Instructs osMessageAttachments to only send the message to
        ///     attachments with a CreatorID that matches the host object CreatorID
        /// </summary>
        /// <remarks>
        /// This would be used if distributed in an object vendor/updater server.
        /// </remarks>
        public const int OS_ATTACH_MSG_OBJECT_CREATOR = 2;

        /// <summary>
        /// Instructs osMessageAttachments to only send the message to
        ///     attachments with a CreatorID that matches the sending script CreatorID
        /// </summary>
        /// <remarks>
        /// This might be used if the script is distributed independently of a
        ///     containing object.
        /// </remarks>
        public const int OS_ATTACH_MSG_SCRIPT_CREATOR = 4;

        #endregion

        public const int LAND_LEVEL = 0;
        public const int LAND_RAISE = 1;
        public const int LAND_LOWER = 2;
        public const int LAND_SMOOTH = 3;
        public const int LAND_NOISE = 4;
        public const int LAND_REVERT = 5;
        public const int LAND_SMALL_BRUSH = 1;
        public const int LAND_MEDIUM_BRUSH = 2;
        public const int LAND_LARGE_BRUSH = 3;

        //Agent Dataserver
        public const int DATA_ONLINE = 1;
        public const int DATA_NAME = 2;
        public const int DATA_BORN = 3;
        public const int DATA_RATING = 4;
        public const int DATA_SIM_POS = 5;
        public const int DATA_SIM_STATUS = 6;
        public const int DATA_SIM_RATING = 7;
        public const int DATA_PAYINFO = 8;
        public const int DATA_SIM_RELEASE = 128;

        public const int ANIM_ON = 1;
        public const int LOOP = 2;
        public const int REVERSE = 4;
        public const int PING_PONG = 8;
        public const int SMOOTH = 16;
        public const int ROTATE = 32;
        public const int SCALE = 64;
        public const int ALL_SIDES = -1;

        // LINK flags
        public const int LINK_SET = -1;
        public const int LINK_ROOT = 1;
        public const int LINK_ALL_OTHERS = -2;
        public const int LINK_ALL_CHILDREN = -3;
        public const int LINK_THIS = -4;

        public const int CHANGED_INVENTORY = 1;
        public const int CHANGED_COLOR = 2;
        public const int CHANGED_SHAPE = 4;
        public const int CHANGED_SCALE = 8;
        public const int CHANGED_TEXTURE = 16;
        public const int CHANGED_LINK = 32;
        public const int CHANGED_ALLOWED_DROP = 64;
        public const int CHANGED_OWNER = 128;
        public const int CHANGED_REGION = 256;
        public const int CHANGED_TELEPORT = 512;
        public const int CHANGED_REGION_RESTART = 1024;
        public const int CHANGED_REGION_START = 1024; //LL Changed the constant from CHANGED_REGION_RESTART
        public const int CHANGED_MEDIA = 2048;
        //ApiDesc opensim specific
        public const int CHANGED_ANIMATION = 16384;
        //ApiDesc opensim specific
        public const int CHANGED_POSITION = 32768;

        public const int TYPE_INVALID = 0;
        public const int TYPE_INTEGER = 1;
        public const int TYPE_FLOAT = 2;
        public const int TYPE_STRING = 3;
        public const int TYPE_KEY = 4;
        public const int TYPE_VECTOR = 5;
        public const int TYPE_ROTATION = 6;

        //XML RPC Remote Data Channel
        public const int REMOTE_DATA_CHANNEL = 1;
        public const int REMOTE_DATA_REQUEST = 2;
        public const int REMOTE_DATA_REPLY = 3;

        //llHTTPRequest
        public const int HTTP_METHOD = 0;
        public const int HTTP_MIMETYPE = 1;
        public const int HTTP_BODY_MAXLENGTH = 2;
        public const int HTTP_VERIFY_CERT = 3;
        public const int HTTP_VERBOSE_THROTTLE = 4;
        public const int HTTP_CUSTOM_HEADER = 5;
        public const int HTTP_PRAGMA_NO_CACHE = 6;

        // llSetContentType
        public const int CONTENT_TYPE_TEXT = 0; //text/plain
        public const int CONTENT_TYPE_HTML = 1; //text/html
        public const int CONTENT_TYPE_XML = 2; //application/xml
        public const int CONTENT_TYPE_XHTML = 3; //application/xhtml+xml
        public const int CONTENT_TYPE_ATOM = 4; //application/atom+xml
        public const int CONTENT_TYPE_JSON = 5; //application/json
        public const int CONTENT_TYPE_LLSD = 6; //application/llsd+xml
        public const int CONTENT_TYPE_FORM = 7; //application/x-www-form-urlencoded
        public const int CONTENT_TYPE_RSS = 8; //application/rss+xml

        //parameters comand flags
        public const int PRIM_MATERIAL = 2;
        public const int PRIM_PHYSICS = 3;
        public const int PRIM_TEMP_ON_REZ = 4;
        public const int PRIM_PHANTOM = 5;
        public const int PRIM_POSITION = 6;
        public const int PRIM_SIZE = 7;
        public const int PRIM_ROTATION = 8;
        public const int PRIM_TYPE = 9;
        // gap 10-16
        public const int PRIM_TEXTURE = 17;
        public const int PRIM_COLOR = 18;
        public const int PRIM_BUMP_SHINY = 19;
        public const int PRIM_FULLBRIGHT = 20;
        public const int PRIM_FLEXIBLE = 21;
        public const int PRIM_TEXGEN = 22;
        public const int PRIM_POINT_LIGHT = 23; // Huh?
        //ApiDesc not supported
        public const int PRIM_CAST_SHADOWS = 24; // Not implemented, here for completeness sake
        public const int PRIM_GLOW = 25;
        public const int PRIM_TEXT = 26;
        public const int PRIM_NAME = 27;
        public const int PRIM_DESC = 28;
        public const int PRIM_ROT_LOCAL = 29;
        public const int PRIM_PHYSICS_SHAPE_TYPE = 30;
        public const int PRIM_PHYSICS_MATERIAL = 31; // apparently not on SL wiki
        public const int PRIM_OMEGA = 32;
        public const int PRIM_POS_LOCAL = 33;
        public const int PRIM_LINK_TARGET = 34;
        public const int PRIM_SLICE = 35;
        public const int PRIM_SPECULAR = 36;
        public const int PRIM_NORMAL = 37;
        public const int PRIM_ALPHA_MODE = 38;
        //ApiDesc not supported
        public const int PRIM_ALLOW_UNSIT = 39; // experiences related. unsupported
        //ApiDesc not supported
        public const int PRIM_SCRIPTED_SIT_ONLY = 40; // experiences related. unsupported
        public const int PRIM_SIT_TARGET = 41;


        // parameters

        public const int PRIM_ALPHA_MODE_NONE = 0;
        public const int PRIM_ALPHA_MODE_BLEND = 1;
        public const int PRIM_ALPHA_MODE_MASK = 2;
        public const int PRIM_ALPHA_MODE_EMISSIVE = 3;

        public const int PRIM_TEXGEN_DEFAULT = 0;
        public const int PRIM_TEXGEN_PLANAR = 1;

        public const int PRIM_TYPE_BOX = 0;
        public const int PRIM_TYPE_CYLINDER = 1;
        public const int PRIM_TYPE_PRISM = 2;
        public const int PRIM_TYPE_SPHERE = 3;
        public const int PRIM_TYPE_TORUS = 4;
        public const int PRIM_TYPE_TUBE = 5;
        public const int PRIM_TYPE_RING = 6;
        public const int PRIM_TYPE_SCULPT = 7;

        public const int PRIM_HOLE_DEFAULT = 0;
        public const int PRIM_HOLE_CIRCLE = 16;
        public const int PRIM_HOLE_SQUARE = 32;
        public const int PRIM_HOLE_TRIANGLE = 48;

        public const int PRIM_MATERIAL_STONE = 0;
        public const int PRIM_MATERIAL_METAL = 1;
        public const int PRIM_MATERIAL_GLASS = 2;
        public const int PRIM_MATERIAL_WOOD = 3;
        public const int PRIM_MATERIAL_FLESH = 4;
        public const int PRIM_MATERIAL_PLASTIC = 5;
        public const int PRIM_MATERIAL_RUBBER = 6;
        public const int PRIM_MATERIAL_LIGHT = 7;

        public const int PRIM_SHINY_NONE = 0;
        public const int PRIM_SHINY_LOW = 1;
        public const int PRIM_SHINY_MEDIUM = 2;
        public const int PRIM_SHINY_HIGH = 3;
        public const int PRIM_BUMP_NONE = 0;
        public const int PRIM_BUMP_BRIGHT = 1;
        public const int PRIM_BUMP_DARK = 2;
        public const int PRIM_BUMP_WOOD = 3;
        public const int PRIM_BUMP_BARK = 4;
        public const int PRIM_BUMP_BRICKS = 5;
        public const int PRIM_BUMP_CHECKER = 6;
        public const int PRIM_BUMP_CONCRETE = 7;
        public const int PRIM_BUMP_TILE = 8;
        public const int PRIM_BUMP_STONE = 9;
        public const int PRIM_BUMP_DISKS = 10;
        public const int PRIM_BUMP_GRAVEL = 11;
        public const int PRIM_BUMP_BLOBS = 12;
        public const int PRIM_BUMP_SIDING = 13;
        public const int PRIM_BUMP_LARGETILE = 14;
        public const int PRIM_BUMP_STUCCO = 15;
        public const int PRIM_BUMP_SUCTION = 16;
        public const int PRIM_BUMP_WEAVE = 17;

        public const int PRIM_SCULPT_TYPE_SPHERE = 1;
        public const int PRIM_SCULPT_TYPE_TORUS = 2;
        public const int PRIM_SCULPT_TYPE_PLANE = 3;
        public const int PRIM_SCULPT_TYPE_CYLINDER = 4;
        public const int PRIM_SCULPT_FLAG_INVERT = 0x40;
        public const int PRIM_SCULPT_FLAG_MIRROR = 0x80;
        //ApiDesc Auxiliar to clear flags keeping scultp type
        public const int PRIM_SCULPT_TYPE_MASK = 0x07;  // auxiliar mask

        public const int PRIM_PHYSICS_SHAPE_PRIM = 0;
        public const int PRIM_PHYSICS_SHAPE_NONE = 1;
        public const int PRIM_PHYSICS_SHAPE_CONVEX = 2;

        public const int PROFILE_NONE = 0;
        public const int PROFILE_SCRIPT_MEMORY = 1;

        public const int MASK_BASE = 0;
        public const int MASK_OWNER = 1;
        public const int MASK_GROUP = 2;
        public const int MASK_EVERYONE = 3;
        public const int MASK_NEXT = 4;

        public const int PERM_TRANSFER = 8192;
        public const int PERM_MODIFY = 16384;
        public const int PERM_COPY = 32768;
        public const int PERM_MOVE = 524288;
        public const int PERM_ALL = 2147483647;

        public const int PARCEL_MEDIA_COMMAND_STOP = 0;
        public const int PARCEL_MEDIA_COMMAND_PAUSE = 1;
        public const int PARCEL_MEDIA_COMMAND_PLAY = 2;
        public const int PARCEL_MEDIA_COMMAND_LOOP = 3;
        public const int PARCEL_MEDIA_COMMAND_TEXTURE = 4;
        public const int PARCEL_MEDIA_COMMAND_URL = 5;
        public const int PARCEL_MEDIA_COMMAND_TIME = 6;
        public const int PARCEL_MEDIA_COMMAND_AGENT = 7;
        public const int PARCEL_MEDIA_COMMAND_UNLOAD = 8;
        public const int PARCEL_MEDIA_COMMAND_AUTO_ALIGN = 9;
        public const int PARCEL_MEDIA_COMMAND_TYPE = 10;
        public const int PARCEL_MEDIA_COMMAND_SIZE = 11;
        public const int PARCEL_MEDIA_COMMAND_DESC = 12;

        public const int PARCEL_FLAG_ALLOW_FLY = 0x1;                           // parcel allows flying
        public const int PARCEL_FLAG_ALLOW_SCRIPTS = 0x2;                       // parcel allows outside scripts
        public const int PARCEL_FLAG_ALLOW_LANDMARK = 0x8;                      // parcel allows landmarks to be created
        public const int PARCEL_FLAG_ALLOW_TERRAFORM = 0x10;                    // parcel allows anyone to terraform the land
        public const int PARCEL_FLAG_ALLOW_DAMAGE = 0x20;                       // parcel allows damage
        public const int PARCEL_FLAG_ALLOW_CREATE_OBJECTS = 0x40;               // parcel allows anyone to create objects
        public const int PARCEL_FLAG_USE_ACCESS_GROUP = 0x100;                  // parcel limits access to a group
        public const int PARCEL_FLAG_USE_ACCESS_LIST = 0x200;                   // parcel limits access to a list of residents
        public const int PARCEL_FLAG_USE_BAN_LIST = 0x400;                      // parcel uses a ban list, including restricting access based on payment info
        public const int PARCEL_FLAG_USE_LAND_PASS_LIST = 0x800;                // parcel allows passes to be purchased
        public const int PARCEL_FLAG_LOCAL_SOUND_ONLY = 0x8000;                 // parcel restricts spatialized sound to the parcel
        public const int PARCEL_FLAG_RESTRICT_PUSHOBJECT = 0x200000;            // parcel restricts llPushObject
        public const int PARCEL_FLAG_ALLOW_GROUP_SCRIPTS = 0x2000000;           // parcel allows scripts owned by group
        public const int PARCEL_FLAG_ALLOW_CREATE_GROUP_OBJECTS = 0x4000000;    // parcel allows group object creation
        public const int PARCEL_FLAG_ALLOW_ALL_OBJECT_ENTRY = 0x8000000;        // parcel allows objects owned by any user to enter
        public const int PARCEL_FLAG_ALLOW_GROUP_OBJECT_ENTRY = 0x10000000;     // parcel allows with the same group to enter

        public const int REGION_FLAG_ALLOW_DAMAGE = 0x1;                        // region is entirely damage enabled
        public const int REGION_FLAG_FIXED_SUN = 0x10;                          // region has a fixed sun position
        public const int REGION_FLAG_BLOCK_TERRAFORM = 0x40;                    // region terraforming disabled
        public const int REGION_FLAG_SANDBOX = 0x100;                           // region is a sandbox
        public const int REGION_FLAG_DISABLE_COLLISIONS = 0x1000;               // region has disabled collisions
        public const int REGION_FLAG_DISABLE_PHYSICS = 0x4000;                  // region has disabled physics
        public const int REGION_FLAG_BLOCK_FLY = 0x80000;                       // region blocks flying
        public const int REGION_FLAG_ALLOW_DIRECT_TELEPORT = 0x100000;          // region allows direct teleports
        public const int REGION_FLAG_RESTRICT_PUSHOBJECT = 0x400000;            // region restricts llPushObject

        //llManageEstateAccess
        public const int ESTATE_ACCESS_ALLOWED_AGENT_ADD = 0;
        public const int ESTATE_ACCESS_ALLOWED_AGENT_REMOVE = 1;
        public const int ESTATE_ACCESS_ALLOWED_GROUP_ADD = 2;
        public const int ESTATE_ACCESS_ALLOWED_GROUP_REMOVE = 3;
        public const int ESTATE_ACCESS_BANNED_AGENT_ADD = 4;
        public const int ESTATE_ACCESS_BANNED_AGENT_REMOVE = 5;

        public static readonly LSLInteger PAY_HIDE = -1;
        public static readonly LSLInteger PAY_DEFAULT = -2;

        public const string NULL_KEY = "00000000-0000-0000-0000-000000000000";
        public const string EOF = "\n\n\n";
        public const double PI = 3.14159274f;
        public const double TWO_PI = 6.28318548f;
        public const double PI_BY_TWO = 1.57079637f;
        public const double DEG_TO_RAD = 0.01745329238f;
        public const double RAD_TO_DEG = 57.29578f;
        public const double SQRT2 = 1.414213538f;
        public const int STRING_TRIM_HEAD = 1;
        public const int STRING_TRIM_TAIL = 2;
        public const int STRING_TRIM = 3;
        public const int LIST_STAT_RANGE = 0;
        public const int LIST_STAT_MIN = 1;
        public const int LIST_STAT_MAX = 2;
        public const int LIST_STAT_MEAN = 3;
        public const int LIST_STAT_MEDIAN = 4;
        public const int LIST_STAT_STD_DEV = 5;
        public const int LIST_STAT_SUM = 6;
        public const int LIST_STAT_SUM_SQUARES = 7;
        public const int LIST_STAT_NUM_COUNT = 8;
        public const int LIST_STAT_GEOMETRIC_MEAN = 9;
        public const int LIST_STAT_HARMONIC_MEAN = 100;

        //ParcelPrim Categories
        public const int PARCEL_COUNT_TOTAL = 0;
        public const int PARCEL_COUNT_OWNER = 1;
        public const int PARCEL_COUNT_GROUP = 2;
        public const int PARCEL_COUNT_OTHER = 3;
        public const int PARCEL_COUNT_SELECTED = 4;
        public const int PARCEL_COUNT_TEMP = 5;

        public const int DEBUG_CHANNEL = 0x7FFFFFFF;
        public const int PUBLIC_CHANNEL = 0x00000000;

        // Constants for llGetObjectDetails
        public const int OBJECT_UNKNOWN_DETAIL = -1;
        public const int OBJECT_NAME = 1;
        public const int OBJECT_DESC = 2;
        public const int OBJECT_POS = 3;
        public const int OBJECT_ROT = 4;
        public const int OBJECT_VELOCITY = 5;
        public const int OBJECT_OWNER = 6;
        public const int OBJECT_GROUP = 7;
        public const int OBJECT_CREATOR = 8;
        public const int OBJECT_RUNNING_SCRIPT_COUNT = 9;
        public const int OBJECT_TOTAL_SCRIPT_COUNT = 10;
        public const int OBJECT_SCRIPT_MEMORY = 11;
        public const int OBJECT_SCRIPT_TIME = 12;
        public const int OBJECT_PRIM_EQUIVALENCE = 13;
        public const int OBJECT_SERVER_COST = 14;
        public const int OBJECT_STREAMING_COST = 15;
        public const int OBJECT_PHYSICS_COST = 16;
        public const int OBJECT_CHARACTER_TIME = 17;
        public const int OBJECT_ROOT = 18;
        public const int OBJECT_ATTACHED_POINT = 19;
        public const int OBJECT_PATHFINDING_TYPE = 20;
        public const int OBJECT_PHYSICS = 21;
        public const int OBJECT_PHANTOM = 22;
        public const int OBJECT_TEMP_ON_REZ = 23;
        public const int OBJECT_RENDER_WEIGHT = 24;
        public const int OBJECT_HOVER_HEIGHT = 25;
        public const int OBJECT_BODY_SHAPE_TYPE = 26;
        public const int OBJECT_LAST_OWNER_ID = 27;
        public const int OBJECT_CLICK_ACTION = 28;
        public const int OBJECT_OMEGA = 29;
        public const int OBJECT_PRIM_COUNT = 30;
        public const int OBJECT_TOTAL_INVENTORY_COUNT = 31;
        public const int OBJECT_REZZER_KEY = 32;
        public const int OBJECT_GROUP_TAG = 33;
        public const int OBJECT_TEMP_ATTACHED = 34;
        public const int OBJECT_ATTACHED_SLOTS_AVAILABLE = 35;

        // Pathfinding types
        //ApiDesc not supported
        public const int OPT_OTHER = -1;
        //ApiDesc not supported
        public const int OPT_LEGACY_LINKSET = 0;
        //ApiDesc not supported
        public const int OPT_AVATAR = 1;
        //ApiDesc not supported
        public const int OPT_CHARACTER = 2;
        //ApiDesc not supported
        public const int OPT_WALKABLE = 3;
        //ApiDesc not supported
        public const int OPT_STATIC_OBSTACLE = 4;
        //ApiDesc not supported
        public const int OPT_MATERIAL_VOLUME = 5;
        //ApiDesc not supported
        public const int OPT_EXCLUSION_VOLUME = 6;

        // for llGetAgentList
        public const int AGENT_LIST_PARCEL = 0x1;
        public const int AGENT_LIST_PARCEL_OWNER = 2;
        public const int AGENT_LIST_REGION = 4;
        public const int AGENT_LIST_EXCLUDENPC = 0x4000000;

        // Can not be public const?
        public static readonly vector ZERO_VECTOR = new vector(0.0, 0.0, 0.0);
        public static readonly rotation ZERO_ROTATION = new rotation(0.0, 0.0, 0.0, 1.0);

        // constants for llSetCameraParams
        public const int CAMERA_PITCH = 0;
        public const int CAMERA_FOCUS_OFFSET = 1;
        public const int CAMERA_FOCUS_OFFSET_X = 2;
        public const int CAMERA_FOCUS_OFFSET_Y = 3;
        public const int CAMERA_FOCUS_OFFSET_Z = 4;
        public const int CAMERA_POSITION_LAG = 5;
        public const int CAMERA_FOCUS_LAG = 6;
        public const int CAMERA_DISTANCE = 7;
        public const int CAMERA_BEHINDNESS_ANGLE = 8;
        public const int CAMERA_BEHINDNESS_LAG = 9;
        public const int CAMERA_POSITION_THRESHOLD = 10;
        public const int CAMERA_FOCUS_THRESHOLD = 11;
        public const int CAMERA_ACTIVE = 12;
        public const int CAMERA_POSITION = 13;
        public const int CAMERA_POSITION_X = 14;
        public const int CAMERA_POSITION_Y = 15;
        public const int CAMERA_POSITION_Z = 16;
        public const int CAMERA_FOCUS = 17;
        public const int CAMERA_FOCUS_X = 18;
        public const int CAMERA_FOCUS_Y = 19;
        public const int CAMERA_FOCUS_Z = 20;
        public const int CAMERA_POSITION_LOCKED = 21;
        public const int CAMERA_FOCUS_LOCKED = 22;

        // constants for llGetParcelDetails
        public const int PARCEL_DETAILS_NAME = 0;
        public const int PARCEL_DETAILS_DESC = 1;
        public const int PARCEL_DETAILS_OWNER = 2;
        public const int PARCEL_DETAILS_GROUP = 3;
        public const int PARCEL_DETAILS_AREA = 4;
        public const int PARCEL_DETAILS_ID = 5;
        public const int PARCEL_DETAILS_SEE_AVATARS = 6;
        public const int PARCEL_DETAILS_ANY_AVATAR_SOUNDS = 7;
        public const int PARCEL_DETAILS_GROUP_SOUNDS = 8;
        // constants for llGetParcelDetails os specific
        public const int PARCEL_DETAILS_DWELL = 64;

        //osSetParcelDetails
        public const int PARCEL_DETAILS_CLAIMDATE = 10;

        // constants for llSetClickAction
        public const int CLICK_ACTION_NONE = 0;
        public const int CLICK_ACTION_TOUCH = 0;
        public const int CLICK_ACTION_SIT = 1;
        public const int CLICK_ACTION_BUY = 2;
        public const int CLICK_ACTION_PAY = 3;
        public const int CLICK_ACTION_OPEN = 4;
        public const int CLICK_ACTION_PLAY = 5;
        public const int CLICK_ACTION_OPEN_MEDIA = 6;
        public const int CLICK_ACTION_ZOOM = 7;

        // constants for the llDetectedTouch* functions
        public const int TOUCH_INVALID_FACE = -1;
        public static readonly vector TOUCH_INVALID_TEXCOORD = new vector(-1.0, -1.0, 0.0);
        public static readonly vector TOUCH_INVALID_VECTOR = ZERO_VECTOR;

        // constants for llGetPrimMediaParams/llSetPrimMediaParams
        public const int PRIM_MEDIA_ALT_IMAGE_ENABLE = 0;
        public const int PRIM_MEDIA_CONTROLS = 1;
        public const int PRIM_MEDIA_CURRENT_URL = 2;
        public const int PRIM_MEDIA_HOME_URL = 3;
        public const int PRIM_MEDIA_AUTO_LOOP = 4;
        public const int PRIM_MEDIA_AUTO_PLAY = 5;
        public const int PRIM_MEDIA_AUTO_SCALE = 6;
        public const int PRIM_MEDIA_AUTO_ZOOM = 7;
        public const int PRIM_MEDIA_FIRST_CLICK_INTERACT = 8;
        public const int PRIM_MEDIA_WIDTH_PIXELS = 9;
        public const int PRIM_MEDIA_HEIGHT_PIXELS = 10;
        public const int PRIM_MEDIA_WHITELIST_ENABLE = 11;
        public const int PRIM_MEDIA_WHITELIST = 12;
        public const int PRIM_MEDIA_PERMS_INTERACT = 13;
        public const int PRIM_MEDIA_PERMS_CONTROL = 14;

        public const int PRIM_MEDIA_CONTROLS_STANDARD = 0;
        public const int PRIM_MEDIA_CONTROLS_MINI = 1;

        public const int PRIM_MEDIA_PERM_NONE = 0;
        public const int PRIM_MEDIA_PERM_OWNER = 1;
        public const int PRIM_MEDIA_PERM_GROUP = 2;
        public const int PRIM_MEDIA_PERM_ANYONE = 4;

        public const int DENSITY = 1;
        public const int FRICTION = 2;
        public const int RESTITUTION = 4;
        public const int GRAVITY_MULTIPLIER = 8;

        // extra constants for llSetPrimMediaParams
        public static readonly LSLInteger LSL_STATUS_OK = 0;
        public static readonly LSLInteger LSL_STATUS_MALFORMED_PARAMS = 1000;
        public static readonly LSLInteger LSL_STATUS_TYPE_MISMATCH = 1001;
        public static readonly LSLInteger LSL_STATUS_BOUNDS_ERROR = 1002;
        public static readonly LSLInteger LSL_STATUS_NOT_FOUND = 1003;
        public static readonly LSLInteger LSL_STATUS_NOT_SUPPORTED = 1004;
        public static readonly LSLInteger LSL_STATUS_INTERNAL_ERROR = 1999;
        public static readonly LSLInteger LSL_STATUS_WHITELIST_FAILED = 2001;

        // Constants for default textures
        public const string TEXTURE_BLANK = "5748decc-f629-461c-9a36-a35a221fe21f";
        public const string TEXTURE_DEFAULT = "89556747-24cb-43ed-920b-47caed15465f";
        public const string TEXTURE_PLYWOOD = "89556747-24cb-43ed-920b-47caed15465f";
        public const string TEXTURE_TRANSPARENT = "8dcd4a48-2d37-4909-9f78-f7a9eb4ef903";
        public const string TEXTURE_MEDIA = "8b5fec65-8d8d-9dc5-cda8-8fdf2716e361";

        // Constants for osGetRegionStats
        public const int STATS_TIME_DILATION = 0;
        public const int STATS_SIM_FPS = 1;
        public const int STATS_PHYSICS_FPS = 2;
        public const int STATS_AGENT_UPDATES = 3;
        public const int STATS_ROOT_AGENTS = 4;
        public const int STATS_CHILD_AGENTS = 5;
        public const int STATS_TOTAL_PRIMS = 6;
        public const int STATS_ACTIVE_PRIMS = 7;
        public const int STATS_FRAME_MS = 8;
        public const int STATS_NET_MS = 9;
        public const int STATS_PHYSICS_MS = 10;
        public const int STATS_IMAGE_MS = 11;
        public const int STATS_OTHER_MS = 12;
        public const int STATS_IN_PACKETS_PER_SECOND = 13;
        public const int STATS_OUT_PACKETS_PER_SECOND = 14;
        public const int STATS_UNACKED_BYTES = 15;
        public const int STATS_AGENT_MS = 16;
        public const int STATS_PENDING_DOWNLOADS = 17;
        public const int STATS_PENDING_UPLOADS = 18;
        public const int STATS_ACTIVE_SCRIPTS = 19;
        public const int STATS_SCRIPT_LPS = 20;

        // Constants for osNpc* functions
        public const int OS_NPC_FLY = 0;
        public const int OS_NPC_NO_FLY = 1;
        public const int OS_NPC_LAND_AT_TARGET = 2;
        public const int OS_NPC_RUNNING = 4;

        public const int OS_NPC_SIT_NOW = 0;

        public const int OS_NPC_CREATOR_OWNED = 0x1;
        public const int OS_NPC_NOT_OWNED = 0x2;
        public const int OS_NPC_SENSE_AS_AGENT = 0x4;
        public const int OS_NPC_OBJECT_GROUP = 0x08;

        public const string URL_REQUEST_GRANTED = "URL_REQUEST_GRANTED";
        public const string URL_REQUEST_DENIED = "URL_REQUEST_DENIED";

        public static readonly LSLInteger RC_REJECT_TYPES = 0;
        public static readonly LSLInteger RC_DETECT_PHANTOM = 1;
        public static readonly LSLInteger RC_DATA_FLAGS = 2;
        public static readonly LSLInteger RC_MAX_HITS = 3;

        public static readonly LSLInteger RC_REJECT_AGENTS = 1;
        public static readonly LSLInteger RC_REJECT_PHYSICAL = 2;
        public static readonly LSLInteger RC_REJECT_NONPHYSICAL = 4;
        public static readonly LSLInteger RC_REJECT_LAND = 8;
        public static readonly LSLInteger RC_REJECT_HOST = 0x20000000;
        public static readonly LSLInteger RC_REJECT_HOSTGROUP = 0x40000000;

        public static readonly LSLInteger RC_GET_NORMAL = 1;
        public static readonly LSLInteger RC_GET_ROOT_KEY = 2;
        public static readonly LSLInteger RC_GET_LINK_NUM = 4;

        public static readonly LSLInteger RCERR_UNKNOWN = -1;
        public static readonly LSLInteger RCERR_SIM_PERF_LOW = -2;
        public static readonly LSLInteger RCERR_CAST_TIME_EXCEEDED = -3;

        public const int KFM_MODE = 1;
        public const int KFM_LOOP = 1;
        public const int KFM_REVERSE = 3;
        public const int KFM_FORWARD = 0;
        public const int KFM_PING_PONG = 2;
        public const int KFM_DATA = 2;
        public const int KFM_TRANSLATION = 2;
        public const int KFM_ROTATION = 1;
        public const int KFM_COMMAND = 0;
        public const int KFM_CMD_PLAY = 0;
        public const int KFM_CMD_STOP = 1;
        public const int KFM_CMD_PAUSE = 2;

        public const string JSON_INVALID = "\uFDD0";
        public const string JSON_OBJECT = "\uFDD1";
        public const string JSON_ARRAY = "\uFDD2";
        public const string JSON_NUMBER = "\uFDD3";
        public const string JSON_STRING = "\uFDD4";
        public const string JSON_NULL = "\uFDD5";
        public const string JSON_TRUE = "\uFDD6";
        public const string JSON_FALSE = "\uFDD7";
        public const string JSON_DELETE = "\uFDD8";
        public const string JSON_APPEND = "-1";

        /// <summary>
        /// process name parameter as regex
        /// </summary>
        public const int OS_LISTEN_REGEX_NAME = 0x1;

        /// <summary>
        /// process message parameter as regex
        /// </summary>
        public const int OS_LISTEN_REGEX_MESSAGE = 0x2;

        // Constants for osTeleportObject

        //ApiDesc osTeleportObject no flags
        public const int OSTPOBJ_NONE           = 0x0;
        //ApiDesc osTeleportObject flag: stop at destination
        public const int OSTPOBJ_STOPATTARGET   = 0x1;
        //ApiDesc osTeleportObject flag: stop at jump point if tp fails
        public const int OSTPOBJ_STOPONFAIL     = 0x2;
        //ApiDesc osTeleportObject flag: the rotation is the final rotation, otherwise is a added rotation
        public const int OSTPOBJ_SETROT         = 0x4;

        //ApiDesc osLocalTeleportAgent no flags
        public const int OS_LTPAG_NONE          = 0x0;
        //ApiDesc osLocalTeleportAgent use velocity
        public const int OS_LTPAG_USEVEL        = 0x1;
        //ApiDesc osLocalTeleportAgent use lookat
        public const int OS_LTPAG_USELOOKAT     = 0x2;
        //ApiDesc osLocalTeleportAgent align lookat to velocity
        public const int OS_LTPAG_ALGNLV        = 0x4;
        //ApiDesc osLocalTeleportAgent force fly
        public const int OS_LTPAG_FORCEFLY      = 0x8;
        //ApiDesc osLocalTeleportAgent force no fly
        public const int OS_LTPAG_FORCENOFLY    = 0x16;

        // Constants for Windlight
        public const int WL_WATER_COLOR = 0;
        public const int WL_WATER_FOG_DENSITY_EXPONENT = 1;
        public const int WL_UNDERWATER_FOG_MODIFIER = 2;
        public const int WL_REFLECTION_WAVELET_SCALE = 3;
        public const int WL_FRESNEL_SCALE = 4;
        public const int WL_FRESNEL_OFFSET = 5;
        public const int WL_REFRACT_SCALE_ABOVE = 6;
        public const int WL_REFRACT_SCALE_BELOW = 7;
        public const int WL_BLUR_MULTIPLIER = 8;
        public const int WL_BIG_WAVE_DIRECTION = 9;
        public const int WL_LITTLE_WAVE_DIRECTION = 10;
        public const int WL_NORMAL_MAP_TEXTURE = 11;
        public const int WL_HORIZON = 12;
        public const int WL_HAZE_HORIZON = 13;
        public const int WL_BLUE_DENSITY = 14;
        public const int WL_HAZE_DENSITY = 15;
        public const int WL_DENSITY_MULTIPLIER = 16;
        public const int WL_DISTANCE_MULTIPLIER = 17;
        public const int WL_MAX_ALTITUDE = 18;
        public const int WL_SUN_MOON_COLOR = 19;
        public const int WL_AMBIENT = 20;
        public const int WL_EAST_ANGLE = 21;
        public const int WL_SUN_GLOW_FOCUS = 22;
        public const int WL_SUN_GLOW_SIZE = 23;
        public const int WL_SCENE_GAMMA = 24;
        public const int WL_STAR_BRIGHTNESS = 25;
        public const int WL_CLOUD_COLOR = 26;
        public const int WL_CLOUD_XY_DENSITY = 27;
        public const int WL_CLOUD_COVERAGE = 28;
        public const int WL_CLOUD_SCALE = 29;
        public const int WL_CLOUD_DETAIL_XY_DENSITY = 30;
        public const int WL_CLOUD_SCROLL_X = 31;
        public const int WL_CLOUD_SCROLL_Y = 32;
        public const int WL_CLOUD_SCROLL_Y_LOCK = 33;
        public const int WL_CLOUD_SCROLL_X_LOCK = 34;
        public const int WL_DRAW_CLASSIC_CLOUDS = 35;
        public const int WL_SUN_MOON_POSITION = 36;

        public const string IMG_USE_BAKED_HEAD    = "5a9f4a74-30f2-821c-b88d-70499d3e7183";
        public const string IMG_USE_BAKED_UPPER   = "ae2de45c-d252-50b8-5c6e-19f39ce79317";
        public const string IMG_USE_BAKED_LOWER   = "24daea5f-0539-cfcf-047f-fbc40b2786ba";
        public const string IMG_USE_BAKED_EYES    = "52cc6bb6-2ee5-e632-d3ad-50197b1dcb8a";
        public const string IMG_USE_BAKED_SKIRT   = "43529ce8-7faa-ad92-165a-bc4078371687";
        public const string IMG_USE_BAKED_HAIR    = "09aac1fb-6bce-0bee-7d44-caac6dbb6c63";
        public const string IMG_USE_BAKED_LEFTARM = "ff62763f-d60a-9855-890b-0c96f8f8cd98";
        public const string IMG_USE_BAKED_LEFTLEG = "8e915e25-31d1-cc95-ae08-d58a47488251";
        public const string IMG_USE_BAKED_AUX1    = "9742065b-19b5-297c-858a-29711d539043";
        public const string IMG_USE_BAKED_AUX2    = "03642e83-2bd1-4eb9-34b4-4c47ed586d2d";
        public const string IMG_USE_BAKED_AUX3    = "edd51b77-fc10-ce7a-4b3d-011dfc349e4f";
    }
}
