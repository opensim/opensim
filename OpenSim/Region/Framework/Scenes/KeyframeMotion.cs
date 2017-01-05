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
using System.Timers;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.PhysicsModules.SharedBase;
using OpenSim.Region.Framework.Scenes.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using Timer = System.Timers.Timer;
using log4net;

namespace OpenSim.Region.Framework.Scenes
{
    public class KeyframeTimer
    {
        private static Dictionary<Scene, KeyframeTimer> m_timers =
                new Dictionary<Scene, KeyframeTimer>();

        private Timer m_timer;
        private Dictionary<KeyframeMotion, object> m_motions = new Dictionary<KeyframeMotion, object>();
        private object m_lockObject = new object();
        private object m_timerLock = new object();
        private const double m_tickDuration = 50.0;

        public double TickDuration
        {
            get { return m_tickDuration; }
        }

        public KeyframeTimer(Scene scene)
        {
            m_timer = new Timer();
            m_timer.Interval = TickDuration;
            m_timer.AutoReset = true;
            m_timer.Elapsed += OnTimer;
        }

        public void Start()
        {
            lock (m_timer)
            {
                if (!m_timer.Enabled)
                    m_timer.Start();
            }
        }

        private void OnTimer(object sender, ElapsedEventArgs ea)
        {
            if (!Monitor.TryEnter(m_timerLock))
                return;

            try
            {
                List<KeyframeMotion> motions;

                lock (m_lockObject)
                {
                    motions = new List<KeyframeMotion>(m_motions.Keys);
                }

                foreach (KeyframeMotion m in motions)
                {
                    try
                    {
                        m.OnTimer(TickDuration);
                    }
                    catch (Exception)
                    {
                        // Don't stop processing
                    }
                }
            }
            catch (Exception)
            {
                // Keep running no matter what
            }
            finally
            {
                Monitor.Exit(m_timerLock);
            }
        }

        public static void Add(KeyframeMotion motion)
        {
            KeyframeTimer timer;

            if (motion.Scene == null)
                return;

            lock (m_timers)
            {
                if (!m_timers.TryGetValue(motion.Scene, out timer))
                {
                    timer = new KeyframeTimer(motion.Scene);
                    m_timers[motion.Scene] = timer;

                    if (!SceneManager.Instance.AllRegionsReady)
                    {
                        // Start the timers only once all the regions are ready. This is required
                        // when using megaregions, because the megaregion is correctly configured
                        // only after all the regions have been loaded. (If we don't do this then
                        // when the prim moves it might think that it crossed into a region.)
                        SceneManager.Instance.OnRegionsReadyStatusChange += delegate(SceneManager sm)
                        {
                            if (sm.AllRegionsReady)
                                timer.Start();
                        };
                    }

                    // Check again, in case the regions were started while we were adding the event handler
                    if (SceneManager.Instance.AllRegionsReady)
                    {
                        timer.Start();
                    }
                }
            }

            lock (timer.m_lockObject)
            {
                timer.m_motions[motion] = null;
            }
        }

        public static void Remove(KeyframeMotion motion)
        {
            KeyframeTimer timer;

            if (motion.Scene == null)
                return;

            lock (m_timers)
            {
                if (!m_timers.TryGetValue(motion.Scene, out timer))
                {
                    return;
                }
            }

            lock (timer.m_lockObject)
            {
                timer.m_motions.Remove(motion);
            }
        }
    }

    [Serializable]
    public class KeyframeMotion
    {
        //private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public enum PlayMode : int
        {
            Forward = 0,
            Reverse = 1,
            Loop = 2,
            PingPong = 3
        };

        [Flags]
        public enum DataFormat : int
        {
            Translation = 2,
            Rotation = 1
        }

        [Serializable]
        public struct Keyframe
        {
            public Vector3? Position;
            public Quaternion? Rotation;
            public Quaternion StartRotation;
            public int TimeMS;
            public int TimeTotal;
            public Vector3 AngularVelocity;
            public Vector3 StartPosition;
        };

        private Vector3 m_serializedPosition;
        private Vector3 m_basePosition;
        private Quaternion m_baseRotation;

        private Keyframe m_currentFrame;

        private List<Keyframe> m_frames = new List<Keyframe>();

        private Keyframe[] m_keyframes;

        // skip timer events.
        //timer.stop doesn't assure there aren't event threads still being fired
        [NonSerialized()]
        private bool m_timerStopped;

        [NonSerialized()]
        private bool m_isCrossing;

        [NonSerialized()]
        private bool m_waitingCrossing;

        // retry position for cross fail
        [NonSerialized()]
        private Vector3 m_nextPosition;

        [NonSerialized()]
        private SceneObjectGroup m_group;

        private PlayMode m_mode = PlayMode.Forward;
        private DataFormat m_data = DataFormat.Translation | DataFormat.Rotation;

        private bool m_running = false;

        [NonSerialized()]
        private bool m_selected = false;

        private int m_iterations = 0;

        private int m_skipLoops = 0;

        [NonSerialized()]
        private Scene m_scene;

        public Scene Scene
        {
            get { return m_scene; }
        }

        public DataFormat Data
        {
            get { return m_data; }
        }

        public bool Selected
        {
            set
            {
                if (m_group != null)
                {
                    if (!value)
                    {
                        // Once we're let go, recompute positions
                        if (m_selected)
                            UpdateSceneObject(m_group);
                    }
                    else
                    {
                        // Save selection position in case we get moved
                        if (!m_selected)
                        {
                            StopTimer();
                            m_serializedPosition = m_group.AbsolutePosition;
                        }
                    }
                }
                m_isCrossing = false;
                m_waitingCrossing = false;
                m_selected = value;
            }
        }

        private void StartTimer()
        {
            lock (m_frames)
            {
                KeyframeTimer.Add(this);
                m_timerStopped = false;
            }
        }

        private void StopTimer()
        {
            lock (m_frames)
                m_timerStopped = true;
        }

        public static KeyframeMotion FromData(SceneObjectGroup grp, Byte[] data)
        {
            KeyframeMotion newMotion = null;

            try
            {
                using (MemoryStream ms = new MemoryStream(data))
                {
                    BinaryFormatter fmt = new BinaryFormatter();
                    newMotion = (KeyframeMotion)fmt.Deserialize(ms);
                }

                newMotion.m_group = grp;

                if (grp != null)
                {
                    newMotion.m_scene = grp.Scene;
                    if (grp.IsSelected)
                        newMotion.m_selected = true;
                }

                newMotion.m_timerStopped = false;
                newMotion.m_running = true;
                newMotion.m_isCrossing = false;
                newMotion.m_waitingCrossing = false;
            }
            catch
            {
                newMotion = null;
            }

            return newMotion;
        }

        public void UpdateSceneObject(SceneObjectGroup grp)
        {
            m_isCrossing = false;
            m_waitingCrossing = false;
            StopTimer();

            if (grp == null)
                return;

            m_group = grp;
            m_scene = grp.Scene;


            lock (m_frames)
            {
                Vector3 grppos = grp.AbsolutePosition;
                Vector3 offset = grppos - m_serializedPosition;
                // avoid doing it more than once
                // current this will happen draging a prim to other region
                m_serializedPosition = grppos;

                m_basePosition += offset;
                m_currentFrame.Position += offset;

                m_nextPosition += offset;

                for (int i = 0; i < m_frames.Count; i++)
                {
                    Keyframe k = m_frames[i];
                    k.Position += offset;
                    m_frames[i] = k;
                }
            }

            if (m_running)
                Start();
        }

        public KeyframeMotion(SceneObjectGroup grp, PlayMode mode, DataFormat data)
        {
            m_mode = mode;
            m_data = data;

            m_group = grp;
            if (grp != null)
            {
                m_basePosition = grp.AbsolutePosition;
                m_baseRotation = grp.GroupRotation;
                m_scene = grp.Scene;
            }

            m_timerStopped = true;
            m_isCrossing = false;
            m_waitingCrossing = false;
        }

        public void SetKeyframes(Keyframe[] frames)
        {
            m_keyframes = frames;
        }

        public KeyframeMotion Copy(SceneObjectGroup newgrp)
        {
            StopTimer();

            KeyframeMotion newmotion = new KeyframeMotion(null, m_mode, m_data);

            newmotion.m_group = newgrp;
            newmotion.m_scene = newgrp.Scene;

            if (m_keyframes != null)
            {
                newmotion.m_keyframes = new Keyframe[m_keyframes.Length];
                m_keyframes.CopyTo(newmotion.m_keyframes, 0);
            }

            lock (m_frames)
            {
                newmotion.m_frames = new List<Keyframe>(m_frames);

                newmotion.m_basePosition = m_basePosition;
                newmotion.m_baseRotation = m_baseRotation;

                if (m_selected)
                    newmotion.m_serializedPosition = m_serializedPosition;
                else
                {
                    if (m_group != null)
                        newmotion.m_serializedPosition = m_group.AbsolutePosition;
                    else
                        newmotion.m_serializedPosition = m_serializedPosition;
                }

                newmotion.m_currentFrame = m_currentFrame;

                newmotion.m_iterations = m_iterations;
                newmotion.m_running = m_running;
            }

            if (m_running && !m_waitingCrossing)
                StartTimer();

            return newmotion;
        }

        public void Delete()
        {
            m_running = false;
            StopTimer();
            m_isCrossing = false;
            m_waitingCrossing = false;
            m_frames.Clear();
            m_keyframes = null;
        }

        public void Start()
        {
            m_isCrossing = false;
            m_waitingCrossing = false;
            if (m_keyframes != null && m_group != null && m_keyframes.Length > 0)
            {
                StartTimer();
                m_running = true;
                m_group.Scene.EventManager.TriggerMovingStartEvent(m_group.RootPart.LocalId);
            }
            else
            {
                StopTimer();
                m_running = false;
            }
        }

        public void Stop()
        {
            StopTimer();
            m_running = false;
            m_isCrossing = false;
            m_waitingCrossing = false;

            m_basePosition = m_group.AbsolutePosition;
            m_baseRotation = m_group.GroupRotation;

            m_group.RootPart.Velocity = Vector3.Zero;
            m_group.RootPart.AngularVelocity = Vector3.Zero;
            m_group.SendGroupRootTerseUpdate();
//            m_group.RootPart.ScheduleTerseUpdate();
            m_frames.Clear();
        }

        public void Pause()
        {
            StopTimer();
            m_running = false;

            m_group.RootPart.Velocity = Vector3.Zero;
            m_group.RootPart.AngularVelocity = Vector3.Zero;
            m_group.SendGroupRootTerseUpdate();
//            m_group.RootPart.ScheduleTerseUpdate();
        }

        public void Suspend()
        {
            lock (m_frames)
            {
                if (m_timerStopped)
                    return;
                m_timerStopped = true;
            }
        }

        public void Resume()
        {
            lock (m_frames)
            {
                if (!m_timerStopped)
                    return;
                if (m_running && !m_waitingCrossing)
                    StartTimer();
            }
        }

        private void GetNextList()
        {
            m_frames.Clear();
            Vector3 pos = m_basePosition;
            Quaternion rot = m_baseRotation;

            if (m_mode == PlayMode.Loop || m_mode == PlayMode.PingPong || m_iterations == 0)
            {
                int direction = 1;
                if (m_mode == PlayMode.Reverse || ((m_mode == PlayMode.PingPong) && ((m_iterations & 1) != 0)))
                    direction = -1;

                int start = 0;
                int end = m_keyframes.Length;

                if (direction < 0)
                {
                    start = m_keyframes.Length - 1;
                    end = -1;
                }

                for (int i = start; i != end ; i += direction)
                {
                    Keyframe k = m_keyframes[i];

                    k.StartPosition = pos;
                    if (k.Position.HasValue)
                    {
                        k.Position = (k.Position * direction);
//                        k.Velocity = (Vector3)k.Position / (k.TimeMS / 1000.0f);
                        k.Position += pos;
                    }
                    else
                    {
                        k.Position = pos;
//                        k.Velocity = Vector3.Zero;
                    }

                    k.StartRotation = rot;
                    if (k.Rotation.HasValue)
                    {
                        if (direction == -1)
                            k.Rotation = Quaternion.Conjugate((Quaternion)k.Rotation);
                        k.Rotation = rot * k.Rotation;
                    }
                    else
                    {
                        k.Rotation = rot;
                    }

/* ang vel not in use for now

                    float angle = 0;

                    float aa = k.StartRotation.X * k.StartRotation.X + k.StartRotation.Y * k.StartRotation.Y + k.StartRotation.Z * k.StartRotation.Z + k.StartRotation.W * k.StartRotation.W;
                    float bb = ((Quaternion)k.Rotation).X * ((Quaternion)k.Rotation).X + ((Quaternion)k.Rotation).Y * ((Quaternion)k.Rotation).Y + ((Quaternion)k.Rotation).Z * ((Quaternion)k.Rotation).Z + ((Quaternion)k.Rotation).W * ((Quaternion)k.Rotation).W;
                    float aa_bb = aa * bb;

                    if (aa_bb == 0)
                    {
                        angle = 0;
                    }
                    else
                    {
                        float ab = k.StartRotation.X * ((Quaternion)k.Rotation).X +
                                   k.StartRotation.Y * ((Quaternion)k.Rotation).Y +
                                   k.StartRotation.Z * ((Quaternion)k.Rotation).Z +
                                   k.StartRotation.W * ((Quaternion)k.Rotation).W;
                        float q = (ab * ab) / aa_bb;

                        if (q > 1.0f)
                        {
                            angle = 0;
                        }
                        else
                        {
                            angle = (float)Math.Acos(2 * q - 1);
                        }
                    }

                    k.AngularVelocity = (new Vector3(0, 0, 1) * (Quaternion)k.Rotation) * (angle / (k.TimeMS / 1000));
 */
                    k.TimeTotal = k.TimeMS;

                    m_frames.Add(k);

                    pos = (Vector3)k.Position;
                    rot = (Quaternion)k.Rotation;

                }

                m_basePosition = pos;
                m_baseRotation = rot;

                m_iterations++;
            }
        }

        public void OnTimer(double tickDuration)
        {
            if (!Monitor.TryEnter(m_frames))
                return;
            if (m_timerStopped)
                KeyframeTimer.Remove(this);
            else
                DoOnTimer(tickDuration);
            Monitor.Exit(m_frames);
        }

        private void Done()
        {
            KeyframeTimer.Remove(this);
            m_timerStopped = true;
            m_running = false;
            m_isCrossing = false;
            m_waitingCrossing = false;

            m_basePosition = m_group.AbsolutePosition;
            m_baseRotation = m_group.GroupRotation;

            m_group.RootPart.Velocity = Vector3.Zero;
            m_group.RootPart.AngularVelocity = Vector3.Zero;
            m_group.SendGroupRootTerseUpdate();
            //            m_group.RootPart.ScheduleTerseUpdate();
            m_frames.Clear();
        }

        private void DoOnTimer(double tickDuration)
        {
            if (m_skipLoops > 0)
            {
                m_skipLoops--;
                return;
            }

            if (m_group == null)
                return;

            bool update = false;

            if (m_selected)
            {
                if (m_group.RootPart.Velocity != Vector3.Zero)
                {
                    m_group.RootPart.Velocity = Vector3.Zero;
                    m_group.SendGroupRootTerseUpdate();
                }
                return;
            }

            if (m_isCrossing)
            {
                // if crossing and timer running then cross failed
                // wait some time then
                // retry to set the position that evtually caused the outbound
                // if still outside region this will call startCrossing below
                m_isCrossing = false;
                m_group.AbsolutePosition = m_nextPosition;
                if (!m_isCrossing)
                {
                    StopTimer();
                    StartTimer();
                }
                return;
            }

            if (m_frames.Count == 0)
            {
                lock (m_frames)
                {
                    GetNextList();

                    if (m_frames.Count == 0)
                    {
                        Done();
                        m_group.Scene.EventManager.TriggerMovingEndEvent(m_group.RootPart.LocalId);
                        return;
                    }

                    m_currentFrame = m_frames[0];
                    m_currentFrame.TimeMS += (int)tickDuration;
                }
                //force a update on a keyframe transition
                m_nextPosition = m_group.AbsolutePosition;
                update = true;
            }

            m_currentFrame.TimeMS -= (int)tickDuration;

            // Do the frame processing
            double remainingSteps = (double)m_currentFrame.TimeMS / tickDuration;

            if (remainingSteps <= 0.0)
            {
                m_group.RootPart.Velocity = Vector3.Zero;
                m_group.RootPart.AngularVelocity = Vector3.Zero;

                m_nextPosition = (Vector3)m_currentFrame.Position;
                m_group.AbsolutePosition = m_nextPosition;

                // we are sending imediate updates, no doing force a extra terseUpdate
                // m_group.UpdateGroupRotationR((Quaternion)m_currentFrame.Rotation);

                m_group.RootPart.RotationOffset = (Quaternion)m_currentFrame.Rotation;

                lock (m_frames)
                {
                    m_frames.RemoveAt(0);
                    if (m_frames.Count > 0)
                        m_currentFrame = m_frames[0];
                }

                update = true;
            }
            else
            {
                float completed = ((float)m_currentFrame.TimeTotal - (float)m_currentFrame.TimeMS) / (float)m_currentFrame.TimeTotal;
                bool lastStep = m_currentFrame.TimeMS <= tickDuration;

                Vector3 v = (Vector3)m_currentFrame.Position - m_group.AbsolutePosition;
                Vector3 motionThisFrame = v / (float)remainingSteps;
                v = v * 1000 / m_currentFrame.TimeMS;

                m_nextPosition = m_group.AbsolutePosition + motionThisFrame;

                if (Vector3.Mag(motionThisFrame) >= 0.05f)
                    update = true;

                //int totalSteps = m_currentFrame.TimeTotal / (int)tickDuration;
                //m_log.DebugFormat("KeyframeMotion.OnTimer: step {0}/{1}, curPosition={2}, finalPosition={3}, motionThisStep={4} (scene {5})",
                //    totalSteps - remainingSteps + 1, totalSteps, m_group.AbsolutePosition, m_currentFrame.Position, motionThisStep, m_scene.RegionInfo.RegionName);

                if ((Quaternion)m_currentFrame.Rotation != m_group.GroupRotation)
                {
                    Quaternion current = m_group.GroupRotation;

                    Quaternion step = Quaternion.Slerp(m_currentFrame.StartRotation, (Quaternion)m_currentFrame.Rotation, completed);
                    step.Normalize();
                    /* use simpler change detection
                    * float angle = 0;

                                        float aa = current.X * current.X + current.Y * current.Y + current.Z * current.Z + current.W * current.W;
                                        float bb = step.X * step.X + step.Y * step.Y + step.Z * step.Z + step.W * step.W;
                                        float aa_bb = aa * bb;

                                        if (aa_bb == 0)
                                        {
                                            angle = 0;
                                        }
                                        else
                                        {
                                            float ab = current.X * step.X +
                                                       current.Y * step.Y +
                                                       current.Z * step.Z +
                                                       current.W * step.W;
                                            float q = (ab * ab) / aa_bb;

                                            if (q > 1.0f)
                                            {
                                                angle = 0;
                                            }
                                            else
                                            {
                                                angle = (float)Math.Acos(2 * q - 1);
                                            }
                                        }

                                        if (angle > 0.01f)
                    */
                    if (Math.Abs(step.X - current.X) > 0.001f
                        || Math.Abs(step.Y - current.Y) > 0.001f
                        || Math.Abs(step.Z - current.Z) > 0.001f)
                    // assuming w is a dependente var
                    {
//                                m_group.UpdateGroupRotationR(step);
                        m_group.RootPart.RotationOffset = step;

                        //m_group.RootPart.UpdateAngularVelocity(m_currentFrame.AngularVelocity / 2);
                        update = true;
                    }
                }
            }

            if (update)
            {
                m_group.AbsolutePosition = m_nextPosition;
                m_group.SendGroupRootTerseUpdate();
            }
        }

        public Byte[] Serialize()
        {
            bool timerWasStopped;
            lock (m_frames)
            {
                timerWasStopped = m_timerStopped;
            }
            StopTimer();

            SceneObjectGroup tmp = m_group;
            m_group = null;

            using (MemoryStream ms = new MemoryStream())
            {
                BinaryFormatter fmt = new BinaryFormatter();
                if (!m_selected && tmp != null)
                    m_serializedPosition = tmp.AbsolutePosition;
                fmt.Serialize(ms, this);
                m_group = tmp;
                if (!timerWasStopped && m_running && !m_waitingCrossing)
                    StartTimer();

                return ms.ToArray();
            }
        }

        public void StartCrossingCheck()
        {
            // timer will be restart by crossingFailure
            // or never since crossing worked and this
            // should be deleted
            StopTimer();

            m_isCrossing = true;
            m_waitingCrossing = true;

            // to remove / retune to smoth crossings
            if (m_group.RootPart.Velocity != Vector3.Zero)
            {
                m_group.RootPart.Velocity = Vector3.Zero;
                m_group.SendGroupRootTerseUpdate();
//                m_group.RootPart.ScheduleTerseUpdate();
            }
        }

        public void CrossingFailure()
        {
            m_waitingCrossing = false;

            if (m_group != null)
            {
                m_group.RootPart.Velocity = Vector3.Zero;
                m_group.SendGroupRootTerseUpdate();
//                m_group.RootPart.ScheduleTerseUpdate();

                if (m_running)
                {
                    StopTimer();
                    m_skipLoops = 1200; // 60 seconds
                    StartTimer();
                }
            }
        }
    }
}
