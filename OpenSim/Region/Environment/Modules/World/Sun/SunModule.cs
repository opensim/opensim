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
 *     * Neither the name of the OpenSim Project nor the
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
using System.Collections.Generic;
using libsecondlife;
using Nini.Config;
using OpenSim.Framework;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;

namespace OpenSim.Region.Environment.Modules
{
    public class SunModule : IRegionModule
    {

        private static readonly log4net.ILog m_log = log4net.LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private const double SeasonalTilt   =  0.03 * Math.PI;  // A daily shift of approximately 1.7188 degrees
        private const double AverageTilt    = -0.25 * Math.PI;  // A 45 degree tilt
        private const double SunCycle       =  2.0D * Math.PI;  // A perfect circle measured in radians
        private const double SeasonalCycle  =  2.0D * Math.PI;  // Ditto

        //
        //    Per Region Values
        //

        private bool   ready = false;

        // Configurable values
        private string m_mode           = "SL";
        private int    m_frame_mod      = 0;
        private double m_day_length     = 0;
        private int    m_year_length    = 0;
        private double m_day_night      = 0;
        // private double m_longitude      = 0;
        // private double m_latitude       = 0;
        // Configurable defaults                     Defaults close to SL
        private string d_mode           = "SL";
        private int    d_frame_mod      = 100;    // Every 10 seconds (actually less)
        private double d_day_length     = 4;      // A VW day is 4 RW hours long
        private int    d_year_length    = 60;     // There are 60 VW days in a VW year
        private double d_day_night      = 0.45;   // axis offset: ratio of light-to-dark, approx 1:3
        // private double d_longitude      = -73.53;
        // private double d_latitude       = 41.29;

        // Frame counter
        private uint   m_frame          = 0;

        // Cached Scene reference
        private Scene  m_scene          = null;

        // Calculated Once in the lifetime of a region
        private long  TicksToEpoch;              // Elapsed time for 1/1/1970
        private uint   SecondsPerSunCycle;        // Length of a virtual day in RW seconds
        private uint   SecondsPerYear;            // Length of a virtual year in RW seconds
        private double SunSpeed;                  // Rate of passage in radians/second
        private double SeasonSpeed;               // Rate of change for seasonal effects
        // private double HoursToRadians;            // Rate of change for seasonal effects
        private long TicksOffset = 0;                // seconds offset from UTC
        // Calculated every update
        private float  OrbitalPosition;           // Orbital placement at a point in time
        private double HorizonShift;              // Axis offset to skew day and night
        private double TotalDistanceTravelled;    // Distance since beginning of time (in radians)
        private double SeasonalOffset;            // Seaonal variation of tilt
        private float  Magnitude;                 // Normal tilt
        // private double VWTimeRatio;               // VW time as a ratio of real time

        // Working values
        private LLVector3 Position = new LLVector3(0,0,0);
        private LLVector3 Velocity = new LLVector3(0,0,0);
        private LLQuaternion  Tilt = new LLQuaternion(1,0,0,0);

        private long LindenHourOffset = 0;
        private bool sunFixed = false;

        private Dictionary<LLUUID, ulong> m_rootAgents = new Dictionary<LLUUID, ulong>();

        // Current time in elpased seconds since Jan 1st 1970
        private ulong CurrentTime
        {
            get {
                return (ulong)(((System.DateTime.Now.Ticks) - TicksToEpoch + TicksOffset + LindenHourOffset)/10000000);
            }
        }

        private float GetLindenEstateHourFromCurrentTime()
        {
            float ticksleftover = ((float)CurrentTime) % ((float)SecondsPerSunCycle);

            float hour = (24 * (ticksleftover / SecondsPerSunCycle)) + 6;

            return hour;
        }

        private void SetTimeByLindenHour(float LindenHour)
        {
            // Linden hour is 24 hours with a 6 hour offset.  6-30

            if (LindenHour - 6 == 0)
            {
                LindenHourOffset = 0;
                return;
            }

            // Remove LindenHourOffset to calculate it from LocalTime
            float ticksleftover = ((float)(((long)(CurrentTime * 10000000) - (long)LindenHourOffset)/ 10000000) % ((float)SecondsPerSunCycle));
            float hour = (24 * (ticksleftover / SecondsPerSunCycle));

            float offsethours = 0;

            if (LindenHour - 6 > hour)
            {
                offsethours = hour + ((LindenHour-6) - hour);
            }
            else
            {
                offsethours = hour - (hour - (LindenHour - 6));
            }
            //m_log.Debug("[OFFSET]: " + hour + " - " + LindenHour + " - " + offsethours.ToString());

            LindenHourOffset = (long)((float)offsethours * (36000000000/m_day_length));
            m_log.Info("[SUN]: Directive from the Estate Tools to set the sun phase to LindenHour " + GetLindenEstateHourFromCurrentTime().ToString());

        }
        // Called immediately after the module is loaded for a given region
        // i.e. Immediately after instance creation.

        public void Initialise(Scene scene, IConfigSource config)
        {

            m_log.Debug("[SUN] Initializing");

            m_scene = scene;

            m_frame = 0;

            TimeZone local = TimeZone.CurrentTimeZone;
            TicksOffset = local.GetUtcOffset(local.ToLocalTime(DateTime.Now)).Ticks;
            m_log.Debug("[SUN] localtime offset is " + TicksOffset);

            // Align ticks with Second Life

            TicksToEpoch = new System.DateTime(1970,1,1).Ticks;

            // Just in case they don't have the stanzas
            try
            {
                // Mode: determines how the sun is handled
                m_mode = config.Configs["Sun"].GetString("mode", d_mode);
                // Mode: determines how the sun is handled
                // m_latitude = config.Configs["Sun"].GetDouble("latitude", d_latitude);
                // Mode: determines how the sun is handled
                // m_longitude = config.Configs["Sun"].GetDouble("longitude", d_longitude);
                // Day length in decimal hours
                m_year_length = config.Configs["Sun"].GetInt("year_length", d_year_length);
                // Day length in decimal hours
                m_day_length  = config.Configs["Sun"].GetDouble("day_length", d_day_length);
                // Day to Night Ratio
                m_day_night   = config.Configs["Sun"].GetDouble("day_night_offset", d_day_night);
                // Update frequency in frames
                m_frame_mod   = config.Configs["Sun"].GetInt("update_interval", d_frame_mod);
            }
            catch (Exception e)
            {
                m_log.Debug("[SUN] Configuration access failed, using defaults. Reason: "+e.Message);
                m_mode        = d_mode;
                m_year_length = d_year_length;
                m_day_length  = d_day_length;
                m_day_night   = d_day_night;
                m_frame_mod   = d_frame_mod;
                // m_latitude    = d_latitude;
                // m_longitude   = d_longitude;
            }

            switch (m_mode)
            {
                case "T1":
                default:
                case "SL":
                    // Time taken to complete a cycle (day and season)

                    SecondsPerSunCycle = (uint) (m_day_length * 60 * 60);
                    SecondsPerYear     = (uint) (SecondsPerSunCycle*m_year_length);

                    // Ration of real-to-virtual time

                    // VWTimeRatio        = 24/m_day_length;

                    // Speed of rotation needed to complete a cycle in the
                    // designated period (day and season)

                    SunSpeed           = SunCycle/SecondsPerSunCycle;
                    SeasonSpeed        = SeasonalCycle/SecondsPerYear;

                    // Horizon translation

                    HorizonShift      = m_day_night; // Z axis translation
                    // HoursToRadians    = (SunCycle/24)*VWTimeRatio;

                    //  Insert our event handling hooks

                    scene.EventManager.OnFrame     += SunUpdate;
                    //scene.EventManager.OnNewClient += SunToClient;
                    scene.EventManager.OnMakeChildAgent += MakeChildAgent;
                    scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
                    scene.EventManager.OnClientClosed += ClientLoggedOut;
                    scene.EventManager.OnEstateToolsTimeUpdate += EstateToolsTimeUpdate;
                    scene.EventManager.OnGetSunLindenHour += GetLindenEstateHourFromCurrentTime;

                    ready = true;

                    m_log.Debug("[SUN] Mode is "+m_mode);
                    m_log.Debug("[SUN] Initialization completed. Day is "+SecondsPerSunCycle+" seconds, and year is "+m_year_length+" days");
                    m_log.Debug("[SUN] Axis offset is "+m_day_night);
                    m_log.Debug("[SUN] Positional data updated every "+m_frame_mod+" frames");

                    break;
            }
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
            ready = false;
            //  Remove our hooks
            m_scene.EventManager.OnFrame     -= SunUpdate;
           // m_scene.EventManager.OnNewClient -= SunToClient;
            m_scene.EventManager.OnMakeChildAgent -= MakeChildAgent;
            m_scene.EventManager.OnAvatarEnteringNewParcel -= AvatarEnteringParcel;
            m_scene.EventManager.OnClientClosed -= ClientLoggedOut;
            m_scene.EventManager.OnEstateToolsTimeUpdate -= EstateToolsTimeUpdate;
            m_scene.EventManager.OnGetSunLindenHour -= GetLindenEstateHourFromCurrentTime;
        }

        public string Name
        {
            get { return "SunModule"; }
        }

        public bool IsSharedModule
        {
            get { return false; }
        }

        public void SunToClient(IClientAPI client)
        {
            if (m_mode != "T1")
            {
                if (ready)
                {
                    if (!sunFixed)
                        GenSunPos();    // Generate shared values once
                    client.SendSunPos(Position, Velocity, CurrentTime, SecondsPerSunCycle, SecondsPerYear, OrbitalPosition);
                    m_log.Debug("[SUN] Initial update for new client");
                }
            }
        }

        public void SunUpdate()
        {
            if (((m_frame++%m_frame_mod) != 0) || !ready || sunFixed)
            {
                return;
            }

            GenSunPos();        // Generate shared values once

            List<ScenePresence> avatars = m_scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                if (!avatar.IsChildAgent)
                    avatar.ControllingClient.SendSunPos(Position, Velocity, CurrentTime, SecondsPerSunCycle, SecondsPerYear, OrbitalPosition);
            }

            // set estate settings for region access to sun position
            m_scene.RegionInfo.RegionSettings.SunVector = Position;
            //m_scene.RegionInfo.EstateSettings.sunHour = GetLindenEstateHourFromCurrentTime();
        }
        public void ForceSunUpdateToAllClients()
        {
            GenSunPos();        // Generate shared values once

            List<ScenePresence> avatars = m_scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                if (!avatar.IsChildAgent)
                    avatar.ControllingClient.SendSunPos(Position, Velocity, CurrentTime, SecondsPerSunCycle, SecondsPerYear, OrbitalPosition);
            }

            // set estate settings for region access to sun position
            m_scene.RegionInfo.RegionSettings.SunVector = Position;
            m_scene.RegionInfo.RegionSettings.SunPosition = GetLindenEstateHourFromCurrentTime();
        }
        /// <summary>
        /// Calculate the sun's orbital position and its velocity.
        /// </summary>

        private void GenSunPos()
        {

            TotalDistanceTravelled  = SunSpeed * CurrentTime;  // distance measured in radians
            OrbitalPosition         = (float) (TotalDistanceTravelled%SunCycle); // position measured in radians

            // TotalDistanceTravelled += HoursToRadians-(0.25*Math.PI)*Math.Cos(HoursToRadians)-OrbitalPosition;
            // OrbitalPosition         = (float) (TotalDistanceTravelled%SunCycle);

            SeasonalOffset          = SeasonSpeed * CurrentTime; // Present season determined as total radians travelled around season cycle

            Tilt.W                  = (float) (AverageTilt + (SeasonalTilt*Math.Sin(SeasonalOffset))); // Calculate seasonal orbital N/S tilt

            // m_log.Debug("[SUN] Total distance travelled = "+TotalDistanceTravelled+", present position = "+OrbitalPosition+".");
            // m_log.Debug("[SUN] Total seasonal progress = "+SeasonalOffset+", present tilt = "+Tilt.W+".");

            // The sun rotates about the Z axis

            Position.X = (float) Math.Cos(-TotalDistanceTravelled);
            Position.Y = (float) Math.Sin(-TotalDistanceTravelled);
            Position.Z = 0;

            // For interest we rotate it slightly about the X access.
            // Celestial tilt is a value that ranges .025

            Position   = LLVector3.Rot(Position,Tilt);

            // Finally we shift the axis so that more of the
            // circle is above the horizon than below. This
            // makes the nights shorter than the days.

            Position.Z = Position.Z + (float) HorizonShift;
            Position   = LLVector3.Norm(Position);

            // m_log.Debug("[SUN] Position("+Position.X+","+Position.Y+","+Position.Z+")");

            Velocity.X = 0;
            Velocity.Y = 0;
            Velocity.Z = (float) SunSpeed;

            // Correct angular velocity to reflect the seasonal rotation

            Magnitude  = LLVector3.Mag(Position);
            if (sunFixed)
            {
                Velocity.X = 0;
                Velocity.Y = 0;
                Velocity.Z = 0;
                return;
            }

            Velocity = LLVector3.Rot(Velocity, Tilt)*((float)(1.0/Magnitude));

            // m_log.Debug("[SUN] Velocity("+Velocity.X+","+Velocity.Y+","+Velocity.Z+")");

        }

        private void ClientLoggedOut(LLUUID AgentId)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(AgentId))
                {
                    m_rootAgents.Remove(AgentId);
                    m_log.Info("[SUN]: Removing " + AgentId + ". Agent logged out.");
                }
            }
        }

        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, LLUUID regionID)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(avatar.UUID))
                {
                    m_rootAgents[avatar.UUID] = avatar.RegionHandle;
                }
                else
                {
                    m_rootAgents.Add(avatar.UUID, avatar.RegionHandle);
                    SunToClient(avatar.ControllingClient);
                }
            }
            //m_log.Info("[FRIEND]: " + avatar.Name + " status:" + (!avatar.IsChildAgent).ToString());
        }

        private void MakeChildAgent(ScenePresence avatar)
        {
            lock (m_rootAgents)
            {
                if (m_rootAgents.ContainsKey(avatar.UUID))
                {
                    if (m_rootAgents[avatar.UUID] == avatar.RegionHandle)
                    {
                        m_rootAgents.Remove(avatar.UUID);
                    }
                }
            }
        }

        public void EstateToolsTimeUpdate(ulong regionHandle, bool FixedTime, bool useEstateTime, float LindenHour)
        {
            if (m_scene.RegionInfo.RegionHandle == regionHandle)
            {
                SetTimeByLindenHour(LindenHour);

                //if (useEstateTime)
                    //LindenHourOffset = 0;

                ForceSunUpdateToAllClients();
                sunFixed = FixedTime;
                if (sunFixed)
                    GenSunPos();


            }
        }
    }
}
