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
using System.Linq;
using System.Reflection;
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Region.PhysicsModules.SharedBase;

namespace OpenSim.Region.Framework.Scenes.Animation
{
    /// <summary>
    /// Handle all animation duties for a scene presence
    /// </summary>
    public class ScenePresenceAnimator
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public AnimationSet Animations
        {
            get { return m_animations; }
        }
        protected AnimationSet m_animations = new AnimationSet();

        /// <value>
        /// The current movement animation
        /// </value>
        public string CurrentMovementAnimation { get; private set; }

        private int m_animTickFall;
        private int m_animTickLand;
        private int m_animTickJump;

        public bool m_jumping = false;

        //        private int m_landing = 0;

        /// <summary>
        /// Is the avatar falling?
        /// </summary>
        public bool Falling { get; private set; }

        private float m_lastFallVelocity;

        /// <value>
        /// The scene presence that this animator applies to
        /// </value>
        protected ScenePresence m_scenePresence;

        public ScenePresenceAnimator(ScenePresence sp)
        {
            m_scenePresence = sp;
            CurrentMovementAnimation = "CROUCH";
        }

        public void AddAnimation(UUID animID, UUID objectID)
        {
            if (m_scenePresence.IsChildAgent)
                return;

            //            m_log.DebugFormat("[SCENE PRESENCE ANIMATOR]: Adding animation {0} for {1}", animID, m_scenePresence.Name);
            if (m_scenePresence.Scene.DebugAnimations)
                m_log.DebugFormat(
                    "[SCENE PRESENCE ANIMATOR]: Adding animation {0} {1} for {2}",
                    GetAnimName(animID), animID, m_scenePresence.Name);

            if (m_animations.Add(animID, m_scenePresence.ControllingClient.NextAnimationSequenceNumber, objectID))
            {
                SendAnimPack();
                m_scenePresence.TriggerScenePresenceUpdated();
            }
        }

        // Called from scripts
        public void AddAnimation(string name, UUID objectID)
        {
            if (m_scenePresence.IsChildAgent)
                return;

            // XXX: For some reason, we store all animations and use them with upper case names, but in LSL animations
            // are referenced with lower case names!
            UUID animID = DefaultAvatarAnimations.GetDefaultAnimation(name.ToUpper());
            if (animID == UUID.Zero)
                return;

            //            m_log.DebugFormat("[SCENE PRESENCE ANIMATOR]: Adding animation {0} {1} for {2}", animID, name, m_scenePresence.Name);

            AddAnimation(animID, objectID);
        }

        /// <summary>
        /// Remove the specified animation
        /// </summary>
        /// <param name='animID'></param>
        /// <param name='allowNoDefault'>
        /// If true, then the default animation can be entirely removed. 
        /// If false, then removing the default animation will reset it to the simulator default (currently STAND).
        /// </param>
        public void RemoveAnimation(UUID animID, bool allowNoDefault)
        {
            if (m_scenePresence.IsChildAgent)
                return;

            if (m_scenePresence.Scene.DebugAnimations)
                m_log.DebugFormat(
                    "[SCENE PRESENCE ANIMATOR]: Removing animation {0} {1} for {2}",
                    GetAnimName(animID), animID, m_scenePresence.Name);

            if (m_animations.Remove(animID, allowNoDefault))
            {
                SendAnimPack();
                m_scenePresence.TriggerScenePresenceUpdated();
            }
        }

        public void avnChangeAnim(UUID animID, bool addRemove, bool sendPack)
        {
            if (m_scenePresence.IsChildAgent)
                return;

            if (animID != UUID.Zero)
            {
                if (addRemove)
                    m_animations.Add(animID, m_scenePresence.ControllingClient.NextAnimationSequenceNumber, UUID.Zero);
                else
                    m_animations.Remove(animID, false);
            }
            if (sendPack)
                SendAnimPack();
        }

        // Called from scripts
        public void RemoveAnimation(string name)
        {
            if (m_scenePresence.IsChildAgent)
                return;

            // XXX: For some reason, we store all animations and use them with upper case names, but in LSL animations
            // are referenced with lower case names!
            UUID animID = DefaultAvatarAnimations.GetDefaultAnimation(name.ToUpper());
            if (animID == UUID.Zero)
                return;

            RemoveAnimation(animID, true);
        }

        public void ResetAnimations()
        {
            if (m_scenePresence.Scene.DebugAnimations)
                m_log.DebugFormat(
                    "[SCENE PRESENCE ANIMATOR]: Resetting animations for {0} in {1}",
                    m_scenePresence.Name, m_scenePresence.Scene.RegionInfo.RegionName);

            m_animations.Clear();
        }


        UUID aoSitGndAnim = UUID.Zero;

        /// <summary>
        /// The movement animation is reserved for "main" animations
        /// that are mutually exclusive, e.g. flying and sitting.
        /// </summary>
        /// <returns>'true' if the animation was updated</returns>
        /// 



        public bool TrySetMovementAnimation(string anim)
        {
            bool ret = false;
            if (!m_scenePresence.IsChildAgent)
            {
//                m_log.DebugFormat(
//                    "[SCENE PRESENCE ANIMATOR]: Setting movement animation {0} for {1}",
//                    anim, m_scenePresence.Name);

                if (aoSitGndAnim != UUID.Zero)
                {
                    avnChangeAnim(aoSitGndAnim, false, true);
                    aoSitGndAnim = UUID.Zero;
                }

                UUID overridenAnim = m_scenePresence.Overrides.GetOverriddenAnimation(anim);
                if (overridenAnim != UUID.Zero)
                {
                    if (anim == "SITGROUND")
                    {
                        UUID defsit = DefaultAvatarAnimations.AnimsUUID["SIT_GROUND_CONSTRAINED"];
                        if (defsit == UUID.Zero)
                            return false;
                        m_animations.SetDefaultAnimation(defsit, m_scenePresence.ControllingClient.NextAnimationSequenceNumber, m_scenePresence.UUID);
                        aoSitGndAnim = overridenAnim;
                        avnChangeAnim(overridenAnim, true, false);
                    }
                    else
                    {
                        m_animations.SetDefaultAnimation(overridenAnim, m_scenePresence.ControllingClient.NextAnimationSequenceNumber, m_scenePresence.UUID);
                    }
                    m_scenePresence.SendScriptEventToAttachments("changed", new Object[] { (int)Changed.ANIMATION });
                    SendAnimPack();
                    ret = true;
                }
                else
                {
                    // translate sit and sitground state animations
                    if (anim == "SIT" || anim == "SITGROUND")
                        anim = m_scenePresence.sitAnimation;

                    if (m_animations.TrySetDefaultAnimation(
                    anim, m_scenePresence.ControllingClient.NextAnimationSequenceNumber, m_scenePresence.UUID))
                    {
//                    m_log.DebugFormat(
//                        "[SCENE PRESENCE ANIMATOR]: Updating movement animation to {0} for {1}",
//                        anim, m_scenePresence.Name);

                        // 16384 is CHANGED_ANIMATION
                        m_scenePresence.SendScriptEventToAttachments("changed", new Object[] { (int)Changed.ANIMATION });
                        SendAnimPack();
                        ret = true;
                    }
                }
            }
            else
            {
                m_log.WarnFormat(
                    "[SCENE PRESENCE ANIMATOR]: Tried to set movement animation {0} on child presence {1}",
                    anim, m_scenePresence.Name);
            }
            return ret;
        }

        public enum motionControlStates : byte
        {
            sitted = 0,
            flying,
            falling,
            jumping,
            landing,
            onsurface
        }

        public motionControlStates currentControlState = motionControlStates.onsurface;

        /// <summary>
        /// This method determines the proper movement related animation
        /// </summary>
        private string DetermineMovementAnimation()
        {
            const int FALL_DELAY = 800;
            const int PREJUMP_DELAY = 200;
            const int JUMP_PERIOD = 800;
            #region Inputs

            if (m_scenePresence.IsInTransit)
                return CurrentMovementAnimation;

            if (m_scenePresence.SitGround)
            {
                currentControlState = motionControlStates.sitted;
                return "SITGROUND";
            }
            if (m_scenePresence.ParentID != 0 || m_scenePresence.ParentUUID != UUID.Zero)
            {
                currentControlState = motionControlStates.sitted;
                return "SIT";
            }

            AgentManager.ControlFlags controlFlags = (AgentManager.ControlFlags)m_scenePresence.AgentControlFlags;
            PhysicsActor actor = m_scenePresence.PhysicsActor;

            const AgentManager.ControlFlags ANYXYMASK = (
                AgentManager.ControlFlags.AGENT_CONTROL_AT_POS | AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_POS |
                AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG | AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_NEG |
                AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS | AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_POS |
                AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG | AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_NEG
                );

            // Check control flags
            /* not in use
                        bool heldForward = ((controlFlags & (AgentManager.ControlFlags.AGENT_CONTROL_AT_POS | AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_POS)) != 0);
                        bool heldBack = ((controlFlags & (AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG | AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_NEG)) != 0);
                        bool heldLeft = ((controlFlags & (AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS | AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_POS)) != 0);
                        bool heldRight = ((controlFlags & (AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG | AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_NEG)) != 0);
            */
            bool heldTurnLeft = (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT) == AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT;
            bool heldTurnRight = (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT) == AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT;
            //            bool heldUp = ((controlFlags & (AgentManager.ControlFlags.AGENT_CONTROL_UP_POS | AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_POS)) != 0);
            // excluded nudge up so it doesn't trigger jump state
            bool heldUp = ((controlFlags & (AgentManager.ControlFlags.AGENT_CONTROL_UP_POS)) != 0);
            bool heldDown = ((controlFlags & (AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG | AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_UP_NEG)) != 0);
            //bool flying = (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_FLY) == AgentManager.ControlFlags.AGENT_CONTROL_FLY;
            //bool mouselook = (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) == AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK;

            bool heldOnXY = ((controlFlags & ANYXYMASK) != 0);
            if (heldOnXY || heldUp || heldDown)
            {
                heldTurnLeft = false;
                heldTurnRight = false;
            }

            #endregion Inputs

            // no physics actor case
            if (actor == null)
            {
                // well what to do?

                currentControlState = motionControlStates.onsurface;
                if (heldOnXY)
                    return "WALK";

                return "STAND";
            }

            #region Flying

            bool isColliding = actor.IsColliding;

            if (actor.Flying)
            {
                m_animTickFall = 0;
                m_animTickJump = 0;
                m_jumping = false;
                Falling = false;

                currentControlState = motionControlStates.flying;

                if (heldOnXY)
                {
                    return (m_scenePresence.Scene.m_useFlySlow ? "FLYSLOW" : "FLY");
                }
                else if (heldUp)
                {
                    return "HOVER_UP";
                }
                else if (heldDown)
                {
                    if (isColliding)
                    {
                        actor.Flying = false;
                        currentControlState = motionControlStates.landing;
                        m_animTickLand = Environment.TickCount;
                        return "LAND";
                    }
                    else
                        return "HOVER_DOWN";
                }
                else
                {
                    return "HOVER";
                }
            }
            else
            {
                if (isColliding && currentControlState == motionControlStates.flying)
                {
                    currentControlState = motionControlStates.landing;
                    m_animTickLand = Environment.TickCount;
                    return "LAND";
                }
            }

            #endregion Flying

            #region Falling/Floating/Landing

            if (!isColliding && currentControlState != motionControlStates.jumping)
            {
                float fallVelocity = actor.Velocity.Z;

                // if stable on Hover assume falling
                if(actor.PIDHoverActive && fallVelocity < 0.05f)
                {
                    Falling = true;
                    currentControlState = motionControlStates.falling;
                    m_lastFallVelocity = fallVelocity;
                    return "FALLDOWN";
                }

                if (fallVelocity < -2.5f)
                    Falling = true;

                if (m_animTickFall == 0 || (fallVelocity >= -0.5f))
                {
                    m_animTickFall = Environment.TickCount;
                }
                else
                {
                    int fallElapsed = (Environment.TickCount - m_animTickFall);
                    if ((fallElapsed > FALL_DELAY) && (fallVelocity < -3.0f))
                    {
                        currentControlState = motionControlStates.falling;
                        m_lastFallVelocity = fallVelocity;
                        // Falling long enough to trigger the animation
                        return "FALLDOWN";
                    }
                }

                // Check if the user has stopped walking just now
                if (CurrentMovementAnimation == "WALK" && !heldOnXY && !heldDown && !heldUp)
                    return "STAND";

                return CurrentMovementAnimation;
            }

            m_animTickFall = 0;

            #endregion Falling/Floating/Landing

            #region Jumping     // section added for jumping...

            if (isColliding && heldUp && currentControlState != motionControlStates.jumping && !actor.PIDHoverActive)
            {
                // Start jumping, prejump
                currentControlState = motionControlStates.jumping;
                m_jumping = true;
                Falling = false;
                m_animTickJump = Environment.TickCount;
                return "PREJUMP";
            }

            if (currentControlState == motionControlStates.jumping)
            {
                int jumptime = Environment.TickCount - m_animTickJump;
                if ((jumptime > (JUMP_PERIOD * 1.5f)) && actor.IsColliding)
                {
                    // end jumping
                    m_jumping = false;
                    Falling = false;
                    actor.Selected = false;      // borrowed for jumping flag
                    m_animTickLand = Environment.TickCount;
                    currentControlState = motionControlStates.landing;
                    return "LAND";
                }
                else if (jumptime > JUMP_PERIOD)
                {
                    // jump down
                    return "JUMP";
                }
                else if (jumptime > PREJUMP_DELAY)
                {
                    // jump up
                    m_jumping = true;
                    return "JUMP";
                }
                return CurrentMovementAnimation;
            }

            #endregion Jumping

            #region Ground Movement

            if (currentControlState == motionControlStates.falling)
            {
                Falling = false;
                currentControlState = motionControlStates.landing;
                m_animTickLand = Environment.TickCount;
                // TODO: SOFT_LAND support
                float fallVsq = m_lastFallVelocity * m_lastFallVelocity;
                if (fallVsq > 300f) // aprox 20*h 
                    return "STANDUP";
                else if (fallVsq > 160f)
                    return "SOFT_LAND";
                else
                    return "LAND";
            }


            if (currentControlState == motionControlStates.landing)
            {
                Falling = false;
                int landElapsed = Environment.TickCount - m_animTickLand;
                int limit = 1000;
                if (CurrentMovementAnimation == "LAND")
                    limit = 350;
                // NB if the above is set too long a weird anim reset from some place prevents STAND from being sent to client

                if ((m_animTickLand != 0) && (landElapsed <= limit))
                {
                    return CurrentMovementAnimation;
                }
                else
                {
                    currentControlState = motionControlStates.onsurface;
                    m_animTickLand = 0;
                    return "STAND";
                }
            }

            // next section moved outside paren. and realigned for jumping

            if (heldOnXY)
            {
                currentControlState = motionControlStates.onsurface;
                Falling = false;
                // Walking / crouchwalking / running
                if (heldDown)
                {
                    return "CROUCHWALK";
                }
                // We need to prevent these animations if the user tries to make their avatar walk or run whilst
                // specifying AGENT_CONTROL_STOP (pressing down space on viewers).
                else if (!m_scenePresence.AgentControlStopActive)
                {
                    if (m_scenePresence.SetAlwaysRun)
                        return "RUN";
                    else
                        return "WALK";
                }
            }
            else
            {
                currentControlState = motionControlStates.onsurface;
                Falling = false;
                // Not walking
                if (heldDown)
                    return "CROUCH";
                else if (heldTurnLeft)
                    return "TURNLEFT";
                else if (heldTurnRight)
                    return "TURNRIGHT";
                else
                    return "STAND";
            }
            #endregion Ground Movement

            return CurrentMovementAnimation;
        }

        /// <summary>
        /// Update the movement animation of this avatar according to its current state
        /// </summary>
        /// <returns>'true' if the animation was changed</returns>
        public bool UpdateMovementAnimations()
        {
            //            m_log.DebugFormat("[SCENE PRESENCE ANIMATOR]: Updating movement animations for {0}", m_scenePresence.Name);

            bool ret = false;
            lock (m_animations)
            {
                string newMovementAnimation = DetermineMovementAnimation();
                if (CurrentMovementAnimation != newMovementAnimation)
                {
                    CurrentMovementAnimation = newMovementAnimation;

//                    m_log.DebugFormat(
//                        "[SCENE PRESENCE ANIMATOR]: Determined animation {0} for {1} in UpdateMovementAnimations()",
//                        CurrentMovementAnimation, m_scenePresence.Name);

                    // Only set it if it's actually changed, give a script
                    // a chance to stop a default animation
                    ret = TrySetMovementAnimation(CurrentMovementAnimation);
                }
            }
            return ret;
        }

        public bool ForceUpdateMovementAnimations()
        {
            lock (m_animations)
            {
                CurrentMovementAnimation = DetermineMovementAnimation();
                return TrySetMovementAnimation(CurrentMovementAnimation);
            }
        }

        public bool SetMovementAnimations(string motionState)
        {
            lock (m_animations)
            {
                CurrentMovementAnimation = motionState;
                return TrySetMovementAnimation(CurrentMovementAnimation);
            }
        }

        public UUID[] GetAnimationArray()
        {
            UUID[] animIDs;
            int[] sequenceNums;
            UUID[] objectIDs;
            m_animations.GetArrays(out animIDs, out sequenceNums, out objectIDs);
            return animIDs;
        }

        public BinBVHAnimation GenerateRandomAnimation()
        {
            int rnditerations = 3;
            BinBVHAnimation anim = new BinBVHAnimation();
            List<string> parts = new List<string>();
            parts.Add("mPelvis"); parts.Add("mHead"); parts.Add("mTorso");
            parts.Add("mHipLeft"); parts.Add("mHipRight"); parts.Add("mHipLeft"); parts.Add("mKneeLeft");
            parts.Add("mKneeRight"); parts.Add("mCollarLeft"); parts.Add("mCollarRight"); parts.Add("mNeck");
            parts.Add("mElbowLeft"); parts.Add("mElbowRight"); parts.Add("mWristLeft"); parts.Add("mWristRight");
            parts.Add("mShoulderLeft"); parts.Add("mShoulderRight"); parts.Add("mAnkleLeft"); parts.Add("mAnkleRight");
            parts.Add("mEyeRight"); parts.Add("mChest"); parts.Add("mToeLeft"); parts.Add("mToeRight");
            parts.Add("mFootLeft"); parts.Add("mFootRight"); parts.Add("mEyeLeft");
            anim.HandPose = 1;
            anim.InPoint = 0;
            anim.OutPoint = (rnditerations * .10f);
            anim.Priority = 7;
            anim.Loop = false;
            anim.Length = (rnditerations * .10f);
            anim.ExpressionName = "afraid";
            anim.EaseInTime = 0;
            anim.EaseOutTime = 0;

            string[] strjoints = parts.ToArray();
            anim.Joints = new binBVHJoint[strjoints.Length];
            for (int j = 0; j < strjoints.Length; j++)
            {
                anim.Joints[j] = new binBVHJoint();
                anim.Joints[j].Name = strjoints[j];
                anim.Joints[j].Priority = 7;
                anim.Joints[j].positionkeys = new binBVHJointKey[rnditerations];
                anim.Joints[j].rotationkeys = new binBVHJointKey[rnditerations];
                Random rnd = new Random();
                for (int i = 0; i < rnditerations; i++)
                {
                    anim.Joints[j].rotationkeys[i] = new binBVHJointKey();
                    anim.Joints[j].rotationkeys[i].time = (i * .10f);
                    anim.Joints[j].rotationkeys[i].key_element.X = ((float)rnd.NextDouble() * 2 - 1);
                    anim.Joints[j].rotationkeys[i].key_element.Y = ((float)rnd.NextDouble() * 2 - 1);
                    anim.Joints[j].rotationkeys[i].key_element.Z = ((float)rnd.NextDouble() * 2 - 1);
                    anim.Joints[j].positionkeys[i] = new binBVHJointKey();
                    anim.Joints[j].positionkeys[i].time = (i * .10f);
                    anim.Joints[j].positionkeys[i].key_element.X = 0;
                    anim.Joints[j].positionkeys[i].key_element.Y = 0;
                    anim.Joints[j].positionkeys[i].key_element.Z = 0;
                }
            }

            AssetBase Animasset = new AssetBase(UUID.Random(), "Random Animation", (sbyte)AssetType.Animation, m_scenePresence.UUID.ToString());
            Animasset.Data = anim.ToBytes();
            Animasset.Temporary = true;
            Animasset.Local = true;
            Animasset.Description = "dance";
            //BinBVHAnimation bbvhanim = new BinBVHAnimation(Animasset.Data);

            m_scenePresence.Scene.AssetService.Store(Animasset);
            AddAnimation(Animasset.FullID, m_scenePresence.UUID);
            return anim;
        }

        /// <summary>
        ///
        /// </summary>
        /// <param name="animations"></param>
        /// <param name="seqs"></param>
        /// <param name="objectIDs"></param>
        public void SendAnimPack(UUID[] animations, int[] seqs, UUID[] objectIDs)
        {
            m_scenePresence.SendAnimPack(animations, seqs, objectIDs);
        }

        public void GetArrays(out UUID[] animIDs, out int[] sequenceNums, out UUID[] objectIDs)
        {
            animIDs = null;
            sequenceNums = null;
            objectIDs = null;

            if (m_animations != null)
                m_animations.GetArrays(out animIDs, out sequenceNums, out objectIDs);
        }

        public void SendAnimPackToClient(IClientAPI client)
        {
            if (m_scenePresence.IsChildAgent)
                return;

            UUID[] animIDs;
            int[] sequenceNums;
            UUID[] objectIDs;

            m_animations.GetArrays(out animIDs, out sequenceNums, out objectIDs);
            client.SendAnimations(animIDs, sequenceNums, m_scenePresence.ControllingClient.AgentId, objectIDs);
        }

        /// <summary>
        /// Send animation information about this avatar to all clients.
        /// </summary>
        public void SendAnimPack()
        {
            //m_log.Debug("Sending animation pack to all");

            if (m_scenePresence.IsChildAgent)
                return;

            UUID[] animIDs;
            int[] sequenceNums;
            UUID[] objectIDs;

            m_animations.GetArrays(out animIDs, out sequenceNums, out objectIDs);

            //            SendAnimPack(animIDs, sequenceNums, objectIDs);
            m_scenePresence.SendAnimPack(animIDs, sequenceNums, objectIDs);
        }

        public string GetAnimName(UUID animId)
        {
            string animName;

            if (!DefaultAvatarAnimations.AnimsNames.TryGetValue(animId, out animName))
            {
                AssetMetadata amd = m_scenePresence.Scene.AssetService.GetMetadata(animId.ToString());
                if (amd != null)
                    animName = amd.Name;
                else
                    animName = "Unknown";
            }

            return animName;
        }
    }
}
