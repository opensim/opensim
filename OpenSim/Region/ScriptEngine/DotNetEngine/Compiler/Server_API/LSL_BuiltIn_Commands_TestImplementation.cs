    /*
* Copyright (c) Contributors, http://www.openmetaverse.org/
* See CONTRIBUTORS.TXT for a full list of copyright holders.
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the OpenSim Project nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
/* Original code: Tedd Hansen */
using System;
using System.Collections.Generic;
using System.Text;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler
{
    public class LSL_BuiltIn_Commands_TestImplementation : LSL_BuiltIn_Commands_Interface
    {
        public LSL_BuiltIn_Commands_TestImplementation(string FullScriptID)
        {
            Common.SendToDebug("LSL_BuiltIn_Commands_TestImplementation: Creating object for FullScriptID: " + FullScriptID);
        }


        public float llSin(float f) { return 0; }
        public float llCos(float f) { return 0; }
        public float llTan(float f) { return 0; }
        public float llAtan2(float x, float y) { return 0; }
        public float llSqrt(float f) { return 0; }
        public float llPow(float fbase, float fexponent) { return 0; }
        public UInt32 llAbs(UInt32 i) { return 0; }
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


        public void llGetPos() { }
        public void llGetLocalPos() { }
        public void llSetRot() { }
        public void llGetRot() { }
        public void llGetLocalRot() { }
        public void llSetForce() { }
        public void llGetForce() { }
        public void llTarget() { }
        public void llTargetRemove() { }
        public void llRotTarget() { }
        public void llRotTargetRemove() { }
        public void llMoveToTarget() { }
        public void llStopMoveToTarget() { }
        public void llApplyImpulse() { }
        public void llApplyRotationalImpulse() { }
        public void llSetTorque() { }
        public void llGetTorque() { }
        public void llSetForceAndTorque() { }
        public void llGetVel() { }
        public void llGetAccel() { }
        public void llGetOmega() { }
        public void llGetTimeOfDay() { }
        public void llGetWallclock() { }
        public void llGetTime() { }
        public void llResetTime() { }
        public void llGetAndResetTime() { }
        public void llSound() { }
        public void llPlaySound() { }
        public void llLoopSound() { }
        public void llLoopSoundMaster() { }
        public void llLoopSoundSlave() { }
        public void llPlaySoundSlave() { }
        public void llTriggerSound() { }
        public void llStopSound() { }
        public void llPreloadSound() { }
        public void llGetSubString() { }
        public void llDeleteSubString() { }
        public void llInsertString() { }
        public void llToUpper() { }
        public void llToLower() { }
        public void llGiveMoney() { }
        public void llMakeExplosion() { }
        public void llMakeFountain() { }
        public void llMakeSmoke() { }
        public void llMakeFire() { }
        public void llRezObject() { }
        public void llLookAt() { }
        public void llStopLookAt() { }
        public void llSetTimerEvent() { }
        public void llSleep() { }
        public void llGetMass() { }
        public void llCollisionFilter() { }
        public void llTakeControls() { }
        public void llReleaseControls() { }
        public void llAttachToAvatar() { }
        public void llDetachFromAvatar() { }
        public void llTakeCamera() { }
        public void llReleaseCamera() { }
        public void llGetOwner() { }
        public void llInstantMessage() { }
        public void llEmail() { }
        public void llGetNextEmail() { }
        public void llGetKey() { }
        public void llSetBuoyancy() { }
        public void llSetHoverHeight() { }
        public void llStopHover() { }
        public void llMinEventDelay() { }
        public void llSoundPreload() { }
        public void llRotLookAt() { }
        public void llStringLength() { }
        public void llStartAnimation() { }
        public void llStopAnimation() { }
        public void llPointAt() { }
        public void llStopPointAt() { }
        public void llTargetOmega() { }
        public void llGetStartParameter() { }
        public void llGodLikeRezObject() { }
        public void llRequestPermissions() { }
        public void llGetPermissionsKey() { }
        public void llGetPermissions() { }
        public void llGetLinkNumber() { }
        public void llSetLinkColor() { }
        public void llCreateLink() { }
        public void llBreakLink() { }
        public void llBreakAllLinks() { }
        public void llGetLinkKey() { }
        public void llGetLinkName() { }
        public void llGetInventoryNumber() { }
        public void llGetInventoryName() { }
        public void llSetScriptState() { }
        public void llGetEnergy() { }
        public void llGiveInventory() { }
        public void llRemoveInventory() { }
        public void llSetText() { }
        public void llWater() { }
        public void llPassTouches() { }
        public void llRequestAgentData() { }
        public void llRequestInventoryData() { }
        public void llSetDamage() { }
        public void llTeleportAgentHome() { }
        public void llModifyLand() { }
        public void llCollisionSound() { }
        public void llCollisionSprite() { }
        public void llGetAnimation() { }
        public void llResetScript() { }
        public void llMessageLinked() { }
        public void llPushObject() { }
        public void llPassCollisions() { }
        public void llGetScriptName() { }
        public void llGetNumberOfSides() { }
        public void llAxisAngle2Rot() { }
        public void llRot2Axis() { }
        public void llRot2Angle() { }
        public void llAcos() { }
        public void llAsin() { }
        public void llAngleBetween() { }
        public void llGetInventoryKey() { }
        public void llAllowInventoryDrop() { }
        public void llGetSunDirection() { }
        public void llGetTextureOffset() { }
        public void llGetTextureScale() { }
        public void llGetTextureRot() { }
        public void llSubStringIndex() { }
        public void llGetOwnerKey() { }
        public void llGetCenterOfMass() { }
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
        public void llGetObjectName() { }
        public void llSetObjectName() { }
        public void llGetDate() { }
        public void llEdgeOfWorld() { }
        public void llGetAgentInfo() { }
        public void llAdjustSoundVolume() { }
        public void llSetSoundQueueing() { }
        public void llSetSoundRadius() { }
        public void llKey2Name() { }
        public void llSetTextureAnim() { }
        public void llTriggerSoundLimited() { }
        public void llEjectFromLand() { }
        public void llParseString2List() { }
        public void llOverMyLand() { }
        public void llGetLandOwnerAt() { }
        public void llGetNotecardLine() { }
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
