// Proprietary code of Avination Virtual Limited
// (c) 2012 Melanie Thielker
//

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
using OpenSim.Region.Physics.Manager;
using OpenSim.Region.Framework.Scenes.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Runtime.Serialization;
using Timer = System.Timers.Timer;
using log4net;

namespace OpenSim.Region.Framework.Scenes
{
    [Serializable]
    public class KeyframeMotion
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

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
        };

        private Vector3 m_basePosition;
        private Quaternion m_baseRotation;
        private Vector3 m_serializedPosition;

        private Keyframe m_currentFrame;

        private List<Keyframe> m_frames = new List<Keyframe>();

        private Keyframe[] m_keyframes;

        [NonSerialized()]
        protected Timer m_timer = null;

        // timer lock
        [NonSerialized()]
        private object m_onTimerLock;

        // timer overrun detect
        // prevents overlap or timer events threads frozen on the lock
        [NonSerialized()]
        private bool m_inOnTimer;

        // skip timer events.
        //timer.stop doesn't assure there aren't event threads still being fired
        [NonSerialized()]
        private bool m_skipOnTimer;

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

        private const double timerInterval = 50.0;

        public DataFormat Data
        {
            get { return m_data; }
        }

        public bool Selected
        {
            set
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
                        m_serializedPosition = m_group.AbsolutePosition;
                }
                m_selected = value;
            }
        }

        public static KeyframeMotion FromData(SceneObjectGroup grp, Byte[] data)
        {
            MemoryStream ms = new MemoryStream(data);

            BinaryFormatter fmt = new BinaryFormatter();

            KeyframeMotion newMotion = (KeyframeMotion)fmt.Deserialize(ms);

/*
 * create timer in start()
 * this was creating unneeded timers

            // This will be started when position is updated

            newMotion.m_timer = new Timer();
            newMotion.m_timer.Interval = (int)timerInterval;
            newMotion.m_timer.AutoReset = true;
            newMotion.m_timer.Elapsed += newMotion.OnTimer;
*/
            newMotion.m_group = grp;

            if (grp != null && grp.IsSelected)
                newMotion.m_selected = true;

            newMotion.m_onTimerLock = new object();
            newMotion.m_skipOnTimer = false;
            newMotion.m_inOnTimer = false;
            return newMotion;
        }

        public void UpdateSceneObject(SceneObjectGroup grp)
        {
//            lock (m_onTimerLock)
            {
                m_skipOnTimer = true;
                if (m_timer != null)
                    m_timer.Stop();

                m_group = grp;
                Vector3 grppos = grp.AbsolutePosition;
                Vector3 offset = grppos - m_serializedPosition;
                // avoid doing it more than once
                // current this will happen draging a prim to other region
                m_serializedPosition = grppos;

                m_basePosition += offset;
                m_currentFrame.Position += offset;

                m_nextPosition += offset;
/*                
                for (int i = 0; i < m_frames.Count; i++)
                {
                    Keyframe k = m_frames[i];
                    k.Position += offset;
                    m_frames[i] = k;
                }
*/
                for (int i = 0; i < m_frames.Count; i++)
                {
                    Keyframe k = m_frames[i];
                    k.Position += offset;
                    m_frames[i]=k;
                }

                if (m_running)
                    Start();
            }
        }

        public KeyframeMotion(SceneObjectGroup grp, PlayMode mode, DataFormat data)
        {
            m_mode = mode;
            m_data = data;

            m_onTimerLock = new object();

            m_group = grp;
            m_basePosition = grp.AbsolutePosition;
            m_baseRotation = grp.GroupRotation;
/*
            m_timer.Interval = (int)timerInterval;
            m_timer.AutoReset = true;
            m_timer.Elapsed += OnTimer;
 */
        }

        public void SetKeyframes(Keyframe[] frames)
        {
            m_keyframes = frames;
        }

        public void Delete()
        {
            m_skipOnTimer = true;
            m_frames.Clear();
            m_keyframes = null;
            m_running = false;

            if (m_timer == null)
                return;

            m_timer.Stop();
            m_timer.Elapsed -= OnTimer;
            m_timer = null;
        }

        public void Start()
        {
            if (m_keyframes.Length > 0)
            {
                if (m_timer == null)
                {
                    m_timer = new Timer();
                    m_timer.Interval = (int)timerInterval;
                    m_timer.AutoReset = true;
                    m_timer.Elapsed += OnTimer;
                }

                m_skipOnTimer = false;
                m_inOnTimer = false;

                m_timer.Start();
                m_running = true;
            }
            else
            {
                m_running = false;
                m_skipOnTimer = true;
                if (m_timer != null)
                {
                    m_timer.Stop();
                    m_timer.Elapsed -= OnTimer;
                    m_timer = null;
                }
            }
        }

        public void Stop()
        {
            m_skipOnTimer = true;

            // Failed object creation
            if (m_timer == null)
                return;

            m_timer.Stop();
            m_timer.Elapsed -= OnTimer;
            m_timer = null;

            m_basePosition = m_group.AbsolutePosition;
            m_baseRotation = m_group.GroupRotation;

            m_group.RootPart.Velocity = Vector3.Zero;
            m_group.RootPart.UpdateAngularVelocity(Vector3.Zero);
            m_group.SendGroupRootTerseUpdate();

            m_frames.Clear();
            m_running = false;
        }

        public void Pause()
        {
            m_skipOnTimer = true;

            m_group.RootPart.Velocity = Vector3.Zero;
            m_group.RootPart.UpdateAngularVelocity(Vector3.Zero);
            m_group.SendGroupRootTerseUpdate();

            m_timer.Stop();
            m_running = false;
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
//                if (m_mode == PlayMode.PingPong && m_keyframes.Length > 1)
//                    end = m_keyframes.Length - 1;

                if (direction < 0)
                {
                    start = m_keyframes.Length - 1;
                    end = -1;
//                    if (m_mode == PlayMode.PingPong && m_keyframes.Length > 1)
//                        end = 0;
                }

                for (int i = start; i != end ; i += direction)
                {
                    Keyframe k = m_keyframes[i];

                    if (k.Position.HasValue)
                        k.Position = (k.Position * direction) + pos;
                    else
                        k.Position = pos;

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

        protected void OnTimer(object sender, ElapsedEventArgs e)
        {
            if (m_inOnTimer)
            {
                m_log.Error("[KeyFrame]: timer overrun");
                return;
            }

//            lock (m_onTimerLock)
            {
                if (m_skipOnTimer)
                    return;

                m_inOnTimer = true;
                try
                {
                    if (m_frames.Count == 0)
                    {
                        GetNextList();

                        if (m_frames.Count == 0)
                        {
                            Stop();
                            m_inOnTimer = false;
                            return;
                        }

                        m_currentFrame = m_frames[0];
                    }

                    if (m_selected)
                    {
                        if (m_group.RootPart.Velocity != Vector3.Zero)
                        {
                            m_group.RootPart.Velocity = Vector3.Zero;
                            m_group.SendGroupRootTerseUpdate();
                        }
                        m_inOnTimer = false;
                        return;
                    }

                    // Do the frame processing
                    double steps = (double)m_currentFrame.TimeMS / timerInterval;
                    float complete = ((float)m_currentFrame.TimeTotal - (float)m_currentFrame.TimeMS) / (float)m_currentFrame.TimeTotal;

                    if (steps <= 1.0)
                    {
                        m_currentFrame.TimeMS = 0;

                        //                    m_group.AbsolutePosition = (Vector3)m_currentFrame.Position;
                        m_nextPosition = (Vector3)m_currentFrame.Position;
                        m_group.AbsolutePosition = m_nextPosition;

                        m_group.UpdateGroupRotationR((Quaternion)m_currentFrame.Rotation);
                    }
                    else
                    {
                        Vector3 v = (Vector3)m_currentFrame.Position - m_group.AbsolutePosition;
                        Vector3 motionThisFrame = v / (float)steps;
                        v = v * 1000 / m_currentFrame.TimeMS;

                        bool update = false;

                        if (Vector3.Mag(motionThisFrame) >= 0.05f)
                        {
                            //                        m_group.AbsolutePosition += motionThisFrame;
                            m_nextPosition = m_group.AbsolutePosition + motionThisFrame;
                            m_group.AbsolutePosition = m_nextPosition;

                            m_group.RootPart.Velocity = v;
                            update = true;
                        }

                        if ((Quaternion)m_currentFrame.Rotation != m_group.GroupRotation)
                        {
                            Quaternion current = m_group.GroupRotation;

                            Quaternion step = Quaternion.Slerp(m_currentFrame.StartRotation, (Quaternion)m_currentFrame.Rotation, complete);

                            float angle = 0;

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
                            {
                                m_group.UpdateGroupRotationR(step);
                                //m_group.RootPart.UpdateAngularVelocity(m_currentFrame.AngularVelocity / 2);
                                update = true;
                            }
                        }

                        if (update)
                            m_group.SendGroupRootTerseUpdate();
                    }

                    m_currentFrame.TimeMS -= (int)timerInterval;

                    if (m_currentFrame.TimeMS <= 0)
                    {
                        m_group.RootPart.Velocity = Vector3.Zero;
                        m_group.RootPart.UpdateAngularVelocity(Vector3.Zero);
                        m_group.SendGroupRootTerseUpdate();

                        m_frames.RemoveAt(0);
                        if (m_frames.Count > 0)
                            m_currentFrame = m_frames[0];
                    }
                }
                finally
                {
                    m_inOnTimer = false;
                }
            }
        }

        public Byte[] Serialize(bool StopMoveTimer)
        {
            MemoryStream ms = new MemoryStream();
            if (StopMoveTimer && m_timer != null)
            {
                m_skipOnTimer = true;
                m_timer.Stop();
            }

//            lock (m_onTimerLock)
            {
                BinaryFormatter fmt = new BinaryFormatter();
                SceneObjectGroup tmp = m_group;
                m_group = null;
                if(!m_selected)
                    m_serializedPosition = tmp.AbsolutePosition;
                fmt.Serialize(ms, this);
                m_group = tmp;
                return ms.ToArray();
            }
        }

        public void StartCrossingCheck()
        {
            m_skipOnTimer = true;
            if (m_timer != null)
                m_timer.Stop();

            if (m_group.RootPart.Velocity != Vector3.Zero)
            {
                m_group.RootPart.Velocity = Vector3.Zero;
                m_group.SendGroupRootTerseUpdate();
            }
        }

        public void CrossingFailure()
        {
            // The serialization has stopped the timer, so let's wait a moment
            // then retry the crossing. We'll get back here if it fails.
            // if it is a open border there is no serialization
            // so make sure timer is actually stopped

            m_group.RootPart.Velocity = Vector3.Zero;
            m_group.SendGroupRootTerseUpdate();

            Util.FireAndForget(delegate (object x)
            {
                Thread.Sleep(60000);
                if (m_running)
                {
                    m_skipOnTimer = false;
                    m_timer.Start();
                    m_group.AbsolutePosition = m_nextPosition;
                }
            });
        }
    }
}
