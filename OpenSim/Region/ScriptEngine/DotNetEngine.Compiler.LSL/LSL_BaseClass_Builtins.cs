//using System;
//using System.Collections.Generic;
//using System.Text;

//namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler.LSL
//{
//    public partial class LSL_BaseClass
//    {


//        public float llSin() {
//            float f = (float)LSLStack.Pop();
//            return LSL_Builtins.llSin(f);
//        }
//        public float llCos() {
//            float f = (float)LSLStack.Pop();
//            return LSL_Builtins.llCos(f);
//        }
//        public float llTan() {
//            float f = (float)LSLStack.Pop();
//            return LSL_Builtins.llTan(f);
//        }
//        public float llAtan2() {
//            float x = (float)LSLStack.Pop();
//            float y = (float)LSLStack.Pop();
//            return LSL_Builtins.llAtan2(x, y);
//        }
//        public float llSqrt() {
//            float f = (float)LSLStack.Pop();
//            return LSL_Builtins.llSqrt(f);
//        }
//        float llPow()
//        {
//            float fexponent = (float)LSLStack.Pop();
//            float fbase = (float)LSLStack.Pop();
//            return LSL_Builtins.llPow(fbase, fexponent);
//        }
//        //UInt32 llAbs(UInt32 i){ return; }
//        //float llFabs(float f){ return; }
//        //float llFrand(float mag){ return; }
//        //UInt32 llFloor(float f){ return; }
//        //UInt32 llCeil(float f){ return; }
//        //UInt32 llRound(float f){ return; }
//        //float llVecMag(LSO_Enums.Vector v){ return; }
//        //LSO_Enums.Vector llVecNorm(LSO_Enums.Vector v){ return; }
//        //float llVecDist(LSO_Enums.Vector a, LSO_Enums.Vector b){ return; }
//        //LSO_Enums.Vector llRot2Euler(LSO_Enums.Rotation r){ return; }
//        //LSO_Enums.Rotation llEuler2Rot(LSO_Enums.Vector v){ return; }
//        //LSO_Enums.Rotation llAxes2Rot(LSO_Enums.Vector fwd, LSO_Enums.Vector left, LSO_Enums.Vector up){ return; }
//        //LSO_Enums.Vector llRot2Fwd(LSO_Enums.Rotation r){ return; }
//        //LSO_Enums.Vector llRot2Left(LSO_Enums.Rotation r){ return; }
//        //LSO_Enums.Vector llRot2Up(LSO_Enums.Rotation r){ return; }
//        //LSO_Enums.Rotation llRotBetween(LSO_Enums.Vector start, LSO_Enums.Vector end){ return; }
//        public void llWhisper()
//        {
//            UInt16 i = (UInt16)LSLStack.Pop();
//            string s = (string)LSLStack.Pop();
//            LSL_Builtins.llWhisper(i, s);
//        }
//        public void llSay()
//        {
//            UInt16 i = (UInt16)LSLStack.Pop();
//            string s = (string)LSLStack.Pop();
//            LSL_Builtins.llSay(i, s);
//        }
//        //void llShout(UInt16 channelID, string text);
//        //UInt32 llListen(UInt16 channelID, string name, LSO_Enums.Key ID, string msg);
//        //void llListenControl(UInt32 number, UInt32 active);
//        //void llListenRemove(UInt32 number);
//        //void llSensor(string name, LSO_Enums.Key id, UInt32 type, float range, float arc);
//        //void llSensorRepeat(string name, LSO_Enums.Key id, UInt32 type, float range, float arc, float rate);
//        //void llSensorRemove();
//        //string llDetectedName(UInt32 number);
//        //LSO_Enums.Key llDetectedKey(UInt32 number);
//        //LSO_Enums.Key llDetectedOwner(UInt32 number);
//        //UInt32 llDetectedType(UInt32 number);
//        //LSO_Enums.Vector llDetectedPos(UInt32 number);
//        //LSO_Enums.Vector llDetectedVel(UInt32 number);
//        //LSO_Enums.Vector llDetectedGrab(UInt32 number);
//        //LSO_Enums.Rotation llDetectedRot(UInt32 number);
//        //UInt32 llDetectedGroup(UInt32 number);
//        //UInt32 llDetectedLinkNumber(UInt32 number);
//        //void llDie();
//        //float llGround(LSO_Enums.Vector offset);
//        //float llCloud(LSO_Enums.Vector offset);
//        //LSO_Enums.Vector llWind(LSO_Enums.Vector offset);
//        //void llSetStatus(UInt32 status, UInt32 value);
//        //UInt32 llGetStatus(UInt32 status);
//        //void llSetScale(LSO_Enums.Vector scale);
//        //LSO_Enums.Vector llGetScale();
//        //void llSetColor();
//        //float llGetAlpha();
//        //void llSetAlpha();
//        //LSO_Enums.Vector llGetColor();
//        //void llSetTexture();
//        //void llScaleTexture();
//        //void llOffsetTexture();
//        //void llRotateTexture();
//        //string llGetTexture();
//        //void llSetPos();

//        public void llGetPos() { }
//        public void llGetLocalPos() { }
//        public void llSetRot() { }
//        public void llGetRot() { }
//        public void llGetLocalRot() { }
//        public void llSetForce() { }
//        public void llGetForce() { }
//        public void llTarget() { }
//        public void llTargetRemove() { }
//        public void llRotTarget() { }
//        public void llRotTargetRemove() { }
//        public void llMoveToTarget() { }
//        public void llStopMoveToTarget() { }
//        public void llApplyImpulse() { }
//        public void llApplyRotationalImpulse() { }
//        public void llSetTorque() { }
//        public void llGetTorque() { }
//        public void llSetForceAndTorque() { }
//        public void llGetVel() { }
//        public void llGetAccel() { }
//        public void llGetOmega() { }
//        public void llGetTimeOfDay() { }
//        public void llGetWallclock() { }
//        public void llGetTime() { }
//        public void llResetTime() { }
//        public void llGetAndResetTime() { }
//        public void llSound() { }
//        public void llPlaySound() { }
//        public void llLoopSound() { }
//        public void llLoopSoundMaster() { }
//        public void llLoopSoundSlave() { }
//        public void llPlaySoundSlave() { }
//        public void llTriggerSound() { }
//        public void llStopSound() { }
//        public void llPreloadSound() { }
//        public void llGetSubString() { }
//        public void llDeleteSubString() { }
//        public void llInsertString() { }
//        public void llToUpper() { }
//        public void llToLower() { }
//        public void llGiveMoney() { }
//        public void llMakeExplosion() { }
//        public void llMakeFountain() { }
//        public void llMakeSmoke() { }
//        public void llMakeFire() { }
//        public void llRezObject() { }
//        public void llLookAt() { }
//        public void llStopLookAt() { }
//        public void llSetTimerEvent() { }
//        public void llSleep() { }
//        public void llGetMass() { }
//        public void llCollisionFilter() { }
//        public void llTakeControls() { }
//        public void llReleaseControls() { }
//        public void llAttachToAvatar() { }
//        public void llDetachFromAvatar() { }
//        public void llTakeCamera() { }
//        public void llReleaseCamera() { }
//        public void llGetOwner() { }
//        public void llInstantMessage() { }
//        public void llEmail() { }
//        public void llGetNextEmail() { }
//        public void llGetKey() { }
//        public void llSetBuoyancy() { }
//        public void llSetHoverHeight() { }
//        public void llStopHover() { }
//        public void llMinEventDelay() { }
//        public void llSoundPreload() { }
//        public void llRotLookAt() { }
//        public void llStringLength() { }
//        public void llStartAnimation() { }
//        public void llStopAnimation() { }
//        public void llPointAt() { }
//        public void llStopPointAt() { }
//        public void llTargetOmega() { }
//        public void llGetStartParameter() { }
//        public void llGodLikeRezObject() { }
//        public void llRequestPermissions() { }
//        public void llGetPermissionsKey() { }
//        public void llGetPermissions() { }
//        public void llGetLinkNumber() { }
//        public void llSetLinkColor() { }
//        public void llCreateLink() { }
//        public void llBreakLink() { }
//        public void llBreakAllLinks() { }
//        public void llGetLinkKey() { }
//        public void llGetLinkName() { }
//        public void llGetInventoryNumber() { }
//        public void llGetInventoryName() { }
//        public void llSetScriptState() { }
//        public void llGetEnergy() { }
//        public void llGiveInventory() { }
//        public void llRemoveInventory() { }
//        public void llSetText() { }
//        public void llWater() { }
//        public void llPassTouches() { }
//        public void llRequestAgentData() { }
//        public void llRequestInventoryData() { }
//        public void llSetDamage() { }
//        public void llTeleportAgentHome() { }
//        public void llModifyLand() { }
//        public void llCollisionSound() { }
//        public void llCollisionSprite() { }
//        public void llGetAnimation() { }
//        public void llResetScript() { }
//        public void llMessageLinked() { }
//        public void llPushObject() { }
//        public void llPassCollisions() { }
//        public void llGetScriptName() { }
//        public void llGetNumberOfSides() { }
//        public void llAxisAngle2Rot() { }
//        public void llRot2Axis() { }
//        public void llRot2Angle() { }
//        public void llAcos() { }
//        public void llAsin() { }
//        public void llAngleBetween() { }
//        public void llGetInventoryKey() { }
//        public void llAllowInventoryDrop() { }
//        public void llGetSunDirection() { }
//        public void llGetTextureOffset() { }
//        public void llGetTextureScale() { }
//        public void llGetTextureRot() { }
//        public void llSubStringIndex() { }
//        public void llGetOwnerKey() { }
//        public void llGetCenterOfMass() { }
//        public void llListSort() { }
//        public void llGetListLength() { }
//        public void llList2Integer() { }
//        public void llList2Float() { }
//        public void llList2String() { }
//        public void llList2Key() { }
//        public void llList2Vector() { }
//        public void llList2Rot() { }
//        public void llList2List() { }
//        public void llDeleteSubList() { }
//        public void llGetListEntryType() { }
//        public void llList2CSV() { }
//        public void llCSV2List() { }
//        public void llListRandomize() { }
//        public void llList2ListStrided() { }
//        public void llGetRegionCorner() { }
//        public void llListInsertList() { }
//        public void llListFindList() { }
//        public void llGetObjectName() { }
//        public void llSetObjectName() { }
//        public void llGetDate() { }
//        public void llEdgeOfWorld() { }
//        public void llGetAgentInfo() { }
//        public void llAdjustSoundVolume() { }
//        public void llSetSoundQueueing() { }
//        public void llSetSoundRadius() { }
//        public void llKey2Name() { }
//        public void llSetTextureAnim() { }
//        public void llTriggerSoundLimited() { }
//        public void llEjectFromLand() { }
//        public void llParseString2List() { }
//        public void llOverMyLand() { }
//        public void llGetLandOwnerAt() { }
//        public void llGetNotecardLine() { }
//        public void llGetAgentSize() { }
//        public void llSameGroup() { }
//        public void llUnSit() { }
//        public void llGroundSlope() { }
//        public void llGroundNormal() { }
//        public void llGroundContour() { }
//        public void llGetAttached() { }
//        public void llGetFreeMemory() { }
//        public void llGetRegionName() { }
//        public void llGetRegionTimeDilation() { }
//        public void llGetRegionFPS() { }
//        public void llParticleSystem() { }
//        public void llGroundRepel() { }
//        public void llGiveInventoryList() { }
//        public void llSetVehicleType() { }
//        public void llSetVehicleFloatParam() { }
//        public void llSetVehicleVectorParam() { }
//        public void llSetVehicleRotationParam() { }
//        public void llSetVehicleFlags() { }
//        public void llRemoveVehicleFlags() { }
//        public void llSitTarget() { }
//        public void llAvatarOnSitTarget() { }
//        public void llAddToLandPassList() { }
//        public void llSetTouchText() { }
//        public void llSetSitText() { }
//        public void llSetCameraEyeOffset() { }
//        public void llSetCameraAtOffset() { }
//        public void llDumpList2String() { }
//        public void llScriptDanger() { }
//        public void llDialog() { }
//        public void llVolumeDetect() { }
//        public void llResetOtherScript() { }
//        public void llGetScriptState() { }
//        public void llRemoteLoadScript() { }
//        public void llSetRemoteScriptAccessPin() { }
//        public void llRemoteLoadScriptPin() { }
//        public void llOpenRemoteDataChannel() { }
//        public void llSendRemoteData() { }
//        public void llRemoteDataReply() { }
//        public void llCloseRemoteDataChannel() { }
//        public void llMD5String() { }
//        public void llSetPrimitiveParams() { }
//        public void llStringToBase64() { }
//        public void llBase64ToString() { }
//        public void llXorBase64Strings() { }
//        public void llRemoteDataSetRegion() { }
//        public void llLog10() { }
//        public void llLog() { }
//        public void llGetAnimationList() { }
//        public void llSetParcelMusicURL() { }
//        public void llGetRootPosition() { }
//        public void llGetRootRotation() { }
//        public void llGetObjectDesc() { }
//        public void llSetObjectDesc() { }
//        public void llGetCreator() { }
//        public void llGetTimestamp() { }
//        public void llSetLinkAlpha() { }
//        public void llGetNumberOfPrims() { }
//        public void llGetNumberOfNotecardLines() { }
//        public void llGetBoundingBox() { }
//        public void llGetGeometricCenter() { }
//        public void llGetPrimitiveParams() { }
//        public void llIntegerToBase64() { }
//        public void llBase64ToInteger() { }
//        public void llGetGMTclock() { }
//        public void llGetSimulatorHostname() { }
//        public void llSetLocalRot() { }
//        public void llParseStringKeepNulls() { }
//        public void llRezAtRoot() { }
//        public void llGetObjectPermMask() { }
//        public void llSetObjectPermMask() { }
//        public void llGetInventoryPermMask() { }
//        public void llSetInventoryPermMask() { }
//        public void llGetInventoryCreator() { }
//        public void llOwnerSay() { }
//        public void llRequestSimulatorData() { }
//        public void llForceMouselook() { }
//        public void llGetObjectMass() { }
//        public void llListReplaceList() { }
//        public void llLoadURL() { }
//        public void llParcelMediaCommandList() { }
//        public void llParcelMediaQuery() { }
//        public void llModPow() { }
//        public void llGetInventoryType() { }
//        public void llSetPayPrice() { }
//        public void llGetCameraPos() { }
//        public void llGetCameraRot() { }
//        public void llSetPrimURL() { }
//        public void llRefreshPrimURL() { }
//        public void llEscapeURL() { }
//        public void llUnescapeURL() { }
//        public void llMapDestination() { }
//        public void llAddToLandBanList() { }
//        public void llRemoveFromLandPassList() { }
//        public void llRemoveFromLandBanList() { }
//        public void llSetCameraParams() { }
//        public void llClearCameraParams() { }
//        public void llListStatistics() { }
//        public void llGetUnixTime() { }
//        public void llGetParcelFlags() { }
//        public void llGetRegionFlags() { }
//        public void llXorBase64StringsCorrect() { }
//        public void llHTTPRequest() { }
//        public void llResetLandBanList() { }
//        public void llResetLandPassList() { }
//        public void llGetParcelPrimCount() { }
//        public void llGetParcelPrimOwners() { }
//        public void llGetObjectPrimCount() { }
//        public void llGetParcelMaxPrims() { }
//        public void llGetParcelDetails() { }

//    }
//}
