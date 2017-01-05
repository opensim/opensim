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
using System.Collections.Generic;
using System.Reflection;
using log4net;
using Mono.Addins;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.CoreModules
{
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "SunModule")]
    public class SunModule : ISunModule
    {
        /// <summary>
        /// Note:  Sun Hour can be a little deceaving.  Although it's based on a 24 hour clock
        /// it is not based on ~06:00 == Sun Rise.   Rather it is based on 00:00 being sun-rise.
        /// </summary>

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        //
        // Global Constants used to determine where in the sky the sun is
        //
        private const double m_SeasonalTilt   =  0.03 * Math.PI;  // A daily shift of approximately 1.7188 degrees
        private const double m_AverageTilt    = -0.25 * Math.PI;  // A 45 degree tilt
        private const double m_SunCycle       =  2.0D * Math.PI;  // A perfect circle measured in radians
        private const double m_SeasonalCycle  =  2.0D * Math.PI;  // Ditto

        //
        //    Per Region Values
        //

        private bool ready = false;

        // This solves a chick before the egg problem
        // the local SunFixedHour and SunFixed variables MUST be updated
        // at least once with the proper Region Settings before we start
        // updating those region settings in GenSunPos()
        private bool receivedEstateToolsSunUpdate = false;

        // Sun's position information is updated and sent to clients every m_UpdateInterval frames
        private int    m_UpdateInterval           = 0;

        // Number of real time hours per virtual day
        private double m_DayLengthHours           = 0;

        // Number of virtual days to a virtual year
        private int    m_YearLengthDays           = 0;

        // Ratio of Daylight hours to Night time hours.  This is accomplished by shifting the
        // sun's orbit above the horizon
        private double m_HorizonShift = 0;

        // Used to scale current and positional time to adjust length of an hour during day vs night.
        private double m_DayTimeSunHourScale;

        // private double m_longitude      = 0;
        // private double m_latitude       = 0;
        // Configurable defaults                     Defaults close to SL
        private int    d_frame_mod      = 100;    // Every 10 seconds (actually less)
        private double d_day_length     = 4;      // A VW day is 4 RW hours long
        private int    d_year_length    = 60;     // There are 60 VW days in a VW year
        private double d_day_night      = 0.5;   // axis offset: Default Hoizon shift to try and closely match the sun model in LL Viewer
        private double d_DayTimeSunHourScale = 0.5; // Day/Night hours are equal


        // private double d_longitude      = -73.53;
        // private double d_latitude       = 41.29;

        // Frame counter
        private uint   m_frame          = 0;

        // Cached Scene reference
        private Scene  m_scene          = null;

        // Calculated Once in the lifetime of a region
        private long TicksToEpoch;              // Elapsed time for 1/1/1970
        private uint SecondsPerSunCycle;        // Length of a virtual day in RW seconds
        private uint SecondsPerYear;            // Length of a virtual year in RW seconds
        private double SunSpeed;                // Rate of passage in radians/second
        private double SeasonSpeed;             // Rate of change for seasonal effects
        // private double HoursToRadians;          // Rate of change for seasonal effects
        private long TicksUTCOffset = 0;        // seconds offset from UTC
        // Calculated every update
        private float OrbitalPosition;          // Orbital placement at a point in time
        private double HorizonShift;            // Axis offset to skew day and night
        private double TotalDistanceTravelled;  // Distance since beginning of time (in radians)
        private double SeasonalOffset;          // Seaonal variation of tilt
        private float  Magnitude;               // Normal tilt
        // private double VWTimeRatio;             // VW time as a ratio of real time

        // Working values
        private Vector3 Position = Vector3.Zero;
        private Vector3 Velocity = Vector3.Zero;
        private Quaternion Tilt = new Quaternion(1.0f, 0.0f, 0.0f, 0.0f);

        // Used to fix the sun in the sky so it doesn't move based on current time
        private bool m_SunFixed = false;
        private float m_SunFixedHour = 0f;

        private const int TICKS_PER_SECOND = 10000000;

        private ulong m_CurrentTimeOffset = 0;

        // Current time in elapsed seconds since Jan 1st 1970
        private ulong CurrentTime
        {
            get
            {
                ulong ctime = (ulong)(((DateTime.Now.Ticks) - TicksToEpoch + TicksUTCOffset) / TICKS_PER_SECOND);
                return ctime + m_CurrentTimeOffset;
            }
        }

        // Time in seconds since UTC to use to calculate sun position.
        ulong PosTime = 0;

        /// <summary>
        /// Calculate the sun's orbital position and its velocity.
        /// </summary>
        private void GenSunPos()
        {
            // Time in seconds since UTC to use to calculate sun position.
            PosTime = CurrentTime;

            if (m_SunFixed)
            {
                // SunFixedHour represents the "hour of day" we would like
                // It's represented in 24hr time, with 0 hour being sun-rise
                // Because our day length is probably not 24hrs {LL is 6} we need to do a bit of math

                // Determine the current "day" from current time, so we can use "today"
                // to determine Seasonal Tilt and what'not

                // Integer math rounded is on purpose to drop fractional day, determines number
                // of virtual days since Epoch
                PosTime = CurrentTime / SecondsPerSunCycle;

                // Since we want number of seconds since Epoch, multiply back up
                PosTime *= SecondsPerSunCycle;

                // Then offset by the current Fixed Sun Hour
                // Fixed Sun Hour needs to be scaled to reflect the user configured Seconds Per Sun Cycle
                PosTime += (ulong)((m_SunFixedHour / 24.0) * (ulong)SecondsPerSunCycle);
            }
            else
            {
                if (m_DayTimeSunHourScale != 0.5f)
                {
                    ulong CurDaySeconds = CurrentTime % SecondsPerSunCycle;
                    double CurDayPercentage = (double)CurDaySeconds / SecondsPerSunCycle;

                    ulong DayLightSeconds = (ulong)(m_DayTimeSunHourScale * SecondsPerSunCycle);
                    ulong NightSeconds = SecondsPerSunCycle - DayLightSeconds;

                    PosTime = CurrentTime / SecondsPerSunCycle;
                    PosTime *= SecondsPerSunCycle;

                    if (CurDayPercentage < 0.5)
                    {
                        PosTime += (ulong)((CurDayPercentage / .5) * DayLightSeconds);
                    }
                    else
                    {
                        PosTime += DayLightSeconds;
                        PosTime += (ulong)(((CurDayPercentage - 0.5) / .5) * NightSeconds);
                    }
                }
            }

            TotalDistanceTravelled = SunSpeed * PosTime;  // distance measured in radians

            OrbitalPosition = (float)(TotalDistanceTravelled % m_SunCycle); // position measured in radians

            // TotalDistanceTravelled += HoursToRadians-(0.25*Math.PI)*Math.Cos(HoursToRadians)-OrbitalPosition;
            // OrbitalPosition         = (float) (TotalDistanceTravelled%SunCycle);

            SeasonalOffset = SeasonSpeed * PosTime; // Present season determined as total radians travelled around season cycle
            Tilt.W = (float)(m_AverageTilt + (m_SeasonalTilt * Math.Sin(SeasonalOffset))); // Calculate seasonal orbital N/S tilt

            // m_log.Debug("[SUN] Total distance travelled = "+TotalDistanceTravelled+", present position = "+OrbitalPosition+".");
            // m_log.Debug("[SUN] Total seasonal progress = "+SeasonalOffset+", present tilt = "+Tilt.W+".");

            // The sun rotates about the Z axis

            Position.X = (float)Math.Cos(-TotalDistanceTravelled);
            Position.Y = (float)Math.Sin(-TotalDistanceTravelled);
            Position.Z = 0;

            // For interest we rotate it slightly about the X access.
            // Celestial tilt is a value that ranges .025

            Position *= Tilt;

            // Finally we shift the axis so that more of the
            // circle is above the horizon than below. This
            // makes the nights shorter than the days.

            Position = Vector3.Normalize(Position);
            Position.Z = Position.Z + (float)HorizonShift;
            Position = Vector3.Normalize(Position);

            // m_log.Debug("[SUN] Position("+Position.X+","+Position.Y+","+Position.Z+")");

            Velocity.X = 0;
            Velocity.Y = 0;
            Velocity.Z = (float)SunSpeed;

            // Correct angular velocity to reflect the seasonal rotation

            Magnitude = Position.Length();
            if (m_SunFixed)
            {
                Velocity.X = 0;
                Velocity.Y = 0;
                Velocity.Z = 0;
            }
            else
            {
                Velocity = (Velocity * Tilt) * (1.0f / Magnitude);
            }

            // TODO: Decouple this, so we can get rid of Linden Hour info
            // Update Region with new Sun Vector
            // set estate settings for region access to sun position
            if (receivedEstateToolsSunUpdate)
            {
                m_scene.RegionInfo.RegionSettings.SunVector = Position;
            }
        }

        private float GetCurrentTimeAsLindenSunHour()
        {
            float curtime = m_SunFixed ? m_SunFixedHour : GetCurrentSunHour();
            return (curtime + 6.0f) % 24.0f;
        }

        #region INonSharedRegion Methods

        // Called immediately after the module is loaded for a given region
        // i.e. Immediately after instance creation.
        public void Initialise(IConfigSource config)
        {
            m_frame = 0;

            // This one puts an entry in the main help screen
//            m_scene.AddCommand("Regions", this, "sun", "sun", "Usage: sun [param] [value] - Get or Update Sun module paramater", null);

            TimeZone local = TimeZone.CurrentTimeZone;
            TicksUTCOffset = local.GetUtcOffset(local.ToLocalTime(DateTime.Now)).Ticks;
            m_log.DebugFormat("[SUN]: localtime offset is {0}", TicksUTCOffset);

            // Align ticks with Second Life

            TicksToEpoch = new DateTime(1970, 1, 1).Ticks;

            // Just in case they don't have the stanzas
            try
            {
                // Mode: determines how the sun is handled
                // m_latitude = config.Configs["Sun"].GetDouble("latitude", d_latitude);
                // Mode: determines how the sun is handled
                // m_longitude = config.Configs["Sun"].GetDouble("longitude", d_longitude);
                // Year length in days
                m_YearLengthDays = config.Configs["Sun"].GetInt("year_length", d_year_length);
                // Day length in decimal hours
                m_DayLengthHours  = config.Configs["Sun"].GetDouble("day_length", d_day_length);

                // Horizon shift, this is used to shift the sun's orbit, this affects the day / night ratio
                // must hard code to ~.5 to match sun position in LL based viewers
                m_HorizonShift   = config.Configs["Sun"].GetDouble("day_night_offset", d_day_night);

                // Scales the sun hours 0...12 vs 12...24, essentially makes daylight hours longer/shorter vs nighttime hours
                m_DayTimeSunHourScale = config.Configs["Sun"].GetDouble("day_time_sun_hour_scale", d_DayTimeSunHourScale);

                // Update frequency in frames
                m_UpdateInterval   = config.Configs["Sun"].GetInt("update_interval", d_frame_mod);
            }
            catch (Exception e)
            {
                m_log.Debug("[SUN]: Configuration access failed, using defaults. Reason: " + e.Message);
                m_YearLengthDays = d_year_length;
                m_DayLengthHours  = d_day_length;
                m_HorizonShift   = d_day_night;
                m_UpdateInterval   = d_frame_mod;
                m_DayTimeSunHourScale = d_DayTimeSunHourScale;

                // m_latitude    = d_latitude;
                // m_longitude   = d_longitude;
            }

            SecondsPerSunCycle = (uint) (m_DayLengthHours * 60 * 60);
            SecondsPerYear     = (uint) (SecondsPerSunCycle*m_YearLengthDays);

            // Ration of real-to-virtual time

            // VWTimeRatio        = 24/m_day_length;

            // Speed of rotation needed to complete a cycle in the
            // designated period (day and season)

            SunSpeed           = m_SunCycle/SecondsPerSunCycle;
            SeasonSpeed        = m_SeasonalCycle/SecondsPerYear;

            // Horizon translation

            HorizonShift      = m_HorizonShift; // Z axis translation
            // HoursToRadians    = (SunCycle/24)*VWTimeRatio;

            m_log.Debug("[SUN]: Initialization completed. Day is " + SecondsPerSunCycle + " seconds, and year is " + m_YearLengthDays + " days");
            m_log.Debug("[SUN]: Axis offset is " + m_HorizonShift);
            m_log.Debug("[SUN]: Percentage of time for daylight " + m_DayTimeSunHourScale);
            m_log.Debug("[SUN]: Positional data updated every " + m_UpdateInterval + " frames");
        }

        public Type ReplaceableInterface
        {
            get { return null; }
        }

        public void AddRegion(Scene scene)
        {
            m_scene = scene;
            //  Insert our event handling hooks

            scene.EventManager.OnFrame += SunUpdate;
            scene.EventManager.OnAvatarEnteringNewParcel += AvatarEnteringParcel;
            scene.EventManager.OnEstateToolsSunUpdate += EstateToolsSunUpdate;
            scene.EventManager.OnGetCurrentTimeAsLindenSunHour += GetCurrentTimeAsLindenSunHour;

            scene.RegisterModuleInterface<ISunModule>(this);

            // This one enables the ability to type just "sun" without any parameters
            //            m_scene.AddCommand("Regions", this, "sun", "", "", HandleSunConsoleCommand);
            foreach (KeyValuePair<string, string> kvp in GetParamList())
            {
                string sunCommand = string.Format("sun {0}", kvp.Key);
                m_scene.AddCommand("Regions", this, sunCommand, string.Format("{0} [<value>]", sunCommand), kvp.Value, "", HandleSunConsoleCommand);
            }
            m_scene.AddCommand("Regions", this, "sun help", "sun help", "list parameters that can be changed", "", HandleSunConsoleCommand);
            m_scene.AddCommand("Regions", this, "sun list", "sun list", "list parameters that can be changed", "", HandleSunConsoleCommand);
            ready = true;
        }

        public void RemoveRegion(Scene scene)
        {
            ready = false;

            // Remove our hooks
            m_scene.EventManager.OnFrame -= SunUpdate;
            m_scene.EventManager.OnAvatarEnteringNewParcel -= AvatarEnteringParcel;
            m_scene.EventManager.OnEstateToolsSunUpdate -= EstateToolsSunUpdate;
            m_scene.EventManager.OnGetCurrentTimeAsLindenSunHour -= GetCurrentTimeAsLindenSunHour;
        }

        public void RegionLoaded(Scene scene)
        {
        }

        public void Close()
        {
        }

        public string Name
        {
            get { return "SunModule"; }
        }

        #endregion

        #region EventManager Events

        public void SunToClient(IClientAPI client)
        {
            if (ready)
            {
                if (m_SunFixed)
                {
                    // m_log.DebugFormat("[SUN]: Fixed SunHour {0}, Position {1}, PosTime {2}, OrbitalPosition : {3} ",
                    //                   m_SunFixedHour, Position.ToString(), PosTime.ToString(), OrbitalPosition.ToString());
                    client.SendSunPos(Position, Velocity, PosTime, SecondsPerSunCycle, SecondsPerYear, OrbitalPosition);
                }
                else
                {
                    // m_log.DebugFormat("[SUN]: SunHour {0}, Position {1}, PosTime {2}, OrbitalPosition : {3} ",
                    //                  m_SunFixedHour, Position.ToString(), PosTime.ToString(), OrbitalPosition.ToString());
                    client.SendSunPos(Position, Velocity, CurrentTime, SecondsPerSunCycle, SecondsPerYear, OrbitalPosition);
                }
            }
        }

        public void SunUpdate()
        {
            if (((m_frame++ % m_UpdateInterval) != 0) || !ready || m_SunFixed || !receivedEstateToolsSunUpdate)
                return;

            GenSunPos();        // Generate shared values once

            SunUpdateToAllClients();
        }

        /// <summary>
        /// When an avatar enters the region, it's probably a good idea to send them the current sun info
        /// </summary>
        /// <param name="avatar"></param>
        /// <param name="localLandID"></param>
        /// <param name="regionID"></param>
        private void AvatarEnteringParcel(ScenePresence avatar, int localLandID, UUID regionID)
        {
            SunToClient(avatar.ControllingClient);
        }

        public void EstateToolsSunUpdate(ulong regionHandle)
        {
            if (m_scene.RegionInfo.RegionHandle == regionHandle)
            {
                float sunFixedHour;
                bool fixedSun;

                if (m_scene.RegionInfo.RegionSettings.UseEstateSun)
                {
                    sunFixedHour = (float)m_scene.RegionInfo.EstateSettings.SunPosition;
                    fixedSun = m_scene.RegionInfo.EstateSettings.FixedSun;
                }
                else
                {
                    sunFixedHour = (float)m_scene.RegionInfo.RegionSettings.SunPosition - 6.0f;
                    fixedSun = m_scene.RegionInfo.RegionSettings.FixedSun;
                }

                // Must limit the Sun Hour to 0 ... 24
                while (sunFixedHour > 24.0f)
                    sunFixedHour -= 24;

                while (sunFixedHour < 0)
                    sunFixedHour += 24;

                m_SunFixedHour = sunFixedHour;
                m_SunFixed = fixedSun;

                // m_log.DebugFormat("[SUN]: Sun Settings Update: Fixed Sun? : {0}", m_SunFixed.ToString());
                // m_log.DebugFormat("[SUN]: Sun Settings Update: Sun Hour   : {0}", m_SunFixedHour.ToString());

                receivedEstateToolsSunUpdate = true;

                // Generate shared values
                GenSunPos();

                // When sun settings are updated, we should update all clients with new settings.
                SunUpdateToAllClients();

                // m_log.DebugFormat("[SUN]: PosTime : {0}", PosTime.ToString());
            }
        }

        #endregion

        private void SunUpdateToAllClients()
        {
            m_scene.ForEachRootClient(delegate(IClientAPI client)
            {
                SunToClient(client);
            });
        }

        #region ISunModule Members

        public double GetSunParameter(string param)
        {
            switch (param.ToLower())
            {
                case "year_length":
                    return m_YearLengthDays;

                case "day_length":
                    return m_DayLengthHours;

                case "day_night_offset":
                    return m_HorizonShift;

                case "day_time_sun_hour_scale":
                    return m_DayTimeSunHourScale;

                case "update_interval":
                    return m_UpdateInterval;

                case "current_time":
                    return GetCurrentTimeAsLindenSunHour();

                default:
                    throw new Exception("Unknown sun parameter.");
            }
        }

        public void SetSunParameter(string param, double value)
        {
            switch (param)
            {
                case "year_length":
                    m_YearLengthDays = (int)value;
                    SecondsPerYear = (uint) (SecondsPerSunCycle*m_YearLengthDays);
                    SeasonSpeed = m_SeasonalCycle/SecondsPerYear;
                    break;

                case "day_length":
                    m_DayLengthHours = value;
                    SecondsPerSunCycle = (uint) (m_DayLengthHours * 60 * 60);
                    SecondsPerYear = (uint) (SecondsPerSunCycle*m_YearLengthDays);
                    SunSpeed = m_SunCycle/SecondsPerSunCycle;
                    SeasonSpeed = m_SeasonalCycle/SecondsPerYear;
                    break;

                case "day_night_offset":
                    m_HorizonShift = value;
                    HorizonShift = m_HorizonShift;
                    break;

                case "day_time_sun_hour_scale":
                    m_DayTimeSunHourScale = value;
                    break;

                case "update_interval":
                    m_UpdateInterval = (int)value;
                    break;

                case "current_time":
                    value = (value + 18.0) % 24.0;
                    // set the current offset so that the effective sun time is the parameter
                    m_CurrentTimeOffset = 0; // clear this first so we use raw time
                    m_CurrentTimeOffset = (ulong)(SecondsPerSunCycle * value/ 24.0) - (CurrentTime % SecondsPerSunCycle);
                    break;

                default:
                    throw new Exception("Unknown sun parameter.");
            }

            // Generate shared values
            GenSunPos();

            // When sun settings are updated, we should update all clients with new settings.
            SunUpdateToAllClients();
        }

        public float GetCurrentSunHour()
        {
            float ticksleftover = CurrentTime % SecondsPerSunCycle;

            return (24.0f * (ticksleftover / SecondsPerSunCycle));
        }

        #endregion

        public void HandleSunConsoleCommand(string module, string[] cmdparams)
        {
            if (m_scene.ConsoleScene() == null)
            {
                // FIXME: If console region is root then this will be printed by every module.  Currently, there is no
                // way to prevent this, short of making the entire module shared (which is complete overkill).
                // One possibility is to return a bool to signal whether the module has completely handled the command
                m_log.InfoFormat("[Sun]: Please change to a specific region in order to set Sun parameters.");
                return;
            }

            if (m_scene.ConsoleScene() != m_scene)
            {
                m_log.InfoFormat("[Sun]: Console Scene is not my scene.");
                return;
            }

            m_log.InfoFormat("[Sun]: Processing command.");

            foreach (string output in ParseCmdParams(cmdparams))
            {
                MainConsole.Instance.Output(output);
            }
        }

        private Dictionary<string, string> GetParamList()
        {
            Dictionary<string, string> Params = new Dictionary<string, string>();

            Params.Add("year_length", "number of days to a year");
            Params.Add("day_length", "number of hours to a day");
            Params.Add("day_night_offset", "induces a horizon shift");
            Params.Add("update_interval", "how often to update the sun's position in frames");
            Params.Add("day_time_sun_hour_scale", "scales day light vs nite hours to change day/night ratio");
            Params.Add("current_time", "time in seconds of the simulator");

            return Params;
        }

        private List<string> ParseCmdParams(string[] args)
        {
            List<string> Output = new List<string>();

            if ((args.Length == 1) || (args[1].ToLower() == "help") || (args[1].ToLower() == "list"))
            {
                Output.Add("The following parameters can be changed or viewed:");
                foreach (KeyValuePair<string, string> kvp in GetParamList())
                {
                    Output.Add(String.Format("{0} - {1}",kvp.Key, kvp.Value));
                }
                return Output;
            }

            if (args.Length == 2)
            {
                try
                {
                    double value = GetSunParameter(args[1]);
                    Output.Add(String.Format("Parameter {0} is {1}.", args[1], value.ToString()));
                }
                catch (Exception)
                {
                    Output.Add(String.Format("Unknown parameter {0}.", args[1]));
                }

            }
            else if (args.Length == 3)
            {
                double value = 0.0;
                if (! double.TryParse(args[2], out value))
                {
                    Output.Add(String.Format("The parameter value {0} is not a valid number.", args[2]));
                    return Output;
                }

                SetSunParameter(args[1].ToLower(), value);
                Output.Add(String.Format("Parameter {0} set to {1}.", args[1], value.ToString()));
            }

            return Output;
        }
    }
}
