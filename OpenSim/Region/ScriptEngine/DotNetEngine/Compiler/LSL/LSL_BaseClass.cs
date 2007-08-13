using System;
using System.Collections.Generic;
using System.Text;
using OpenSim.Region.ScriptEngine.DotNetEngine.Compiler;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
{
    public class LSL_BaseClass : LSL_BuiltIn_Commands_Interface
    {
        public UInt32 State = 0;
        internal OpenSim.Region.Environment.Scenes.Scene World;

        public void Start(OpenSim.Region.Environment.Scenes.Scene _World, string FullScriptID)
        {
            World = _World;
            Common.SendToLog("OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL.LSL_BaseClass.Start() called. FullScriptID: " + FullScriptID);

            return;
        }

        //
        // IMPLEMENT THESE!
        //



        public float llSin(float f) { return 0; }
        public float llCos(float f) { return 0; }
        public float llTan(float f) { return 0; }
        public float llAtan2(float x, float y) { return 0; }
        public float llSqrt(float f) { return 0; }
        public float llPow(float fbase, float fexponent) { return 0; }
        public UInt32 llAbs(Int32 i) { return 0; }
        public float llFabs(float f) { return 0; }
        public float llFrand(float mag) { return 0; }
        public UInt32 llFloor(float f) { return 0; }
        public UInt32 llCeil(float f) { return 0; }
        public UInt32 llRound(float f) { return 0; }
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
        public void llWhisper(UInt16 channelID, string text)
        {
            Common.SendToDebug("INTERNAL FUNCTION llWhisper(" + channelID + ", \"" + text + "\");");
            Common.SendToLog("llWhisper Channel " + channelID + ", Text: \"" + text + "\"");
        }
        //public void llSay(UInt32 channelID, string text)
        public void llSay(object channelID, object text)
        {
            //TODO: DO SOMETHING USEFUL HERE
            Common.SendToDebug("INTERNAL FUNCTION llSay(" + (UInt32)channelID + ", \"" + (string)text + "\");");
            Common.SendToLog("llSay Channel " + (UInt32)channelID + ", Text: \"" + (string)text + "\"");
        }
        public void llShout(UInt16 channelID, string text) { return; }
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
        public void llSleep(float sec) { }
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
        public float llGetEnergy() { return 0; }
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
        public float llAcos(float val) { return 0; }
        public float llAsin(float val) { return 0; }
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
        public void llListSort() { }
        public void llGetListLength() { }
        public void llList2Integer() { }
        public void llList2Float() { }
        public void llList2String() { }
        public void llList2Key() { }
        public void llList2Vector() { }
        public void llList2Rot() { }
        public void llList2List() { }
        public void llDeleteSubList() { }
        public void llGetListEntryType() { }
        public void llList2CSV() { }
        public void llCSV2List() { }
        public void llListRandomize() { }
        public void llList2ListStrided() { }
        public void llGetRegionCorner() { }
        public void llListInsertList() { }
        public void llListFindList() { }
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
        public void llGetAgentSize() { }
        public void llSameGroup() { }
        public void llUnSit() { }
        public void llGroundSlope() { }
        public void llGroundNormal() { }
        public void llGroundContour() { }
        public void llGetAttached() { }
        public void llGetFreeMemory() { }
        public void llGetRegionName() { }
        public void llGetRegionTimeDilation() { }
        public void llGetRegionFPS() { }
        public void llParticleSystem() { }
        public void llGroundRepel() { }
        public void llGiveInventoryList() { }
        public void llSetVehicleType() { }
        public void llSetVehicleFloatParam() { }
        public void llSetVehicleVectorParam() { }
        public void llSetVehicleRotationParam() { }
        public void llSetVehicleFlags() { }
        public void llRemoveVehicleFlags() { }
        public void llSitTarget() { }
        public void llAvatarOnSitTarget() { }
        public void llAddToLandPassList() { }
        public void llSetTouchText() { }
        public void llSetSitText() { }
        public void llSetCameraEyeOffset() { }
        public void llSetCameraAtOffset() { }
        public void llDumpList2String() { }
        public void llScriptDanger() { }
        public void llDialog() { }
        public void llVolumeDetect() { }
        public void llResetOtherScript() { }
        public void llGetScriptState() { }
        public void llRemoteLoadScript() { }
        public void llSetRemoteScriptAccessPin() { }
        public void llRemoteLoadScriptPin() { }
        public void llOpenRemoteDataChannel() { }
        public void llSendRemoteData() { }
        public void llRemoteDataReply() { }
        public void llCloseRemoteDataChannel() { }
        public void llMD5String() { }
        public void llSetPrimitiveParams() { }
        public void llStringToBase64() { }
        public void llBase64ToString() { }
        public void llXorBase64Strings() { }
        public void llRemoteDataSetRegion() { }
        public void llLog10() { }
        public void llLog() { }
        public void llGetAnimationList() { }
        public void llSetParcelMusicURL() { }
        public void llGetRootPosition() { }
        public void llGetRootRotation() { }
        public void llGetObjectDesc() { }
        public void llSetObjectDesc() { }
        public void llGetCreator() { }
        public void llGetTimestamp() { }
        public void llSetLinkAlpha() { }
        public void llGetNumberOfPrims() { }
        public void llGetNumberOfNotecardLines() { }
        public void llGetBoundingBox() { }
        public void llGetGeometricCenter() { }
        public void llGetPrimitiveParams() { }
        public void llIntegerToBase64() { }
        public void llBase64ToInteger() { }
        public void llGetGMTclock() { }
        public void llGetSimulatorHostname() { }
        public void llSetLocalRot() { }
        public void llParseStringKeepNulls() { }
        public void llRezAtRoot() { }
        public void llGetObjectPermMask() { }
        public void llSetObjectPermMask() { }
        public void llGetInventoryPermMask() { }
        public void llSetInventoryPermMask() { }
        public void llGetInventoryCreator() { }
        public void llOwnerSay() { }
        public void llRequestSimulatorData() { }
        public void llForceMouselook() { }
        public void llGetObjectMass() { }
        public void llListReplaceList() { }
        public void llLoadURL() { }
        public void llParcelMediaCommandList() { }
        public void llParcelMediaQuery() { }
        public void llModPow() { }
        public void llGetInventoryType() { }
        public void llSetPayPrice() { }
        public void llGetCameraPos() { }
        public void llGetCameraRot() { }
        public void llSetPrimURL() { }
        public void llRefreshPrimURL() { }
        public void llEscapeURL() { }
        public void llUnescapeURL() { }
        public void llMapDestination() { }
        public void llAddToLandBanList() { }
        public void llRemoveFromLandPassList() { }
        public void llRemoveFromLandBanList() { }
        public void llSetCameraParams() { }
        public void llClearCameraParams() { }
        public void llListStatistics() { }
        public void llGetUnixTime() { }
        public void llGetParcelFlags() { }
        public void llGetRegionFlags() { }
        public void llXorBase64StringsCorrect() { }
        public void llHTTPRequest() { }
        public void llResetLandBanList() { }
        public void llResetLandPassList() { }
        public void llGetParcelPrimCount() { }
        public void llGetParcelPrimOwners() { }
        public void llGetObjectPrimCount() { }
        public void llGetParcelMaxPrims() { }
        public void llGetParcelDetails() { }

    }
}
