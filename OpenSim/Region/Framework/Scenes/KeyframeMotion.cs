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
    public class KeyframeTimer
    {
        private static Dictionary<Scene, KeyframeTimer>m_timers =
                new Dictionary<Scene, KeyframeTimer>();

        private Dictionary<KeyframeMotion, object> m_motions = new Dictionary<KeyframeMotion, object>();
        private object m_lockObject = new object();
        private const double m_tickDuration = 50.0;
        private Scene m_scene;
        private int m_prevTick;

        public double TickDuration
        {
            get { return m_tickDuration; }
        }

        public KeyframeTimer(Scene scene)
        {
            m_prevTick = Util.EnvironmentTickCount();

            m_scene = scene;

            m_scene.EventManager.OnFrame += OnTimer;
        }

        private void OnTimer()
        {
            int thisTick = Util.EnvironmentTickCount();
            int tickdiff = Util.EnvironmentTickCountSubtract(thisTick, m_prevTick);
            m_prevTick = thisTick;

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
                        m.OnTimer(tickdiff);
                    }
                    catch (Exception inner)
                    {
                        // Don't stop processing
                    }
                }
            }
            catch (Exception e)
            {
                // Keep running no matter what
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
            KeyframeTimer.Add(this);
            m_timerStopped = false;
        }

        private void StopTimer()
        {
            m_timerStopped = true;
            KeyframeTimer.Remove(this);
        }

        public static KeyframeMotion FromData(SceneObjectGroup grp, Byte[] data)
        {
            KeyframeMotion newMotion = null;

            try
            {
                MemoryStream ms = new MemoryStream(data);
                BinaryFormatter fmt = new BinaryFormatter();

                newMotion = (KeyframeMotion)fmt.Deserialize(ms);

                newMotion.m_group = grp;

                if (grp != null)
                {
                    newMotion.m_scene = grp.Scene;
                    if (grp.IsSelected)
                        newMotion.m_selected = true;
                }

                newMotion.m_timerStopped = false;
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
                m_frames[i]=k;
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
            }
            else
            {
                m_running = false;
                StopTimer();
            }
        }

        public void Stop()
        {
            m_running = false;
            m_isCrossing = false;
            m_waitingCrossing = false;

            StopTimer();

            m_basePosition = m_group.AbsolutePosition;
            m_baseRotation = m_group.GroupRotation;

            m_group.RootPart.Velocity = Vector3.Zero;
            m_group.RootPart.AngularVelocity = Vector3.Zero;
            m_group.SendGroupRootTerseUpdate(PrimUpdateFlags.Immediate);
//            m_group.RootPart.ScheduleTerseUpdate();
            m_frames.Clear();
        }

        public void Pause()
        {
            m_running = false;
            StopTimer();

            m_group.RootPart.Velocity = Vector3.Zero;
            m_group.RootPart.AngularVelocity = Vector3.Zero;
            m_group.SendGroupRootTerseUpdate(PrimUpdateFlags.Immediate);
//            m_group.RootPart.ScheduleTerseUpdate();

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
            if (m_skipLoops > 0)
            {
                m_skipLoops--;
                return;
            }

            if (m_timerStopped) // trap events still in air even after a timer.stop
                return;

            if (m_group == null)
                return;

            bool update = false;

            if (m_selected)
            {
                if (m_group.RootPart.Velocity != Vector3.Zero)
                {
                    m_group.RootPart.Velocity = Vector3.Zero;
                    m_group.SendGroupRootTerseUpdate(PrimUpdateFlags.Immediate);

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
                GetNextList();

                if (m_frames.Count == 0)
                {
                    Stop();
                    Scene scene = m_group.Scene;

                    IScriptModule[] scriptModules = scene.RequestModuleInterfaces<IScriptModule>();
                    foreach (IScriptModule m in scriptModules)
                    {
                        if (m == null)
                            continue;
                        m.PostObjectEvent(m_group.RootPart.UUID, "moving_end", new object[0]);
                    }

                    return;
                }

                m_currentFrame = m_frames[0];
                m_currentFrame.TimeMS += (int)tickDuration;

                //force a update on a keyframe transition
                update = true;
            }

            m_currentFrame.TimeMS -= (int)tickDuration;

            // Do the frame processing
            double steps = (double)m_currentFrame.TimeMS / tickDuration;

            if (steps <= 0.0)
            {
                m_group.RootPart.Velocity = Vector3.Zero;
                m_group.RootPart.AngularVelocity = Vector3.Zero;

                m_nextPosition = (Vector3)m_currentFrame.Position;
                m_group.AbsolutePosition = m_nextPosition;

                // we are sending imediate updates, no doing force a extra terseUpdate
                // m_group.UpdateGroupRotationR((Quaternion)m_currentFrame.Rotation);

                m_group.RootPart.RotationOffset = (Quaternion)m_currentFrame.Rotation;
                m_frames.RemoveAt(0);
                if (m_frames.Count > 0)
                    m_currentFrame = m_frames[0];

                update = true;
            }
            else
            {
                float complete = ((float)m_currentFrame.TimeTotal - (float)m_currentFrame.TimeMS) / (float)m_currentFrame.TimeTotal;

                Vector3 v = (Vector3)m_currentFrame.Position - m_group.AbsolutePosition;
                Vector3 motionThisFrame = v / (float)steps;
                v = v * 1000 / m_currentFrame.TimeMS;

                if (Vector3.Mag(motionThisFrame) >= 0.05f)
                {
                    // m_group.AbsolutePosition += motionThisFrame;
                    m_nextPosition = m_group.AbsolutePosition + motionThisFrame;
                    m_group.AbsolutePosition = m_nextPosition;

                    m_group.RootPart.Velocity = v;
                    update = true;
                }

                if ((Quaternion)m_currentFrame.Rotation != m_group.GroupRotation)
                {
                    Quaternion current = m_group.GroupRotation;

                    Quaternion step = Quaternion.Slerp(m_currentFrame.StartRotation, (Quaternion)m_currentFrame.Rotation, complete);
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
                    if(Math.Abs(step.X - current.X) > 0.001f 
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
                m_group.SendGroupRootTerseUpdate(PrimUpdateFlags.Immediate);
            }
        }

        public Byte[] Serialize()
        {
            StopTimer();
            MemoryStream ms = new MemoryStream();

            BinaryFormatter fmt = new BinaryFormatter();
            SceneObjectGroup tmp = m_group;
            m_group = null;
            if (!m_selected && tmp != null)
                m_serializedPosition = tmp.AbsolutePosition;
            fmt.Serialize(ms, this);
            m_group = tmp;
            if (m_running && !m_waitingCrossing)
                StartTimer();

            return ms.ToArray();
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
                m_group.SendGroupRootTerseUpdate(PrimUpdateFlags.Immediate);
//                m_group.RootPart.ScheduleTerseUpdate();
            }
        }

        public void CrossingFailure()
        {
            m_waitingCrossing = false;

            if (m_group != null)
            {
                m_group.RootPart.Velocity = Vector3.Zero;
                m_group.SendGroupRootTerseUpdate(PrimUpdateFlags.Immediate);
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
