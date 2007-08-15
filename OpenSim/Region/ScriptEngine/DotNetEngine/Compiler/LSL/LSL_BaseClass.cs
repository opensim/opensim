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
        public double llSin(double f) { return (double)Math.Sin(f); }
        public double llCos(double f) { return (double)Math.Cos(f); }
        public double llTan(double f) { return (double)Math.Tan(f); }
        public double llAtan2(double x, double y) { return (double)Math.Atan2(y, x); }
        public double llSqrt(double f) { return (double)Math.Sqrt(f); }
        public double llPow(double fbase, double fexponent) { return (double)Math.Pow(fbase, fexponent); }
        public int llAbs(int i) { return (int)Math.Abs(i); }
        public double llFabs(double f) { return (double)Math.Abs(f); }
        public double llFrand(double mag) { return 0; }
        public int llFloor(double f) { return (int)Math.Floor(f); }
        public int llCeil(double f) { return (int)Math.Ceiling(f); }
        public int llRound(double f) { return (int)Math.Round(f, 1); }
        public double llVecMag(Axiom.Math.Vector3 v) { return 0; }
        public Axiom.Math.Vector3 llVecNorm(Axiom.Math.Vector3 v) { return new Axiom.Math.Vector3(); }
        public double llVecDist(Axiom.Math.Vector3 a, Axiom.Math.Vector3 b) { return 0; }
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
        //public void llSay(int channelID, string text)
        public void llSay(int channelID, string text)
        {
            //TODO: DO SOMETHING USEFUL HERE
            //Common.SendToDebug("INTERNAL FUNCTION llSay(" + (int)channelID + ", \"" + (string)text + "\");");
            Console.WriteLine("llSay Channel " + channelID + ", Text: \"" + text + "\"");
            //type for say is 1
            //World.SimChat(enc.GetBytes(text), 1, World.Objects[World.ConvertLocalIDToFullID(MY_OBJECT_ID)], MY_OBJECT_NAME, World.Objects[World.ConvertLocalIDToFullID(MY_OBJECT_ID)]);
        }
        public void llShout(int channelID, string text)
        {
            Console.WriteLine("llShout Channel " + channelID + ", Text: \"" + text + "\"");
            //type for shout is 2
            //World.SimChat(enc.GetBytes(text), 2, World.Objects[World.ConvertLocalIDToFullID(MY_OBJECT_ID)], MY_OBJECT_NAME, World.Objects[World.ConvertLocalIDToFullID(MY_OBJECT_ID)]);

        }
        public int llListen(int channelID, string name, string ID, string msg) { return 0; }
        public void llListenControl(int number, int active) { return; }
        public void llListenRemove(int number) { return; }
        public void llSensor(string name, string id, int type, double range, double arc) { return; }
        public void llSensorRepeat(string name, string id, int type, double range, double arc, double rate) { return; }
        public void llSensorRemove() { return; }
        public string llDetectedName(int number) { return ""; }
        public string llDetectedKey(int number) { return ""; }
        public string llDetectedOwner(int number) { return ""; }
        public int llDetectedType(int number) { return 0; }
        public Axiom.Math.Vector3 llDetectedPos(int number) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llDetectedVel(int number) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llDetectedGrab(int number) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Quaternion llDetectedRot(int number) { return new Axiom.Math.Quaternion(); }
        public int llDetectedGroup(int number) { return 0; }
        public int llDetectedLinkNumber(int number) { return 0; }
        public void llDie() { return; }
        public double llGround(Axiom.Math.Vector3 offset) { return 0; }
        public double llCloud(Axiom.Math.Vector3 offset) { return 0; }
        public Axiom.Math.Vector3 llWind(Axiom.Math.Vector3 offset) { return new Axiom.Math.Vector3(); }
        public void llSetStatus(int status, int value) { return; }
        public int llGetStatus(int status) { return 0; }
        public void llSetScale(Axiom.Math.Vector3 scale) { return; }
        public Axiom.Math.Vector3 llGetScale() { return new Axiom.Math.Vector3(); }
        public void llSetColor(Axiom.Math.Vector3 color, int face) { return; }
        public double llGetAlpha(int face) { return 0; }
        public void llSetAlpha(double alpha, int face) { return; }
        public Axiom.Math.Vector3 llGetColor(int face) { return new Axiom.Math.Vector3(); }
        public void llSetTexture(string texture, int face) { return; }
        public void llScaleTexture(double u, double v, int face) { return; }
        public void llOffsetTexture(double u, double v, int face) { return; }
        public void llRotateTexture(double rotation, int face) { return; }
        public string llGetTexture(int face) { return ""; }
        public void llSetPos(Axiom.Math.Vector3 pos) { return; }


        public Axiom.Math.Vector3 llGetPos() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGetLocalPos() { return new Axiom.Math.Vector3(); }
        public void llSetRot(Axiom.Math.Quaternion rot) { }
        public Axiom.Math.Quaternion llGetRot() { return new Axiom.Math.Quaternion(); }
        public Axiom.Math.Quaternion llGetLocalRot() { return new Axiom.Math.Quaternion(); }
        public void llSetForce(Axiom.Math.Vector3 force, int local) { }
        public Axiom.Math.Vector3 llGetForce() { return new Axiom.Math.Vector3(); }
        public int llTarget(Axiom.Math.Vector3 position, double range) { return 0; }
        public void llTargetRemove(int number) { }
        public int llRotTarget(Axiom.Math.Quaternion rot, double error) { return 0; }
        public void llRotTargetRemove(int number) { }
        public void llMoveToTarget(Axiom.Math.Vector3 target, double tau) { }
        public void llStopMoveToTarget() { }
        public void llApplyImpulse(Axiom.Math.Vector3 force, int local) { }
        public void llApplyRotationalImpulse(Axiom.Math.Vector3 force, int local) { }
        public void llSetTorque(Axiom.Math.Vector3 torque, int local) { }
        public Axiom.Math.Vector3 llGetTorque() { return new Axiom.Math.Vector3(); }
        public void llSetForceAndTorque(Axiom.Math.Vector3 force, Axiom.Math.Vector3 torque, int local) { }
        public Axiom.Math.Vector3 llGetVel() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGetAccel() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGetOmega() { return new Axiom.Math.Vector3(); }
        public double llGetTimeOfDay() { return 0; }
        public double llGetWallclock() { return 0; }
        public double llGetTime() { return 0; }
        public void llResetTime() { }
        public double llGetAndResetTime() { return 0; }
        public void llSound() { }
        public void llPlaySound(string sound, double volume) { }
        public void llLoopSound(string sound, double volume) { }
        public void llLoopSoundMaster(string sound, double volume) { }
        public void llLoopSoundSlave(string sound, double volume) { }
        public void llPlaySoundSlave(string sound, double volume) { }
        public void llTriggerSound(string sound, double volume) { }
        public void llStopSound() { }
        public void llPreloadSound(string sound) { }
        public void llGetSubString(string src, int start, int end) { }
        public string llDeleteSubString(string src, int start, int end) { return ""; }
        public void llInsertString(string dst, int position, string src) { }
        public string llToUpper(string source) { return ""; }
        public string llToLower(string source) { return ""; }
        public int llGiveMoney(string destination, int amount) { return 0; }
        public void llMakeExplosion() { }
        public void llMakeFountain() { }
        public void llMakeSmoke() { }
        public void llMakeFire() { }
        public void llRezObject(string inventory, Axiom.Math.Vector3 pos, Axiom.Math.Quaternion rot, int param) { }
        public void llLookAt(Axiom.Math.Vector3 target, double strength, double damping) { }
        public void llStopLookAt() { }
        public void llSetTimerEvent(double sec) { }
        public void llSleep(double sec) { System.Threading.Thread.Sleep((int)(sec * 1000)); }
        public double llGetMass() { return 0; }
        public void llCollisionFilter(string name, string id, int accept) { }
        public void llTakeControls(int controls, int accept, int pass_on) { }
        public void llReleaseControls() { }
        public void llAttachToAvatar(int attachment) { }
        public void llDetachFromAvatar() { }
        public void llTakeCamera() { }
        public void llReleaseCamera() { }
        public string llGetOwner() { return ""; }
        public void llInstantMessage(string user, string message) { }
        public void llEmail(string address, string subject, string message) { }
        public void llGetNextEmail(string address, string subject) { }
        public string llGetKey() { return ""; }
        public void llSetBuoyancy(double buoyancy) { }
        public void llSetHoverHeight(double height, int water, double tau) { }
        public void llStopHover() { }
        public void llMinEventDelay(double delay) { }
        public void llSoundPreload() { }
        public void llRotLookAt(Axiom.Math.Quaternion target, double strength, double damping) { }
        public int llStringLength(string str) { return 0; }
        public void llStartAnimation(string anim) { }
        public void llStopAnimation(string anim) { }
        public void llPointAt() { }
        public void llStopPointAt() { }
        public void llTargetOmega(Axiom.Math.Vector3 axis, double spinrate, double gain) { }
        public int llGetStartParameter() { return 0; }
        public void llGodLikeRezObject(string inventory, Axiom.Math.Vector3 pos) { }
        public void llRequestPermissions(string agent, int perm) { }
        public string llGetPermissionsKey() { return ""; }
        public int llGetPermissions() { return 0; }
        public int llGetLinkNumber() { return 0; }
        public void llSetLinkColor(int linknumber, Axiom.Math.Vector3 color, int face) { }
        public void llCreateLink(string target, int parent) { }
        public void llBreakLink(int linknum) { }
        public void llBreakAllLinks() { }
        public string llGetLinkKey(int linknum) { return ""; }
        public void llGetLinkName(int linknum) { }
        public int llGetInventoryNumber(int type) { return 0; }
        public string llGetInventoryName(int type, int number) { return ""; }
        public void llSetScriptState(string name, int run) { }
        public double llGetEnergy() { return 1.0f; }
        public void llGiveInventory(string destination, string inventory) { }
        public void llRemoveInventory(string item) { }
        public void llSetText(string text, Axiom.Math.Vector3 color, double alpha) { }
        public double llWater(Axiom.Math.Vector3 offset) { return 0; }
        public void llPassTouches(int pass) { }
        public string llRequestAgentData(string id, int data) { return ""; }
        public string llRequestInventoryData(string name) { return ""; }
        public void llSetDamage(double damage) { }
        public void llTeleportAgentHome(string agent) { }
        public void llModifyLand(int action, int brush) { }
        public void llCollisionSound(string impact_sound, double impact_volume) { }
        public void llCollisionSprite(string impact_sprite) { }
        public string llGetAnimation(string id) { return ""; }
        public void llResetScript() { }
        public void llMessageLinked(int linknum, int num, string str, string id) { }
        public void llPushObject(string target, Axiom.Math.Vector3 impulse, Axiom.Math.Vector3 ang_impulse, int local) { }
        public void llPassCollisions(int pass) { }
        public string llGetScriptName() { return ""; }
        public int llGetNumberOfSides() { return 0; }
        public Axiom.Math.Quaternion llAxisAngle2Rot(Axiom.Math.Vector3 axis, double angle) { return new Axiom.Math.Quaternion(); }
        public Axiom.Math.Vector3 llRot2Axis(Axiom.Math.Quaternion rot) { return new Axiom.Math.Vector3(); }
        public void llRot2Angle() { }
        public double llAcos(double val) { return (double)Math.Acos(val); }
        public double llAsin(double val) { return (double)Math.Asin(val); }
        public double llAngleBetween(Axiom.Math.Quaternion a, Axiom.Math.Quaternion b) { return 0; }
        public string llGetInventoryKey(string name) { return ""; }
        public void llAllowInventoryDrop(int add) { }
        public Axiom.Math.Vector3 llGetSunDirection() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGetTextureOffset(int face) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGetTextureScale(int side) { return new Axiom.Math.Vector3(); }
        public double llGetTextureRot(int side) { return 0; }
        public int llSubStringIndex(string source, string pattern) { return 0; }
        public string llGetOwnerKey(string id) { return ""; }
        public Axiom.Math.Vector3 llGetCenterOfMass() { return new Axiom.Math.Vector3(); }
        public List<string> llListSort(List<string> src, int stride, int ascending)
        { return new List<string>(); }
        public int llGetListLength(List<string> src) { return 0; }
        public int llList2Integer(List<string> src, int index) { return 0; }
        public double llList2double(List<string> src, int index) { return 0; }
        public string llList2String(List<string> src, int index) { return ""; }
        public string llList2Key(List<string> src, int index) { return ""; }
        public Axiom.Math.Vector3 llList2Vector(List<string> src, int index)
        { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Quaternion llList2Rot(List<string> src, int index)
        { return new Axiom.Math.Quaternion(); }
        public List<string> llList2List(List<string> src, int start, int end)
        { return new List<string>(); }
        public List<string> llDeleteSubList(List<string> src, int start, int end)
        { return new List<string>(); }
        public int llGetListEntryType(List<string> src, int index) { return 0; }
        public string llList2CSV(List<string> src) { return ""; }
        public List<string> llCSV2List(string src)
        { return new List<string>(); }
        public List<string> llListRandomize(List<string> src, int stride)
        { return new List<string>(); }
        public List<string> llList2ListStrided(List<string> src, int start, int end, int stride)
        { return new List<string>(); }
        public Axiom.Math.Vector3 llGetRegionCorner()
        { return new Axiom.Math.Vector3(); }
        public List<string> llListInsertList(List<string> dest, List<string> src, int start)
        { return new List<string>(); }
        public int llListFindList(List<string> src, List<string> test) { return 0; }
        public string llGetObjectName() { return ""; }
        public void llSetObjectName(string name) { }
        public string llGetDate() { return ""; }
        public int llEdgeOfWorld(Axiom.Math.Vector3 pos, Axiom.Math.Vector3 dir) { return 0; }
        public int llGetAgentInfo(string id) { return 0; }
        public void llAdjustSoundVolume(double volume) { }
        public void llSetSoundQueueing(int queue) { }
        public void llSetSoundRadius(double radius) { }
        public string llKey2Name(string id) { return ""; }
        public void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate) { }
        public void llTriggerSoundLimited(string sound, double volume, Axiom.Math.Vector3 top_north_east, Axiom.Math.Vector3 bottom_south_west) { }
        public void llEjectFromLand(string pest) { }
        public void llParseString2List() { }
        public int llOverMyLand(string id) { return 0; }
        public string llGetLandOwnerAt(Axiom.Math.Vector3 pos) { return ""; }
        public string llGetNotecardLine(string name, int line) { return ""; }
        public Axiom.Math.Vector3 llGetAgentSize(string id) { return new Axiom.Math.Vector3(); }
        public int llSameGroup(string agent) { return 0; }
        public void llUnSit(string id) { }
        public Axiom.Math.Vector3 llGroundSlope(Axiom.Math.Vector3 offset) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGroundNormal(Axiom.Math.Vector3 offset) { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Vector3 llGroundContour(Axiom.Math.Vector3 offset) { return new Axiom.Math.Vector3(); }
        public int llGetAttached() { return 0; }
        public int llGetFreeMemory() { return 0; }
        public string llGetRegionName() { return World.RegionInfo.RegionName; }
        public double llGetRegionTimeDilation() { return 1.0f; }
        public double llGetRegionFPS() { return 10.0f; }
        public void llParticleSystem(List<Object> rules) { }
        public void llGroundRepel(double height, int water, double tau) { }
        public void llGiveInventoryList() { }
        public void llSetVehicleType(int type) { }
        public void llSetVehicledoubleParam(int param, double value) { }
        public void llSetVehicleVectorParam(int param, Axiom.Math.Vector3 vec) { }
        public void llSetVehicleRotationParam(int param, Axiom.Math.Quaternion rot) { }
        public void llSetVehicleFlags(int flags) { }
        public void llRemoveVehicleFlags(int flags) { }
        public void llSitTarget(Axiom.Math.Vector3 offset, Axiom.Math.Quaternion rot) { }
        public string llAvatarOnSitTarget() { return ""; }
        public void llAddToLandPassList(string avatar, double hours) { }
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
        public void llDialog(string avatar, string message, List<string> buttons, int chat_channel) { }
        public void llVolumeDetect(int detect) { }
        public void llResetOtherScript(string name) { }
        public int llGetScriptState(string name) { return 0; }
        public void llRemoteLoadScript() { }
        public void llSetRemoteScriptAccessPin(int pin) { }
        public void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param) { }
        public void llOpenRemoteDataChannel() { }
        public string llSendRemoteData(string channel, string dest, int idata, string sdata) { return ""; }
        public void llRemoteDataReply(string channel, string message_id, string sdata, int idata) { }
        public void llCloseRemoteDataChannel(string channel) { }
        public string llMD5String(string src, int nonce)
        {
            return OpenSim.Framework.Utilities.Util.Md5Hash(src + ":" + nonce.ToString());
        }
        public void llSetPrimitiveParams(List<string> rules) { }
        public string llStringToBase64(string str) { return ""; }
        public string llBase64ToString(string str) { return ""; }
        public void llXorBase64Strings() { }
        public void llRemoteDataSetRegion() { }
        public double llLog10(double val) { return (double)Math.Log10(val); }
        public double llLog(double val) { return (double)Math.Log(val); }
        public List<string> llGetAnimationList(string id) { return new List<string>(); }
        public void llSetParcelMusicURL(string url) { }
        public Axiom.Math.Vector3 llGetRootPosition() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Quaternion llGetRootRotation() { return new Axiom.Math.Quaternion(); }
        public string llGetObjectDesc() { return ""; }
        public void llSetObjectDesc(string desc) { }
        public string llGetCreator() { return ""; }
        public string llGetTimestamp() { return ""; }
        public void llSetLinkAlpha(int linknumber, double alpha, int face) { }
        public int llGetNumberOfPrims() { return 0; }
        public string llGetNumberOfNotecardLines(string name) { return ""; }
        public List<string> llGetBoundingBox(string obj) { return new List<string>(); }
        public Axiom.Math.Vector3 llGetGeometricCenter() { return new Axiom.Math.Vector3(); }
        public void llGetPrimitiveParams() { }
        public string llIntegerToBase64(int number) { return ""; }
        public int llBase64ToInteger(string str) { return 0; }
        public double llGetGMTclock() { return 0; }
        public string llGetSimulatorHostname() { return ""; }
        public void llSetLocalRot(Axiom.Math.Quaternion rot) { }
        public List<string> llParseStringKeepNulls(string src, List<string> seperators, List<string> spacers)
        { return new List<string>(); }
        public void llRezAtRoot(string inventory, Axiom.Math.Vector3 position, Axiom.Math.Vector3 velocity, Axiom.Math.Quaternion rot, int param) { }
        public int llGetObjectPermMask(int mask) { return 0; }
        public void llSetObjectPermMask(int mask, int value) { }
        public void llGetInventoryPermMask(string item, int mask) { }
        public void llSetInventoryPermMask(string item, int mask, int value) { }
        public string llGetInventoryCreator(string item) { return ""; }
        public void llOwnerSay(string msg) { }
        public void llRequestSimulatorData(string simulator, int data) { }
        public void llForceMouselook(int mouselook) { }
        public double llGetObjectMass(string id) { return 0; }
        public void llListReplaceList() { }
        public void llLoadURL(string avatar_id, string message, string url) { }
        public void llParcelMediaCommandList(List<string> commandList) { }
        public void llParcelMediaQuery() { }

        public int llModPow(int a, int b, int c)
        {
            Int64 tmp = 0;
            Int64 val = Math.DivRem(Convert.ToInt64(Math.Pow(a, b)), c, out tmp);

            return Convert.ToInt32(tmp);
        }

        public int llGetInventoryType(string name) { return 0; }
        public void llSetPayPrice(int price, List<string> quick_pay_buttons) { }
        public Axiom.Math.Vector3 llGetCameraPos() { return new Axiom.Math.Vector3(); }
        public Axiom.Math.Quaternion llGetCameraRot() { return new Axiom.Math.Quaternion(); }
        public void llSetPrimURL() { }
        public void llRefreshPrimURL() { }
        public string llEscapeURL(string url) { return ""; }
        public string llUnescapeURL(string url) { return ""; }
        public void llMapDestination(string simname, Axiom.Math.Vector3 pos, Axiom.Math.Vector3 look_at) { }
        public void llAddToLandBanList(string avatar, double hours) { }
        public void llRemoveFromLandPassList(string avatar) { }
        public void llRemoveFromLandBanList(string avatar) { }
        public void llSetCameraParams(List<string> rules) { }
        public void llClearCameraParams() { }
        public double llListStatistics(int operation, List<string> src) { return 0; }
        public int llGetUnixTime()
        {
            return OpenSim.Framework.Utilities.Util.UnixTimeSinceEpoch();
        }
        public int llGetParcelFlags(Axiom.Math.Vector3 pos) { return 0; }
        public int llGetRegionFlags() { return 0; }
        public string llXorBase64StringsCorrect(string str1, string str2) { return ""; }
        public void llHTTPRequest() { }
        public void llResetLandBanList() { }
        public void llResetLandPassList() { }
        public int llGetParcelPrimCount(Axiom.Math.Vector3 pos, int category, int sim_wide) { return 0; }
        public List<string> llGetParcelPrimOwners(Axiom.Math.Vector3 pos) { return new List<string>(); }
        public int llGetObjectPrimCount(string object_id) { return 0; }
        public int llGetParcelMaxPrims(Axiom.Math.Vector3 pos, int sim_wide) { return 0; }
        public List<string> llGetParcelDetails(Axiom.Math.Vector3 pos, List<string> param) { return new List<string>(); }




        // LSL CONSTANTS
        public const int TRUE = 1;
        public const int FALSE = 0;
        public const int STATUS_PHYSICS = 1;
        public const int STATUS_ROTATE_X = 2;
        public const int STATUS_ROTATE_Y = 4;
        public const int STATUS_ROTATE_Z = 8;
        public const int STATUS_PHANTOM = 16;
        public const int STATUS_SANDBOX = 32;
        public const int STATUS_BLOCK_GRAB = 64;
        public const int STATUS_DIE_AT_EDGE = 128;
        public const int STATUS_RETURN_AT_EDGE = 256;
        public const int AGENT = 1;
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
        public const int AGENT_FLYING = 1;
        public const int AGENT_ATTACHMENTS = 2;
        public const int AGENT_SCRIPTED = 4;
        public const int AGENT_MOUSELOOK = 8;
        public const int AGENT_SITTING = 16;
        public const int AGENT_ON_OBJECT = 32;
        public const int AGENT_AWAY = 64;
        public const int AGENT_WALKING = 128;
        public const int AGENT_IN_AIR = 256;
        public const int AGENT_TYPING = 512;
        public const int AGENT_CROUCHING = 1024;
        public const int AGENT_BUSY = 2048;
        public const int AGENT_ALWAYS_RUN = 4096;
        public const int PSYS_PART_INTERP_COLOR_MASK = 1;
        public const int PSYS_PART_INTERP_SCALE_MASK = 2;
        public const int PSYS_PART_BOUNCE_MASK = 4;
        public const int PSYS_PART_WIND_MASK = 8;
        public const int PSYS_PART_FOLLOW_SRC_MASK = 16;
        public const int PSYS_PART_FOLLOW_VELOCITY_MASK = 32;
        public const int PSYS_PART_TARGET_POS_MASK = 64;
        public const int PSYS_PART_TARGET_LINEAR_MASK = 128;
        public const int PSYS_PART_EMISSIVE_MASK = 256;
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
        public const int VEHICLE_FLAG_NO_DEFLECTION_UP = 1;
        public const int VEHICLE_FLAG_LIMIT_ROLL_ONLY = 2;
        public const int VEHICLE_FLAG_HOVER_WATER_ONLY = 4;
        public const int VEHICLE_FLAG_HOVER_TERRAIN_ONLY = 8;
        public const int VEHICLE_FLAG_HOVER_GLOBAL_HEIGHT = 16;
        public const int VEHICLE_FLAG_HOVER_UP_ONLY = 32;
        public const int VEHICLE_FLAG_LIMIT_MOTOR_UP = 64;
        public const int VEHICLE_FLAG_MOUSELOOK_STEER = 128;
        public const int VEHICLE_FLAG_MOUSELOOK_BANK = 256;
        public const int VEHICLE_FLAG_CAMERA_DECOUPLED = 512;
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
        public const int LAND_LEVEL = 0;
        public const int LAND_RAISE = 1;
        public const int LAND_LOWER = 2;
        public const int LAND_SMOOTH = 3;
        public const int LAND_NOISE = 4;
        public const int LAND_REVERT = 5;
        public const int LAND_SMALL_BRUSH = 1;
        public const int LAND_MEDIUM_BRUSH = 2;
        public const int LAND_LARGE_BRUSH = 3;
        public const int DATA_ONLINE = 1;
        public const int DATA_NAME = 2;
        public const int DATA_BORN = 3;
        public const int DATA_RATING = 4;
        public const int DATA_SIM_POS = 5;
        public const int DATA_SIM_STATUS = 6;
        public const int DATA_SIM_RATING = 7;
        public const int ANIM_ON = 1;
        public const int LOOP = 2;
        public const int REVERSE = 4;
        public const int PING_PONG = 8;
        public const int SMOOTH = 16;
        public const int ROTATE = 32;
        public const int SCALE = 64;
        public const int ALL_SIDES = -1;
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
        public const int TYPE_INVALID = 0;
        public const int TYPE_INTEGER = 1;
        public const int TYPE_double = 2;
        public const int TYPE_STRING = 3;
        public const int TYPE_KEY = 4;
        public const int TYPE_VECTOR = 5;
        public const int TYPE_ROTATION = 6;
        public const int REMOTE_DATA_CHANNEL = 1;
        public const int REMOTE_DATA_REQUEST = 2;
        public const int REMOTE_DATA_REPLY = 3;
        //public const int PRIM_TYPE = 1;
        public const int PRIM_MATERIAL = 2;
        public const int PRIM_PHYSICS = 3;
        public const int PRIM_TEMP_ON_REZ = 4;
        public const int PRIM_PHANTOM = 5;
        public const int PRIM_POSITION = 6;
        public const int PRIM_SIZE = 7;
        public const int PRIM_ROTATION = 8;
        public const int PRIM_TYPE = 9;
        public const int PRIM_TEXTURE = 17;
        public const int PRIM_COLOR = 18;
        public const int PRIM_BUMP_SHINY = 19;
        public const int PRIM_FULLBRIGHT = 20;
        public const int PRIM_FLEXIBLE = 21;
        public const int PRIM_TEXGEN = 22;
        public const int PRIM_TEXGEN_DEFAULT = 0;
        public const int PRIM_TEXGEN_PLANAR = 1;
        public const int PRIM_TYPE_BOX = 0;
        public const int PRIM_TYPE_CYLINDER = 1;
        public const int PRIM_TYPE_PRISM = 2;
        public const int PRIM_TYPE_SPHERE = 3;
        public const int PRIM_TYPE_TORUS = 4;
        public const int PRIM_TYPE_TUBE = 5;
        public const int PRIM_TYPE_RING = 6;
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
        public const int PAY_HIDE = -1;
        public const int PAY_DEFAULT = -2;
        public const string NULL_KEY = "00000000-0000-0000-0000-000000000000";
        public const string EOF = "\n\n\n";
        public const double PI = 3.14159274f;
        public const double TWO_PI = 6.28318548f;
        public const double PI_BY_TWO = 1.57079637f;
        public const double DEG_TO_RAD = 0.01745329238f;
        public const double RAD_TO_DEG = 57.29578f;
        public const double SQRT2 = 1.414213538f;

        // Can not be public const?
        public Axiom.Math.Vector3 ZERO_VECTOR = new Axiom.Math.Vector3(0, 0, 0);
        public Axiom.Math.Quaternion ZERO_ROTATION = new Axiom.Math.Quaternion(0, 0, 0, 0);



    }
}
