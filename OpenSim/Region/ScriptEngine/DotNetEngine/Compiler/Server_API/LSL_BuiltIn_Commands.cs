using System;
using System.Collections.Generic;
using System.Text;
using libsecondlife;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Region.Environment.Scenes.Scripting;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.ScriptEngine.DotNetEngine.Compiler;
using OpenSim.Region.ScriptEngine.Common;
using OpenSim.Framework.Console;
using OpenSim.Framework.Utilities;
using System.Runtime.Remoting.Lifetime;

namespace OpenSim.Region.ScriptEngine.DotNetEngine.Compiler
{

    /// <summary>
    /// Contains all LSL ll-functions. This class will be in Default AppDomain.
    /// </summary>
    public class LSL_BuiltIn_Commands: MarshalByRefObject, LSL_BuiltIn_Commands_Interface
    {

        private System.Text.ASCIIEncoding enc = new System.Text.ASCIIEncoding();
        private ScriptEngine m_ScriptEngine;
        private IScriptHost m_host;
        private uint m_localID;
        private LLUUID m_itemID;

        public LSL_BuiltIn_Commands(ScriptEngine ScriptEngine, IScriptHost host, uint localID, LLUUID itemID)
        {
            m_ScriptEngine = ScriptEngine;
            m_host = host;
            m_localID = localID;
            m_itemID = itemID;
            

            //MainLog.Instance.Notice("ScriptEngine", "LSL_BaseClass.Start() called. Hosted by [" + m_host.Name + ":" + m_host.UUID + "@" + m_host.AbsolutePosition + "]");
        }


        private string m_state = "default";

        public string State() 
        {
            return m_state;
        }

        // Object never expires
        public override Object InitializeLifetimeService()
        {
            //Console.WriteLine("LSL_BuiltIn_Commands: InitializeLifetimeService()");
            //            return null;
            ILease lease = (ILease)base.InitializeLifetimeService();

            if (lease.CurrentState == LeaseState.Initial)
            {
                lease.InitialLeaseTime = TimeSpan.Zero; // TimeSpan.FromMinutes(1);
                //                lease.SponsorshipTimeout = TimeSpan.FromMinutes(2);
                //                lease.RenewOnCallTime = TimeSpan.FromSeconds(2);
            }
            return lease;
        }


        public Scene World
        {
            get { return m_ScriptEngine.World; }
        }

        //These are the implementations of the various ll-functions used by the LSL scripts.
        //starting out, we use the System.Math library for trig functions. - ckrinke 8-14-07
        public double llSin(double f) { return (double)Math.Sin(f); }
        public double llCos(double f) { return (double)Math.Cos(f); }
        public double llTan(double f) { return (double)Math.Tan(f); }
        public double llAtan2(double x, double y) { return (double)Math.Atan2(y, x); }
        public double llSqrt(double f) { return (double)Math.Sqrt(f); }
        public double llPow(double fbase, double fexponent) { return (double)Math.Pow(fbase, fexponent); }
        public int llAbs(int i) { return (int)Math.Abs(i); }
        public double llFabs(double f) { return (double)Math.Abs(f); }

        public double llFrand(double mag)
        {
            lock (Util.RandomClass)
            {
                return Util.RandomClass.Next((int)mag);
            }
        }

        public int llFloor(double f) { return (int)Math.Floor(f); }
        public int llCeil(double f) { return (int)Math.Ceiling(f); }
        public int llRound(double f) { return (int)Math.Round(f, 3); }

        //This next group are vector operations involving squaring and square root. ckrinke
        public double llVecMag(LSL_Types.Vector3 v) 
        { 
            return (v.X*v.X + v.Y*v.Y + v.Z*v.Z); 
        }

        public LSL_Types.Vector3 llVecNorm(LSL_Types.Vector3 v) 
        {
            double mag = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
            LSL_Types.Vector3 nor =  new LSL_Types.Vector3();
            nor.X = v.X / mag; nor.Y = v.Y / mag; nor.Z = v.Z / mag;
            return nor;
        }

        public double llVecDist(LSL_Types.Vector3 a, LSL_Types.Vector3 b) 
        {
            double dx = a.X - b.X; double dy = a.Y - b.Y; double dz = a.Z - b.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        //Now we start getting into quaternions which means sin/cos, matrices and vectors. ckrinke
        public LSL_Types.Vector3 llRot2Euler(LSL_Types.Quaternion r) 
        {
            //This implementation is from http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions. ckrinke
            LSL_Types.Quaternion t = new LSL_Types.Quaternion(r.X * r.X, r.Y * r.Y, r.Z * r.Z, r.R * r.R);
            double m = (t.X + t.Y + t.Z + t.R);
            if (m == 0) return new LSL_Types.Vector3();
            double n = 2 * (r.Y * r.R + r.X * r.Z);
            double p = m * m - n * n;
            if (p > 0)
                return new LSL_Types.Vector3(Math.Atan2(2.0 * (r.X*r.R - r.Y*r.Z),(-t.X - t.Y + t.Z + t.R)),
                  Math.Atan2(n,Math.Sqrt(p)), Math.Atan2(2.0 * (r.Z*r.R - r.X*r.Y),( t.X - t.Y - t.Z + t.R)));
            else if(n>0)
                return new LSL_Types.Vector3( 0.0, Math.PI/2, Math.Atan2((r.Z*r.R + r.X*r.Y), 0.5 - t.X - t.Z));
            else
                return new LSL_Types.Vector3( 0.0, -Math.PI/2, Math.Atan2((r.Z*r.R + r.X*r.Y), 0.5 - t.X - t.Z));
       }

        public LSL_Types.Quaternion llEuler2Rot(LSL_Types.Vector3 v) 
        { 
            //this comes from from http://lslwiki.net/lslwiki/wakka.php?wakka=LibraryRotationFunctions but is incomplete as of 8/19/07
            float err = 0.00001f;
            double ax = Math.Sin(v.X / 2); double aw = Math.Cos(v.X / 2);
            double by = Math.Sin(v.Y / 2); double bw = Math.Cos(v.Y / 2);
            double cz = Math.Sin(v.Z / 2); double cw = Math.Cos(v.Z / 2);
            LSL_Types.Quaternion a1 = new LSL_Types.Quaternion(0.0, 0.0, cz, cw);
            LSL_Types.Quaternion a2 = new LSL_Types.Quaternion(0.0, by, 0.0, bw);
            LSL_Types.Quaternion a3 = new LSL_Types.Quaternion(ax, 0.0, 0.0, aw);
            LSL_Types.Quaternion a = new LSL_Types.Quaternion();
//This multiplication doesnt compile, yet.            a = a1 * a2 * a3;
            LSL_Types.Quaternion b = new LSL_Types.Quaternion(ax * bw * cw + aw * by * cz,
                  aw * by * cw - ax * bw * cz, aw * bw * cz + ax * by * cw, aw * bw * cw - ax * by * cz);
            LSL_Types.Quaternion c = new LSL_Types.Quaternion(); 
//This addition doesnt compile yet c = a + b;
            LSL_Types.Quaternion d = new LSL_Types.Quaternion(); 
//This addition doesnt compile yet d = a - b;
            if ((Math.Abs(c.X) > err && Math.Abs(d.X) > err) ||
                (Math.Abs(c.Y) > err && Math.Abs(d.Y) > err) ||
                (Math.Abs(c.Z) > err && Math.Abs(d.Z) > err) ||
                (Math.Abs(c.R) > err && Math.Abs(d.R) > err))
            {
                //return a new Quaternion that is null until I figure this out
                //                return b;
                //            return a;
            }
            return new LSL_Types.Quaternion(); 
        }

        public LSL_Types.Quaternion llAxes2Rot(LSL_Types.Vector3 fwd, LSL_Types.Vector3 left, LSL_Types.Vector3 up) { return new LSL_Types.Quaternion(); }
        public LSL_Types.Vector3 llRot2Fwd(LSL_Types.Quaternion r) { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llRot2Left(LSL_Types.Quaternion r) { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llRot2Up(LSL_Types.Quaternion r) { return new LSL_Types.Vector3(); }
        public LSL_Types.Quaternion llRotBetween(LSL_Types.Vector3 start, LSL_Types.Vector3 end) { return new LSL_Types.Quaternion(); }

        public void llWhisper(int channelID, string text)
        {
            //type for whisper is 0
            World.SimChat(Helpers.StringToField(text),
                          0, m_host.AbsolutePosition, m_host.Name, m_host.UUID);
        }

        public void llSay(int channelID, string text)
        {
            //type for say is 1

            World.SimChat(Helpers.StringToField(text),
                           1, m_host.AbsolutePosition, m_host.Name, m_host.UUID);
        }

        public void llShout(int channelID, string text)
        {
            //type for shout is 2
            World.SimChat(Helpers.StringToField(text),
                          2, m_host.AbsolutePosition, m_host.Name, m_host.UUID);
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
        public LSL_Types.Vector3 llDetectedPos(int number) { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llDetectedVel(int number) { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llDetectedGrab(int number) { return new LSL_Types.Vector3(); }
        public LSL_Types.Quaternion llDetectedRot(int number) { return new LSL_Types.Quaternion(); }
        public int llDetectedGroup(int number) { return 0; }
        public int llDetectedLinkNumber(int number) { return 0; }
        public void llDie() { return; }
        public double llGround(LSL_Types.Vector3 offset) { return 0; }
        public double llCloud(LSL_Types.Vector3 offset) { return 0; }
        public LSL_Types.Vector3 llWind(LSL_Types.Vector3 offset) { return new LSL_Types.Vector3(); }
        public void llSetStatus(int status, int value) { return; }
        public int llGetStatus(int status) { return 0; }
        public void llSetScale(LSL_Types.Vector3 scale) { return; }
        public LSL_Types.Vector3 llGetScale() { return new LSL_Types.Vector3(); }
        public void llSetColor(LSL_Types.Vector3 color, int face) { return; }
        public double llGetAlpha(int face) { return 0; }
        public void llSetAlpha(double alpha, int face) { return; }
        public LSL_Types.Vector3 llGetColor(int face) { return new LSL_Types.Vector3(); }
        public void llSetTexture(string texture, int face) { return; }
        public void llScaleTexture(double u, double v, int face) { return; }
        public void llOffsetTexture(double u, double v, int face) { return; }
        public void llRotateTexture(double rotation, int face) { return; }

        public string llGetTexture(int face) 
        { 
            return ""; 
        }

        public void llSetPos(LSL_Types.Vector3 pos) { return; }

        public LSL_Types.Vector3 llGetPos()
        {
            throw new NotImplementedException("llGetPos");
            // return m_host.AbsolutePosition;
        }

        public LSL_Types.Vector3 llGetLocalPos() { return new LSL_Types.Vector3(); }
        public void llSetRot(LSL_Types.Quaternion rot) { }
        public LSL_Types.Quaternion llGetRot() { return new LSL_Types.Quaternion(); }
        public LSL_Types.Quaternion llGetLocalRot() { return new LSL_Types.Quaternion(); }
        public void llSetForce(LSL_Types.Vector3 force, int local) { }
        public LSL_Types.Vector3 llGetForce() { return new LSL_Types.Vector3(); }
        public int llTarget(LSL_Types.Vector3 position, double range) { return 0; }
        public void llTargetRemove(int number) { }
        public int llRotTarget(LSL_Types.Quaternion rot, double error) { return 0; }
        public void llRotTargetRemove(int number) { }
        public void llMoveToTarget(LSL_Types.Vector3 target, double tau) { }
        public void llStopMoveToTarget() { }
        public void llApplyImpulse(LSL_Types.Vector3 force, int local) { }
        public void llApplyRotationalImpulse(LSL_Types.Vector3 force, int local) { }
        public void llSetTorque(LSL_Types.Vector3 torque, int local) { }
        public LSL_Types.Vector3 llGetTorque() { return new LSL_Types.Vector3(); }
        public void llSetForceAndTorque(LSL_Types.Vector3 force, LSL_Types.Vector3 torque, int local) { }
        public LSL_Types.Vector3 llGetVel() { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGetAccel() { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGetOmega() { return new LSL_Types.Vector3(); }
        public double llGetTimeOfDay() { return 0; }

        public double llGetWallclock() 
        { 
            return DateTime.Now.TimeOfDay.TotalSeconds; 
        }

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

        public string llGetSubString(string src, int start, int end) 
        { 
            return src.Substring(start, end); 
        }

        public string llDeleteSubString(string src, int start, int end) {return "";}
        public string llInsertString(string dst, int position, string src) { return ""; }

        public string llToUpper(string src) 
        { 
            return src.ToUpper(); 
        }

        public string llToLower(string src) 
        { 
            return src.ToLower(); 
        }

        public int llGiveMoney(string destination, int amount) { return 0; }
        public void llMakeExplosion() { }
        public void llMakeFountain() { }
        public void llMakeSmoke() { }
        public void llMakeFire() { }
        public void llRezObject(string inventory, LSL_Types.Vector3 pos, LSL_Types.Quaternion rot, int param) { }
        public void llLookAt(LSL_Types.Vector3 target, double strength, double damping) { }
        public void llStopLookAt() { }

        public void llSetTimerEvent(double sec) 
        {
            // Setting timer repeat
            m_ScriptEngine.m_LSLLongCmdHandler.SetTimerEvent(m_localID, m_itemID, sec);
        }

        public void llSleep(double sec) 
        { 
            System.Threading.Thread.Sleep((int)(sec * 1000)); 
        }

        public double llGetMass() { return 0; }
        public void llCollisionFilter(string name, string id, int accept) { }
        public void llTakeControls(int controls, int accept, int pass_on) { }
        public void llReleaseControls() { }
        public void llAttachToAvatar(int attachment) { }
        public void llDetachFromAvatar() { }
        public void llTakeCamera() { }
        public void llReleaseCamera() { }

        public string llGetOwner() 
        { 
            return m_host.ObjectOwner.ToStringHyphenated(); 
        }

        public void llInstantMessage(string user, string message) { }
        public void llEmail(string address, string subject, string message) { }
        public void llGetNextEmail(string address, string subject) { }

        public string llGetKey()
        {
            return m_host.UUID.ToStringHyphenated();
        }

        public void llSetBuoyancy(double buoyancy) { }
        public void llSetHoverHeight(double height, int water, double tau) { }
        public void llStopHover() { }
        public void llMinEventDelay(double delay) { }
        public void llSoundPreload() { }
        public void llRotLookAt(LSL_Types.Quaternion target, double strength, double damping) { }

        public int llStringLength(string str)
        {
            if (str.Length > 0)
            {
                return str.Length;
            }
            else
            {
                return 0;
            }
        }

        public void llStartAnimation(string anim) { }
        public void llStopAnimation(string anim) { }
        public void llPointAt() { }
        public void llStopPointAt() { }
        public void llTargetOmega(LSL_Types.Vector3 axis, double spinrate, double gain) { }
        public int llGetStartParameter() { return 0; }
        public void llGodLikeRezObject(string inventory, LSL_Types.Vector3 pos) { }
        public void llRequestPermissions(string agent, int perm) { }
        public string llGetPermissionsKey() { return ""; }
        public int llGetPermissions() { return 0; }
        public int llGetLinkNumber() { return 0; }
        public void llSetLinkColor(int linknumber, LSL_Types.Vector3 color, int face) { }
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

        public void llSetText(string text, LSL_Types.Vector3 color, double alpha)
        {
            // TEMP DISABLED UNTIL WE CAN AGREE UPON VECTOR/ROTATION FORMAT
            //m_host.SetText(text, color, alpha);
        }

        public double llWater(LSL_Types.Vector3 offset) { return 0; }
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
        public void llPushObject(string target, LSL_Types.Vector3 impulse, LSL_Types.Vector3 ang_impulse, int local) { }
        public void llPassCollisions(int pass) { }
        public string llGetScriptName() { return ""; }

        public int llGetNumberOfSides() { return 0; }

        public LSL_Types.Quaternion llAxisAngle2Rot(LSL_Types.Vector3 axis, double angle) { return new LSL_Types.Quaternion(); }
        public LSL_Types.Vector3 llRot2Axis(LSL_Types.Quaternion rot) { return new LSL_Types.Vector3(); }
        public void llRot2Angle() { }

        public double llAcos(double val) 
        { 
            return (double)Math.Acos(val); 
        }

        public double llAsin(double val) 
        { 
            return (double)Math.Asin(val); 
        }

        public double llAngleBetween(LSL_Types.Quaternion a, LSL_Types.Quaternion b) { return 0; }
        public string llGetInventoryKey(string name) { return ""; }
        public void llAllowInventoryDrop(int add) { }
        public LSL_Types.Vector3 llGetSunDirection() { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGetTextureOffset(int face) { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGetTextureScale(int side) { return new LSL_Types.Vector3(); }
        public double llGetTextureRot(int side) { return 0; }

        public int llSubStringIndex(string source, string pattern)
        {
            return source.IndexOf(pattern);
        }

        public string llGetOwnerKey(string id) 
        { 
            return ""; 
        }

        public LSL_Types.Vector3 llGetCenterOfMass() { return new LSL_Types.Vector3(); }

        public List<string> llListSort(List<string> src, int stride, int ascending)
        {
            //List<string> nlist = src.Sort();

            //if (ascending == 0)
            //{
            //nlist.Reverse();
            //}

            //return nlist;
            return new List<string>(); ;
        }

        public int llGetListLength(List<string> src)
        {
            return src.Count;
        }

        public int llList2Integer(List<string> src, int index)
        {
            return Convert.ToInt32(src[index]);
        }

        public double llList2double(List<string> src, int index)
        {
            return Convert.ToDouble(src[index]);
        }

        public float llList2Float(List<string> src, int index)
        {
            return Convert.ToSingle(src[index]);
        }

        public string llList2String(List<string> src, int index)
        {
            return src[index];
        }

        public string llList2Key(List<string> src, int index)
        {
            //return OpenSim.Framework.Types.ToStringHyphenated(src[index]);
            return "";
        }

        public LSL_Types.Vector3 llList2Vector(List<string> src, int index)
        { return new LSL_Types.Vector3(); }
        public LSL_Types.Quaternion llList2Rot(List<string> src, int index)
        { return new LSL_Types.Quaternion(); }
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

        public LSL_Types.Vector3 llGetRegionCorner()
        { 
            return new LSL_Types.Vector3(World.RegionInfo.RegionLocX * 256, World.RegionInfo.RegionLocY * 256, 0);
        }

        public List<string> llListInsertList(List<string> dest, List<string> src, int start)
        { return new List<string>(); }
        public int llListFindList(List<string> src, List<string> test) { return 0; }
        
        public string llGetObjectName() 
        { 
            return m_host.Name; 
        }

        public void llSetObjectName(string name) 
        { 
            m_host.Name = name;
        }

        public string llGetDate()
        {
            DateTime date = DateTime.Now.ToUniversalTime();
            string result = date.ToString("yyyy-MM-dd");
            return result;
        }

        public int llEdgeOfWorld(LSL_Types.Vector3 pos, LSL_Types.Vector3 dir) { return 0; }
        public int llGetAgentInfo(string id) { return 0; }
        public void llAdjustSoundVolume(double volume) { }
        public void llSetSoundQueueing(int queue) { }
        public void llSetSoundRadius(double radius) { }
        public string llKey2Name(string id) { return ""; }
        public void llSetTextureAnim(int mode, int face, int sizex, int sizey, double start, double length, double rate) { }
        public void llTriggerSoundLimited(string sound, double volume, LSL_Types.Vector3 top_north_east, LSL_Types.Vector3 bottom_south_west) { }
        public void llEjectFromLand(string pest) { }

        public void llParseString2List() { }

        public int llOverMyLand(string id) { return 0; }
        public string llGetLandOwnerAt(LSL_Types.Vector3 pos) { return ""; }
        public string llGetNotecardLine(string name, int line) { return ""; }
        public LSL_Types.Vector3 llGetAgentSize(string id) { return new LSL_Types.Vector3(); }
        public int llSameGroup(string agent) { return 0; }
        public void llUnSit(string id) { }
        public LSL_Types.Vector3 llGroundSlope(LSL_Types.Vector3 offset) { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGroundNormal(LSL_Types.Vector3 offset) { return new LSL_Types.Vector3(); }
        public LSL_Types.Vector3 llGroundContour(LSL_Types.Vector3 offset) { return new LSL_Types.Vector3(); }
        public int llGetAttached() { return 0; }
        public int llGetFreeMemory() { return 0; }

        public string llGetRegionName() 
        { 
            return World.RegionInfo.RegionName; 
        }

        public double llGetRegionTimeDilation() { return 1.0f; }
        public double llGetRegionFPS() { return 10.0f; }
        public void llParticleSystem(List<Object> rules) { }
        public void llGroundRepel(double height, int water, double tau) { }
        public void llGiveInventoryList() { }
        public void llSetVehicleType(int type) { }
        public void llSetVehicledoubleParam(int param, double value) { }
        public void llSetVehicleVectorParam(int param, LSL_Types.Vector3 vec) { }
        public void llSetVehicleRotationParam(int param, LSL_Types.Quaternion rot) { }
        public void llSetVehicleFlags(int flags) { }
        public void llRemoveVehicleFlags(int flags) { }
        public void llSitTarget(LSL_Types.Vector3 offset, LSL_Types.Quaternion rot) { }
        public string llAvatarOnSitTarget() { return ""; }
        public void llAddToLandPassList(string avatar, double hours) { }

        public void llSetTouchText(string text)
        {
            m_host.TouchName = text;
        }

        public void llSetSitText(string text)
        {
            m_host.SitName = text;
        }

        public void llSetCameraEyeOffset(LSL_Types.Vector3 offset) { }
        public void llSetCameraAtOffset(LSL_Types.Vector3 offset) { }
        public void llDumpList2String() { }
        public void llScriptDanger(LSL_Types.Vector3 pos) { }
        public void llDialog(string avatar, string message, List<string> buttons, int chat_channel) { }
        public void llVolumeDetect(int detect) { }
        public void llResetOtherScript(string name) { }

        public int llGetScriptState(string name) 
        { 
            return 0;
        }

        public void llRemoteLoadScript() { }
        public void llSetRemoteScriptAccessPin(int pin) { }
        public void llRemoteLoadScriptPin(string target, string name, int pin, int running, int start_param) { }
        public void llOpenRemoteDataChannel() { }
        public string llSendRemoteData(string channel, string dest, int idata, string sdata) { return ""; }
        public void llRemoteDataReply(string channel, string message_id, string sdata, int idata) { }
        public void llCloseRemoteDataChannel(string channel) { }

        public string llMD5String(string src, int nonce)
        {
            return Util.Md5Hash(src + ":" + nonce.ToString());
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

        public LSL_Types.Vector3 llGetRootPosition()
        {
            throw new NotImplementedException("llGetRootPosition");
            //return m_root.AbsolutePosition;
        }

        public LSL_Types.Quaternion llGetRootRotation()
        {
            return new LSL_Types.Quaternion();
        }

        public string llGetObjectDesc() 
        { 
            return m_host.Description; 
        }

        public void llSetObjectDesc(string desc) 
        {
            m_host.Description = desc;
        }

        public string llGetCreator() 
        { 
            return m_host.ObjectCreator.ToStringHyphenated(); 
        }

        public string llGetTimestamp() { return ""; }
        public void llSetLinkAlpha(int linknumber, double alpha, int face) { }
        public int llGetNumberOfPrims() { return 0; }
        public string llGetNumberOfNotecardLines(string name) { return ""; }
        public List<string> llGetBoundingBox(string obj) { return new List<string>(); }
        public LSL_Types.Vector3 llGetGeometricCenter() { return new LSL_Types.Vector3(); }
        public void llGetPrimitiveParams() { }
        public string llIntegerToBase64(int number) { return ""; }
        public int llBase64ToInteger(string str) { return 0; }

        public double llGetGMTclock() 
        { 
            return DateTime.UtcNow.TimeOfDay.TotalSeconds; 
        }
        
        public string llGetSimulatorHostname() 
        { 
            return System.Environment.MachineName; 
        }

        public void llSetLocalRot(LSL_Types.Quaternion rot) { }
        public List<string> llParseStringKeepNulls(string src, List<string> seperators, List<string> spacers)
        { return new List<string>(); }
        public void llRezAtRoot(string inventory, LSL_Types.Vector3 position, LSL_Types.Vector3 velocity, LSL_Types.Quaternion rot, int param) { }
        
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

        public void llLoadURL(string avatar_id, string message, string url) 
        {
            LLUUID avatarId = new LLUUID(avatar_id);
            m_ScriptEngine.World.SendUrlToUser(avatarId, m_host.Name, m_host.UUID, m_host.ObjectOwner, false, message, url);
        }

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
        public LSL_Types.Vector3 llGetCameraPos() { return new LSL_Types.Vector3(); }
        public LSL_Types.Quaternion llGetCameraRot() { return new LSL_Types.Quaternion(); }
        public void llSetPrimURL() { }
        public void llRefreshPrimURL() { }

        public string llEscapeURL(string url)
        {
            try
            {
                return Uri.EscapeUriString(url);
            }
            catch (Exception ex)
            {
                return "llEscapeURL: " + ex.ToString();
            }
        }

        public string llUnescapeURL(string url)
        {
            try
            {
                return Uri.UnescapeDataString(url);
            }
            catch (Exception ex)
            {
                return "llUnescapeURL: " + ex.ToString();
            }
        }
        public void llMapDestination(string simname, LSL_Types.Vector3 pos, LSL_Types.Vector3 look_at) { }
        public void llAddToLandBanList(string avatar, double hours) { }
        public void llRemoveFromLandPassList(string avatar) { }
        public void llRemoveFromLandBanList(string avatar) { }
        public void llSetCameraParams(List<string> rules) { }
        public void llClearCameraParams() { }
        public double llListStatistics(int operation, List<string> src) { return 0; }
        
        public int llGetUnixTime()
        {
            return Util.UnixTimeSinceEpoch();
        }

        public int llGetParcelFlags(LSL_Types.Vector3 pos) { return 0; }
        public int llGetRegionFlags() { return 0; }
        public string llXorBase64StringsCorrect(string str1, string str2) { return ""; }
        public void llHTTPRequest() { }
        public void llResetLandBanList() { }
        public void llResetLandPassList() { }
        public int llGetParcelPrimCount(LSL_Types.Vector3 pos, int category, int sim_wide) { return 0; }
        public List<string> llGetParcelPrimOwners(LSL_Types.Vector3 pos) { return new List<string>(); }
        public int llGetObjectPrimCount(string object_id) { return 0; }
        public int llGetParcelMaxPrims(LSL_Types.Vector3 pos, int sim_wide) { return 0; }
        public List<string> llGetParcelDetails(LSL_Types.Vector3 pos, List<string> param) { return new List<string>(); }

        //
        // OpenSim functions
        //
        public string osSetDynamicTextureURL(string dynamicID, string contentType, string url, string extraParams, int timer)
        {
            if (dynamicID == "")
            {
                IDynamicTextureManager textureManager = this.World.RequestModuleInterface<IDynamicTextureManager>();
                LLUUID createdTexture = textureManager.AddDynamicTextureURL(World.RegionInfo.SimUUID, this.m_host.UUID, contentType, url, extraParams, timer);
                return createdTexture.ToStringHyphenated();
            }
            else
            {
                //TODO update existing dynamic textures
            }

            return LLUUID.Zero.ToStringHyphenated();
        }

    }
}
