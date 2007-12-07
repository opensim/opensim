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
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS AS IS AND ANY
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

using System;
using System.Collections.Generic;
using Nini.Config;
using OpenSim.Region.Environment.Interfaces;
using OpenSim.Region.Environment.Scenes;
using OpenSim.Framework;
using OpenSim.Framework.Console;
using libsecondlife;


namespace OpenSim.Region.Environment.Modules
{
    public class SunModule : IRegionModule
    {
        private static double m_real_day = 24.0;
        private int m_frame_mod = 100;
        private double m_day_length;
        private int m_dilation;
        private int m_frame;
        private long m_start;

        private Scene m_scene;
        private LogBase m_log;

        public void Initialise(Scene scene, IConfigSource config)
        {
            m_start = DateTime.Now.Ticks;
            m_frame = 0;
            
            // Just in case they don't have the stanzas
            try {
                m_day_length = config.Configs["Sun"].GetDouble("day_length", 0.5);
                m_frame_mod = config.Configs["Sun"].GetInt("frame_rate", 100);
            } catch (Exception) {
                m_day_length = 0.5;
                m_frame_mod = 100;
            }
            
            m_dilation = (int)(m_real_day / m_day_length);
            m_scene = scene;
            m_log = MainLog.Instance;
            scene.EventManager.OnFrame += SunUpdate;
            scene.EventManager.OnNewClient += SunToClient;
        }

        public void PostInitialise()
        {
        }

        public void Close()
        {
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
            client.SendSunPos(SunPos(HourOfTheDay()), new LLVector3(0, 0.0f, 10.0f));
        }
        
        public void SunUpdate()
        {
            if (m_frame < m_frame_mod) {
                m_frame++;
                return;
            }
            // m_log.Verbose("SUN","I've got an update {0} => {1}", m_scene.RegionsInfo.RegionName, HourOfTheDay());
            List<ScenePresence> avatars = m_scene.GetAvatars();
            foreach (ScenePresence avatar in avatars)
            {
                avatar.ControllingClient.SendSunPos(SunPos(HourOfTheDay()), new LLVector3(0, 0.0f, 10.0f));
            }
            m_frame = 0;
        }

        // Hour of the Day figures out the hour of the day as a float.  The intent here is that we seed hour of the day with real time when the simulator starts, then run time forward faster based on time dilation factor.  This means that ticks don't get out of hand 
        private double HourOfTheDay() 
        {
            long m_addticks = (DateTime.Now.Ticks - m_start) * m_dilation;
            DateTime dt = new DateTime(m_start + m_addticks);
            return (double)dt.Hour + ((double)dt.Minute / 60.0);
        }

        private LLVector3 SunPos(double hour) 
        {
            // now we have our radian position
            double rad = (hour / m_real_day) * 2 * Math.PI - (Math.PI / 2.0);
            double z = Math.Sin(rad);
            double x = Math.Cos(rad);
            return new LLVector3((float)x, 0f, (float)z);
        }

        // TODO: clear this out.  This is here so that I remember to
        // figure out if we need those other packet fields that I've
        // left out so far
        //
        //        public void SendViewerTime(int phase)
        //  {
        //             Console.WriteLine("SunPhase: {0}", phase);
        //             SimulatorViewerTimeMessagePacket viewertime = new SimulatorViewerTimeMessagePacket();
        //             //viewertime.TimeInfo.SecPerDay = 86400;
        //             // viewertime.TimeInfo.SecPerYear = 31536000;
        //             viewertime.TimeInfo.SecPerDay = 1000;
        //             viewertime.TimeInfo.SecPerYear = 365000;
        //             viewertime.TimeInfo.SunPhase = 1;
        //             int sunPhase = (phase + 2)/2;
        //             if ((sunPhase < 6) || (sunPhase > 36))
        //             {
        //                 viewertime.TimeInfo.SunDirection = new LLVector3(0f, 0.8f, -0.8f);
        //                 Console.WriteLine("sending night");
        //             }
        //             else
        //             {
        //                 if (sunPhase < 12)
        //                 {
        //                     sunPhase = 12;
        //                 }
        //                 sunPhase = sunPhase - 12;
        //
        //                 float yValue = 0.1f*(sunPhase);
        //                 Console.WriteLine("Computed SunPhase: {0}, yValue: {1}", sunPhase, yValue);
        //                 if (yValue > 1.2f)
        //                 {
        //                     yValue = yValue - 1.2f;
        //                 }
        //                 if (yValue > 1)
        //                 {
        //                     yValue = 1;
        //                 }
        //                 if (yValue < 0)
        //                 {
        //                     yValue = 0;
        //                 }
        //                 if (sunPhase < 14)
        //                 {
        //                     yValue = 1 - yValue;
        //                 }
        //                 if (sunPhase < 12)
        //                 {
        //                     yValue *= -1;
        //                 }
        //                 viewertime.TimeInfo.SunDirection = new LLVector3(0f, yValue, 0.3f);
        //                 Console.WriteLine("sending sun update " + yValue);
        //             }
        //             viewertime.TimeInfo.SunAngVelocity = new LLVector3(0, 0.0f, 10.0f);
        //             viewertime.TimeInfo.UsecSinceStart = (ulong) Util.UnixTimeSinceEpoch();
        //             // OutPacket(viewertime);
        // }
    }
}