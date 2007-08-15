using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.ScriptEngine.DotNetEngine.Compiler;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
{
    public class LSL_BaseClass : LSL_BuiltIn_Commands_Interface
    {
        public string State = "default";
        internal OpenSim.Region.Environment.Scenes.Scene World;
        private System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();


        public void Start(OpenSim.Region.Environment.Scenes.Scene _World, string FullScriptID)
        {
            World = _World;
            Console.WriteLine("ScriptEngine", "LSL_BaseClass.Start() called. FullScriptID: " + FullScriptID);

            return;
        }

//These are the implementations of the various ll-functions used by the LSL scripts.
        //starting out, we use the System.Math library for trig functions. - CFK 8-14-07
        public float llSin(float f) { return (float)Math.Sin(f); }
        public float llCos(float f) { return (float)Math.Cos(f); }
        public float llTan(float f) { return (float)Math.Tan(f); }
        public float llAtan2(float x, float y) { return (float)Math.Atan2(y, x); }
        public float llSqrt(float f) { return (float)Math.Sqrt(f); }
        public float llPow(float fbase, float fexponent) { return (float)Math.Pow(fbase, fexponent); }
        public Int32 llAbs(Int32 i) { return (Int32)Math.Abs(i); }
        public float llFabs(float f) { return (float)Math.Abs(f); }
        public float llFrand(float mag) { return 0; }
        public Int32 llFloor(float f) { return (Int32)Math.Floor(f); }
        public Int32 llCeil(float f) { return (Int32)Math.Ceiling(f); }
        public Int32 llRound(float f) { return (Int32)Math.Round(f, 1); }
        public float llVecMag(Axiom.Math.Vector3 v) { return 0; }
        public Axiom.Math.Vector3 llVecNorm(Axiom.Math.Vector3 v) { return new Axiom.Math.Vector3(); }
        public float llVecDist(Axiom.Math.Vector3 a, Axiom.Math.Vector3 b) { return 0; }
        public Axiom.Math.Vector3 llRot2Euler(Axiom.Math.Quaternion r) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Quaternion llEuler2Rot(Axiom.Math.Vector3 v) { return new Axiom.Math.Quaternion(); }
        public Axiom.Math.Quaternion llAxes2Rot(Axiom.Math.Vector3 fwd, Axiom.Math.Vector3 left, Axiom.Math.Vector3 up) { return new Axiom.Math.Quaternion(); }
        public Axiom.Math.Vector3 llRot2Fwd(Axiom.Math.Quaternion r) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llRot2Left(Axiom.Math.Quaternion r) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llRot2Up(Axiom.Math.Quaternion r) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Quaternion llRotBetween(Axiom.Math.Vector3 start, Axiom.Math.Vector3 end) { return new Axiom.Math.Quaternion(); }
        public void llWhisper(int channelID, string text)
        {
            //Common.SendToDebug("INTERNAL FUNCTION llWhisper(" + channelID + ", \"" + text + "\");");
            Console.WriteLine("llWhisper Channel " + channelID + ", Text: \"" + text + "\"");
            //type for whisper is 0
            //World.SimChat(enc.GetBytes(text), 0, World.Objects[World.ConvertLocalIDToFullID(MY_OBJECT_ID)], MY_OBJECT_NAME, World.Objects[World.ConvertLocalIDToFullID(MY_OBJECT_ID)]);

        }
        //public void llSay(UInt32 channelID, string text)
        public void llSay(int channelID, string text)
        {
            //TODO: DO SOMETHING USEFUL HERE
            //Common.SendToDebug("INTERNAL FUNCTION llSay(" + (UInt32)channelID + ", \"" + (string)text + "\");");
            Console.WriteLine("llSay Channel " + channelID + ", Text: \"" + text + "\"");
            //type for say is 1
            //World.SimChat(enc.GetBytes(text), 1, World.Objects[World.ConvertLocalIDToFullID(MY_OBJECT_ID)], MY_OBJECT_NAME, World.Objects[World.ConvertLocalIDToFullID(MY_OBJECT_ID)]);
        }
        public void llShout(UInt16 channelID, string text) {
            Console.WriteLine("llShout Channel " + channelID + ", Text: \"" + text + "\"");
            //type for shout is 2
            //World.SimChat(enc.GetBytes(text), 2, World.Objects[World.ConvertLocalIDToFullID(MY_OBJECT_ID)], MY_OBJECT_NAME, World.Objects[World.ConvertLocalIDToFullID(MY_OBJECT_ID)]);

        }
        public UInt32 llListen(UInt16 channelID, string name, string ID, string msg) { return 0; }
        public void llListenControl(UInt32 number, UInt32 active) { return; }
        public void llListenRemove(UInt32 number) { return; }
        public void llSensor(string name, string id, UInt32 type, float range, float arc) { return; }
        public void llSensorRepeat(string name, string id, UInt32 type, float range, float arc, float rate) { return; }
        public void llSensorRemove() { return; }
        public string llDetectedName(UInt32 number) { return ""; }
        public string llDetectedKey(UInt32 number) { return ""; }
        public string llDetectedOwner(UInt32 number) { return ""; }
        public UInt32 llDetectedType(UInt32 number) { return 0; }
        public Axiom.Math.Vector3 llDetectedPos(UInt32 number) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llDetectedVel(UInt32 number) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llDetectedGrab(UInt32 number) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Quaternion llDetectedRot(UInt32 number) { return new Axiom.Math.Quaternion(); }
        public UInt32 llDetectedGroup(UInt32 number) { return 0; }
        public UInt32 llDetectedLinkNumber(UInt32 number) { return 0; }
        public void llDie() { return; }
        public float llGround(Axiom.Math.Vector3 offset) { return 0; }
        public float llCloud(Axiom.Math.Vector3 offset) { return 0; }
        public Axiom.Math.Vector3 llWind(Axiom.Math.Vector3 offset) { return new Axiom.Math.Vector3(); }
        public void llSetStatus(UInt32 status, UInt32 value) { return; }
        public UInt32 llGetStatus(UInt32 status) { return 0; }
        public void llSetScale(Axiom.Math.Vector3 scale) { return; }
        public Axiom.Math.Vector3 llGetScale() { return new Axiom.Math.Vector3(); }
        public void llSetColor(Axiom.Math.Vector3 color, UInt32 face) { return; }
        public float llGetAlpha(UInt32 face) { return 0; }
        public void llSetAlpha(float alpha, UInt32 face) { return; }
        public Axiom.Math.Vector3 llGetColor(UInt32 face) { return new Axiom.Math.Vector3(); }
        public void llSetTexture(string texture, UInt32 face) { return; }
        public void llScaleTexture(float u, float v, UInt32 face) { return; }
        public void llOffsetTexture(float u, float v, UInt32 face) { return; }
        public void llRotateTexture(float rotation, UInt32 face) { return; }
        public string llGetTexture(UInt32 face) { return ""; }
        public void llSetPos(Axiom.Math.Vector3 pos) { return; }


        public Axiom.Math.Vector3 llGetPos() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGetLocalPos() { return new Axiom.Math.Vector3(); }
        public void llSetRot(Axiom.Math.Quaternion rot) { }
        public Axiom.Math.Quaternion llGetRot() { return new Axiom.Math.Quaternion(); }
        public Axiom.Math.Quaternion llGetLocalRot() { return new Axiom.Math.Quaternion(); }
        public void llSetForce(Axiom.Math.Vector3 force, Int32 local) { }
        public Axiom.Math.Vector3 llGetForce() { return new Axiom.Math.Vector3(); }
        public Int32 llTarget(Axiom.Math.Vector3 position, float range) { return 0; }
        public void llTargetRemove(Int32 number) { }
        public Int32 llRotTarget(Axiom.Math.Quaternion rot, float error) { return 0; }
        public void llRotTargetRemove(Int32 number) { }
        public void llMoveToTarget(Axiom.Math.Vector3 target, float tau) { }
        public void llStopMoveToTarget() { }
        public void llApplyImpulse(Axiom.Math.Vector3 force, Int32 local) { }
        public void llApplyRotationalImpulse(Axiom.Math.Vector3 force, Int32 local) { }
        public void llSetTorque(Axiom.Math.Vector3 torque, Int32 local) { }
        public Axiom.Math.Vector3 llGetTorque() { return new Axiom.Math.Vector3(); }
        public void llSetForceAndTorque(Axiom.Math.Vector3 force, Axiom.Math.Vector3 torque, Int32 local) { }
        public Axiom.Math.Vector3 llGetVel() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGetAccel() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGetOmega() { return new Axiom.Math.Vector3(); }
        public float llGetTimeOfDay() { return 0; }
        public float llGetWallclock() { return 0; }
        public float llGetTime() { return 0; }
        public void llResetTime() { }
        public float llGetAndResetTime() { return 0; }
        public void llSound() { }
        public void llPlaySound(string sound, float volume) { }
        public void llLoopSound(string sound, float volume) { }
        public void llLoopSoundMaster(string sound, float volume) { }
        public void llLoopSoundSlave(string sound, float volume) { }
        public void llPlaySoundSlave(string sound, float volume) { }
        public void llTriggerSound(string sound, float volume) { }
        public void llStopSound() { }
        public void llPreloadSound(string sound) { }
        public void llGetSubString(string src, Int32 start, Int32 end) { }
        public string llDeleteSubString(string src, Int32 start, Int32 end) { return ""; }
        public void llInsertString(string dst, Int32 position, string src) { }
        public string llToUpper(string source) { return ""; }
        public string llToLower(string source) { return ""; }
        public Int32 llGiveMoney(string destination, Int32 amount) { return 0; }
        public void llMakeExplosion() { }
        public void llMakeFountain() { }
        public void llMakeSmoke() { }
        public void llMakeFire() { }
        public void llRezObject(string inventory, Axiom.Math.Vector3 pos, Axiom.Math.Quaternion rot, Int32 param) { }
        public void llLookAt(Axiom.Math.Vector3 target, float strength, float damping) { }
        public void llStopLookAt() { }
        public void llSetTimerEvent(float sec) { }
        public void llSleep(float sec) { System.Threading.Thread.Sleep((int)(sec * 1000)); }
        public float llGetMass() { return 0; }
        public void llCollisionFilter(string name, string id, Int32 accept) { }
        public void llTakeControls(Int32 controls, Int32 accept, Int32 pass_on) { }
        public void llReleaseControls() { }
        public void llAttachToAvatar(Int32 attachment) { }
        public void llDetachFromAvatar() { }
        public void llTakeCamera() { }
        public void llReleaseCamera() { }
        public string llGetOwner() { return ""; }
        public void llInstantMessage(string user, string message) { }
        public void llEmail(string address, string subject, string message) { }
        public void llGetNextEmail(string address, string subject) { }
        public string llGetKey() { return ""; }
        public void llSetBuoyancy(float buoyancy) { }
        public void llSetHoverHeight(float height, Int32 water, float tau) { }
        public void llStopHover() { }
        public void llMinEventDelay(float delay) { }
        public void llSoundPreload() { }
        public void llRotLookAt(Axiom.Math.Quaternion target, float strength, float damping) { }
        public Int32 llStringLength(string str) { return 0; }
        public void llStartAnimation(string anim) { }
        public void llStopAnimation(string anim) { }
        public void llPointAt() { }
        public void llStopPointAt() { }
        public void llTargetOmega(Axiom.Math.Vector3 axis, float spinrate, float gain) { }
        public Int32 llGetStartParameter() { return 0; }
        public void llGodLikeRezObject(string inventory, Axiom.Math.Vector3 pos) { }
        public void llRequestPermissions(string agent, Int32 perm) { }
        public string llGetPermissionsKey() { return ""; }
        public Int32 llGetPermissions() { return 0; }
        public Int32 llGetLinkNumber() { return 0; }
        public void llSetLinkColor(Int32 linknumber, Axiom.Math.Vector3 color, Int32 face) { }
        public void llCreateLink(string target, Int32 parent) { }
        public void llBreakLink(Int32 linknum) { }
        public void llBreakAllLinks() { }
        public string llGetLinkKey(Int32 linknum) { return ""; }
        public void llGetLinkName(Int32 linknum) { }
        public Int32 llGetInventoryNumber(Int32 type) { return 0; }
        public string llGetInventoryName(Int32 type, Int32 number) { return ""; }
        public void llSetScriptState(string name, Int32 run) { }
        public float llGetEnergy() { return 1.0f; }
        public void llGiveInventory(string destination, string inventory) { }
        public void llRemoveInventory(string item) { }
        public void llSetText(string text, Axiom.Math.Vector3 color, float alpha) { }
        public float llWater(Axiom.Math.Vector3 offset) { return 0; }
        public void llPassTouches(Int32 pass) { }
        public string llRequestAgentData(string id, Int32 data) { return ""; }
        public string llRequestInventoryData(string name) { return ""; }
        public void llSetDamage(float damage) { }
        public void llTeleportAgentHome(string agent) { }
        public void llModifyLand(Int32 action, Int32 brush) { }
        public void llCollisionSound(string impact_sound, float impact_volume) { }
        public void llCollisionSprite(string impact_sprite) { }
        public string llGetAnimation(string id) { return ""; }
        public void llResetScript() { }
        public void llMessageLinked(Int32 linknum, Int32 num, string str, string id) { }
        public void llPushObject(string target, Axiom.Math.Vector3 impulse, Axiom.Math.Vector3 ang_impulse, Int32 local) { }
        public void llPassCollisions(Int32 pass) { }
        public string llGetScriptName() { return ""; }
        public Int32 llGetNumberOfSides() { return 0; }
        public Axiom.Math.Quaternion llAxisAngle2Rot(Axiom.Math.Vector3 axis, float angle) { return new Axiom.Math.Quaternion(); }
        public Axiom.Math.Vector3 llRot2Axis(Axiom.Math.Quaternion rot) { return new Axiom.Math.Vector3(); }
        public void llRot2Angle() { }
        public float llAcos(float val) { return (float)Math.Acos(val); }
        public float llAsin(float val) { return (float)Math.Asin(val); }
        public float llAngleBetween(Axiom.Math.Quaternion a, Axiom.Math.Quaternion b) { return 0; }
        public string llGetInventoryKey(string name) { return ""; }
        public void llAllowInventoryDrop(Int32 add) { }
        public Axiom.Math.Vector3 llGetSunDirection() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGetTextureOffset(Int32 face) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGetTextureScale(Int32 side) { return new Axiom.Math.Vector3(); }
        public float llGetTextureRot(Int32 side) { return 0; }
        public Int32 llSubStringIndex(string source, string pattern) { return 0; }
        public string llGetOwnerKey(string id) { return ""; }
        public Axiom.Math.Vector3 llGetCenterOfMass() { return new Axiom.Math.Vector3(); }
        public List<string> llListSort(List<string> src, Int32 stride, Int32 ascending)
            { return new List<string>(); }
        public Int32 llGetListLength(List<string> src) { return 0; }
        public Int32 llList2Integer(List<string> src, Int32 index) { return 0;}
        public float llList2Float(List<string> src, Int32 index) { return 0; }
        public string llList2String(List<string> src, Int32 index) { return ""; }
        public string llList2Key(List<string> src, Int32 index) { return ""; }
        public Axiom.Math.Vector3 llList2Vector(List<string> src, Int32 index)
            { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Quaternion llList2Rot(List<string> src, Int32 index) 
            { return new Axiom.Math.Quaternion(); }
        public List<string> llList2List(List<string> src, Int32 start, Int32 end) 
            { return new List<string>(); }
        public List<string> llDeleteSubList(List<string> src, Int32 start, Int32 end) 
            { return new List<string>(); }
        public Int32 llGetListEntryType(List<string> src, Int32 index) { return 0; }
        public string llList2CSV(List<string> src) { return ""; }
        public List<string> llCSV2List(string src)
            { return new List<string>(); }
        public List<string> llListRandomize(List<string> src, Int32 stride) 
            { return new List<string>(); }
        public List<string> llList2ListStrided(List<string> src, Int32 start, Int32 end, Int32 stride) 
            { return new List<string>(); }
        public Axiom.Math.Vector3 llGetRegionCorner()
        { return new Axiom.Math.Vector3(); }
        public List<string> llListInsertList(List<string> dest, List<string> src, Int32 start) 
            { return new List<string>(); }
        public Int32 llListFindList(List<string> src, List<string> test) { return 0; }
        public string llGetObjectName() { return ""; }
        public void llSetObjectName(string name) { }
        public string llGetDate() { return ""; }
        public Int32 llEdgeOfWorld(Axiom.Math.Vector3 pos, Axiom.Math.Vector3 dir) { return 0; }
        public Int32 llGetAgentInfo(string id) { return 0; }
        public void llAdjustSoundVolume(float volume) { }
        public void llSetSoundQueueing(Int32 queue) { }
        public void llSetSoundRadius(float radius) { }
        public string llKey2Name(string id) { return ""; }
        public void llSetTextureAnim(Int32 mode, Int32 face, Int32 sizex, Int32 sizey, float start, float length, float rate) { }
        public void llTriggerSoundLimited(string sound, float volume, Axiom.Math.Vector3 top_north_east, Axiom.Math.Vector3 bottom_south_west) { }
        public void llEjectFromLand(string pest) { }
        public void llParseString2List() { }
        public Int32 llOverMyLand(string id) { return 0; }
        public string llGetLandOwnerAt(Axiom.Math.Vector3 pos) { return ""; }
        public string llGetNotecardLine(string name, Int32 line) { return ""; }
        public Axiom.Math.Vector3 llGetAgentSize(string id) { return new Axiom.Math.Vector3(); }
        public Int32 llSameGroup(string agent) { return 0; }
        public void llUnSit(string id) { }
        public Axiom.Math.Vector3 llGroundSlope(Axiom.Math.Vector3 offset) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGroundNormal(Axiom.Math.Vector3 offset) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGroundContour(Axiom.Math.Vector3 offset) { return new Axiom.Math.Vector3(); }
        public Int32 llGetAttached() { return 0; }
        public Int32 llGetFreeMemory() { return 0; }
        public string llGetRegionName() { return World.RegionInfo.RegionName; }
        public float llGetRegionTimeDilation() { return 1.0f; }
        public float llGetRegionFPS() { return 10.0f; }
        public void llParticleSystem(List<Object> rules) { }
        public void llGroundRepel(float height, Int32 water, float tau) { }
        public void llGiveInventoryList() { }
        public void llSetVehicleType(Int32 type) { }
        public void llSetVehicleFloatParam(Int32 param, float value) { }
        public void llSetVehicleVectorParam(Int32 param, Axiom.Math.Vector3 vec) { }
        public void llSetVehicleRotationParam(Int32 param, Axiom.Math.Quaternion rot) { }
        public void llSetVehicleFlags(Int32 flags) { }
        public void llRemoveVehicleFlags(Int32 flags) { }
        public void llSitTarget(Axiom.Math.Vector3 offset, Axiom.Math.Quaternion rot) { }
        public string llAvatarOnSitTarget() { return ""; }
        public void llAddToLandPassList(string avatar, float hours) { }
        public void llSetTouchText(string text)
        {
        }
        public void llSetSitText(string text)
        {
        }
        public void llSetCameraEyeOffset(Axiom.Math.Vector3 offset) { }
        public void llSetCameraAtOffset(Axiom.Math.Vector3 offset) { }
        public void llDumpList2String() { }
        public void llScriptDanger(Axiom.Math.Vector3 pos) { }
        public void llDialog(string avatar, string message, List<string> buttons, Int32 chat_channel) { }
        public void llVolumeDetect(Int32 detect) { }
        public void llResetOtherScript(string name) { }
        public Int32 llGetScriptState(string name) { return 0; }
        public void llRemoteLoadScript() { }
        public void llSetRemoteScriptAccessPin(Int32 pin) { }
        public void llRemoteLoadScriptPin(string target, string name, Int32 pin, Int32 running, Int32 start_param) { }
        public void llOpenRemoteDataChannel() { }
        public string llSendRemoteData(string channel, string dest, Int32 idata, string sdata) { return ""; }
        public void llRemoteDataReply(string channel, string message_id, string sdata, Int32 idata) { }
        public void llCloseRemoteDataChannel(string channel) { }
        public string llMD5String(string src, Int32 nonce) {
            return OpenSim.Framework.Utilities.Util.Md5Hash(src + ":" + nonce.ToString());
        }
        public void llSetPrimitiveParams(List<string> rules) { }
        public string llStringToBase64(string str) { return ""; }
        public string llBase64ToString(string str) { return ""; }
        public void llXorBase64Strings() { }
        public void llRemoteDataSetRegion() { }
        public float llLog10(float val) { return (float)Math.Log10(val); }
        public float llLog(float val) { return (float)Math.Log(val); }
        public List<string> llGetAnimationList(string id) { return new List<string>(); }
        public void llSetParcelMusicURL(string url) { }
        public Axiom.Math.Vector3 llGetRootPosition() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Quaternion llGetRootRotation() { return new Axiom.Math.Quaternion(); }
        public string llGetObjectDesc() { return ""; }
        public void llSetObjectDesc(string desc) { }
        public string llGetCreator() { return ""; }
        public string llGetTimestamp() { return ""; }
        public void llSetLinkAlpha(Int32 linknumber, float alpha, Int32 face) { }
        public Int32 llGetNumberOfPrims() { return 0; }
        public string llGetNumberOfNotecardLines(string name) { return ""; }
        public List<string> llGetBoundingBox(string obj) { return new List<string>(); }
        public Axiom.Math.Vector3 llGetGeometricCenter() { return new Axiom.Math.Vector3(); }
        public void llGetPrimitiveParams() { }
        public string llIntegerToBase64(Int32 number) { return ""; }
        public Int32 llBase64ToInteger(string str) { return 0; }
        public float llGetGMTclock() { return 0; }
        public string llGetSimulatorHostname() { return ""; }
        public void llSetLocalRot(Axiom.Math.Quaternion rot) { }
        public List<string> llParseStringKeepNulls(string src, List<string> seperators, List<string> spacers) 
            { return new List<string>(); }
        public void llRezAtRoot(string inventory, Axiom.Math.Vector3 position, Axiom.Math.Vector3 velocity, Axiom.Math.Quaternion rot, Int32 param) { }
        public Int32 llGetObjectPermMask(Int32 mask) { return 0; }
        public void llSetObjectPermMask(Int32 mask, Int32 value) { }
        public void llGetInventoryPermMask(string item, Int32 mask) { }
        public void llSetInventoryPermMask(string item, Int32 mask, Int32 value) { }
        public string llGetInventoryCreator(string item) { return ""; }
        public void llOwnerSay(string msg) { }
        public void llRequestSimulatorData(string simulator, Int32 data) { }
        public void llForceMouselook(Int32 mouselook) { }
        public float llGetObjectMass(string id) { return 0; }
        public void llListReplaceList() { }
        public void llLoadURL(string avatar_id, string message, string url) { }
        public void llParcelMediaCommandList(List<string> commandList) { }
        public void llParcelMediaQuery() { }

        public Int32 llModPow(Int32 a, Int32 b, Int32 c) {
            Int64 tmp = 0;
            Int64 val = Math.DivRem(Convert.ToInt64(Math.Pow(a, b)), c, out tmp);

            return Convert.ToInt32(tmp);
        }

        public Int32 llGetInventoryType(string name) { return 0; }
        public void llSetPayPrice(Int32 price, List<string> quick_pay_buttons) { }
        public Axiom.Math.Vector3 llGetCameraPos() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Quaternion llGetCameraRot() { return new Axiom.Math.Quaternion(); }
        public void llSetPrimURL() { }
        public void llRefreshPrimURL() { }
        public string llEscapeURL(string url) { return ""; }
        public string llUnescapeURL(string url) { return ""; }
        public void llMapDestination(string simname, Axiom.Math.Vector3 pos, Axiom.Math.Vector3 look_at) { }
        public void llAddToLandBanList(string avatar, float hours) { }
        public void llRemoveFromLandPassList(string avatar) { }
        public void llRemoveFromLandBanList(string avatar) { }
        public void llSetCameraParams(List<string> rules) { }
        public void llClearCameraParams() { }
        public float llListStatistics(Int32 operation, List<string> src) { return 0; }
        public Int32 llGetUnixTime() {
            return OpenSim.Framework.Utilities.Util.UnixTimeSinceEpoch();
        }
        public Int32 llGetParcelFlags(Axiom.Math.Vector3 pos) { return 0; }
        public Int32 llGetRegionFlags() { return 0; }
        public string llXorBase64StringsCorrect(string str1, string str2) { return ""; }
        public void llHTTPRequest() { }
        public void llResetLandBanList() { }
        public void llResetLandPassList() { }
        public Int32 llGetParcelPrimCount(Axiom.Math.Vector3 pos, Int32 category, Int32 sim_wide) { return 0; }
        public List<string> llGetParcelPrimOwners(Axiom.Math.Vector3 pos) { return new List<string>(); }
        public Int32 llGetObjectPrimCount(string object_id) { return 0; }
        public Int32 llGetParcelMaxPrims(Axiom.Math.Vector3 pos, Int32 sim_wide) { return 0; }
        public List<string> llGetParcelDetails(Axiom.Math.Vector3 pos, List<string> param) { return new List<string>(); }

    }
}
