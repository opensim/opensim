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
using OpenSim.Region.Physics.Manager;

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
            get { return m_animations;  }
        }
        protected AnimationSet m_animations = new AnimationSet();

        /// <value>
        /// The current movement animation
        /// </value>
        public string CurrentMovementAnimation { get; private set; }
        
        private int m_animTickFall;
        public int m_animTickJump;		// ScenePresence has to see this to control +Z force
        public bool m_jumping = false; 
        public float m_jumpVelocity = 0f;
//        private int m_landing = 0;

        /// <summary>
        /// Is the avatar falling?
        /// </summary>
        public bool Falling { get; private set; }

        private float m_fallHeight;

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
                    m_animations.Remove(animID, true);
            }
            if(sendPack)
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
        
        /// <summary>
        /// The movement animation is reserved for "main" animations
        /// that are mutually exclusive, e.g. flying and sitting.
        /// </summary>
        /// <returns>'true' if the animation was updated</returns>
        public bool TrySetMovementAnimation(string anim)
        {
            bool ret = false;
            if (!m_scenePresence.IsChildAgent)
            {
//                m_log.DebugFormat(
//                    "[SCENE PRESENCE ANIMATOR]: Setting movement animation {0} for {1}",
//                    anim, m_scenePresence.Name);

                if (m_animations.TrySetDefaultAnimation(
                    anim, m_scenePresence.ControllingClient.NextAnimationSequenceNumber, m_scenePresence.UUID))
                {
//                    m_log.DebugFormat(
//                        "[SCENE PRESENCE ANIMATOR]: Updating movement animation to {0} for {1}",
//                        anim, m_scenePresence.Name);

                    // 16384 is CHANGED_ANIMATION
                    m_scenePresence.SendScriptEventToAttachments("changed", new Object[] { (int)Changed.ANIMATION});
                    SendAnimPack();
                    ret = true;
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

        /// <summary>
        /// This method determines the proper movement related animation
        /// </summary>
        private string DetermineMovementAnimation()
        {
            const float FALL_DELAY = 800f;
            const float PREJUMP_DELAY = 200f;
            const float JUMP_PERIOD = 800f;
            #region Inputs

            AgentManager.ControlFlags controlFlags = (AgentManager.ControlFlags)m_scenePresence.AgentControlFlags;
            PhysicsActor actor = m_scenePresence.PhysicsActor;

            // Create forward and left vectors from the current avatar rotation
            Matrix4 rotMatrix = Matrix4.CreateFromQuaternion(m_scenePresence.Rotation);
            Vector3 fwd = Vector3.Transform(Vector3.UnitX, rotMatrix);
            Vector3 left = Vector3.Transform(Vector3.UnitY, rotMatrix);

            // Check control flags
            bool heldForward = ((controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_AT_POS) == AgentManager.ControlFlags.AGENT_CONTROL_AT_POS || (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_POS) == AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_POS);
            bool heldBack = ((controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG || (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_AT_NEG);
            bool heldLeft = ((controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS) == AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS || (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_POS) == AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_POS);
            bool heldRight = ((controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG || (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_NUDGE_LEFT_NEG);
            bool heldTurnLeft = (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT) == AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT;
            bool heldTurnRight = (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT) == AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT;
            bool heldUp = (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_UP_POS) == AgentManager.ControlFlags.AGENT_CONTROL_UP_POS;
            bool heldDown = (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG) == AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG;
            //bool flying = (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_FLY) == AgentManager.ControlFlags.AGENT_CONTROL_FLY;
            //bool mouselook = (controlFlags & AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK) == AgentManager.ControlFlags.AGENT_CONTROL_MOUSELOOK;
            if (heldForward || heldBack || heldLeft || heldRight || heldUp || heldDown)
            {
                heldTurnLeft = false;
                heldTurnRight = false;
            }

            // Direction in which the avatar is trying to move
            Vector3 move = Vector3.Zero;
            if (heldForward) { move.X += fwd.X; move.Y += fwd.Y; }
            if (heldBack) { move.X -= fwd.X; move.Y -= fwd.Y; }
            if (heldLeft) { move.X += left.X; move.Y += left.Y; }
            if (heldRight) { move.X -= left.X; move.Y -= left.Y; }
            if (heldUp) { move.Z += 1; }
            if (heldDown) { move.Z -= 1; }

            // Is the avatar trying to move?
//            bool moving = (move != Vector3.Zero);
            #endregion Inputs

            #region Flying

            if (actor != null && actor.Flying)
            {
                m_animTickFall = 0;
                m_animTickJump = 0;
                m_jumping = false;
                Falling = false;
                m_jumpVelocity = 0f;
                actor.Selected = false;
                m_fallHeight = actor.Position.Z;    // save latest flying height

                if (move.X != 0f || move.Y != 0f)
                {
                    return (m_scenePresence.Scene.m_useFlySlow ? "FLYSLOW" : "FLY");
                }
                else if (move.Z > 0f)
                {
                    return "HOVER_UP";
                }
                else if (move.Z < 0f)
                {
                    if (actor != null && actor.IsColliding)
                        return "LAND";
                    else
                        return "HOVER_DOWN";
                }
                else
                {
                    return "HOVER";
                }
            }

            #endregion Flying

            #region Falling/Floating/Landing

            if ((actor == null || !actor.IsColliding) && !m_jumping)
            {
                float fallElapsed = (float)(Environment.TickCount - m_animTickFall);
                float fallVelocity = (actor != null) ? actor.Velocity.Z : 0.0f;

                if (!m_jumping && (fallVelocity < -3.0f))
                    Falling = true;

                if (m_animTickFall == 0 || (fallVelocity >= 0.0f))
                {
                    // not falling yet, or going up         
                    // reset start of fall time
                    m_animTickFall = Environment.TickCount;
                }
                else if (!m_jumping && (fallElapsed > FALL_DELAY) && (fallVelocity < -3.0f) && (m_scenePresence.WasFlying))
                {
                    // Falling long enough to trigger the animation
                    return "FALLDOWN";
                }

                // Check if the user has stopped walking just now
                if (CurrentMovementAnimation == "WALK" && (move == Vector3.Zero))
                    return "STAND";

                return CurrentMovementAnimation;
            }

            #endregion Falling/Floating/Landing


            #region Jumping     // section added for jumping...

            int jumptime;
            jumptime = Environment.TickCount - m_animTickJump;

            if ((move.Z > 0f) && (!m_jumping))
            {
                // Start jumping, prejump
                m_animTickFall = 0;
                m_jumping = true;
                Falling = false;
                actor.Selected = true;      // borrowed for jumping flag
                m_animTickJump = Environment.TickCount;
                m_jumpVelocity = 0.35f;
                return "PREJUMP";
            }

            if (m_jumping)
            {
                if ((jumptime > (JUMP_PERIOD * 1.5f)) && actor.IsColliding)
                {
                    // end jumping
                    m_jumping = false;
                    Falling = false;
                    actor.Selected = false;      // borrowed for jumping flag
                    m_jumpVelocity = 0f;
                    m_animTickFall = Environment.TickCount;
                    return "LAND";
                }
                else if (jumptime > JUMP_PERIOD)
                {
                    // jump down
                    m_jumpVelocity = 0f;
                    return "JUMP";
                }
                else if (jumptime > PREJUMP_DELAY)
                {
                    // jump up
                    m_jumping = true;
                    m_jumpVelocity = 10f;
                    return "JUMP";
                }
            }

            #endregion Jumping

            #region Ground Movement

            if (CurrentMovementAnimation == "FALLDOWN")
            {
                Falling = false;
                m_animTickFall = Environment.TickCount;
                // TODO: SOFT_LAND support
                float fallHeight = m_fallHeight - actor.Position.Z;
                if (fallHeight > 15.0f)
                    return "STANDUP";
                else if (fallHeight > 8.0f)
                    return "SOFT_LAND";
                else
                    return "LAND";
            }
            else if ((CurrentMovementAnimation == "LAND") || (CurrentMovementAnimation == "SOFT_LAND") || (CurrentMovementAnimation == "STANDUP"))
            {
                int landElapsed = Environment.TickCount - m_animTickFall;
                int limit = 1000;
                if (CurrentMovementAnimation == "LAND")
                    limit = 350;
                // NB if the above is set too long a weird anim reset from some place prevents STAND from being sent to client

                if ((m_animTickFall != 0) && (landElapsed <= limit))
                {
                    return CurrentMovementAnimation;
                }
                else
                {
                    m_fallHeight = actor.Position.Z;    // save latest flying height
                    return "STAND";
                }
            }

            // next section moved outside paren. and realigned for jumping
            if (move.X != 0f || move.Y != 0f)
            {
                m_fallHeight = actor.Position.Z;    // save latest flying height
                Falling = false;
                // Walking / crouchwalking / running
                if (move.Z < 0f)
                    return "CROUCHWALK";
                else if (m_scenePresence.SetAlwaysRun)
                    return "RUN";
                else
                    return "WALK";
            }
            else if (!m_jumping)
            {
                Falling = false;
                // Not walking
                if (move.Z < 0)
                    return "CROUCH";
                else if (heldTurnLeft)
                    return "TURNLEFT";
                else if (heldTurnRight)
                    return "TURNRIGHT";
                else
                    return "STAND";
            }
            #endregion Ground Movement

            Falling = false;

            return CurrentMovementAnimation;
        }

        /// <summary>
        /// Update the movement animation of this avatar according to its current state
        /// </summary>
        /// <returns>'true' if the animation was changed</returns>
        public bool UpdateMovementAnimations()
        {
            bool ret = false;
            lock (m_animations)
            {
                string newMovementAnimation = DetermineMovementAnimation();
                if (CurrentMovementAnimation != newMovementAnimation)
                {
                    CurrentMovementAnimation = DetermineMovementAnimation();

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
            parts.Add("mPelvis");parts.Add("mHead");parts.Add("mTorso");
            parts.Add("mHipLeft");parts.Add("mHipRight");parts.Add("mHipLeft");parts.Add("mKneeLeft");
            parts.Add("mKneeRight");parts.Add("mCollarLeft");parts.Add("mCollarRight");parts.Add("mNeck");
            parts.Add("mElbowLeft");parts.Add("mElbowRight");parts.Add("mWristLeft");parts.Add("mWristRight");
            parts.Add("mShoulderLeft");parts.Add("mShoulderRight");parts.Add("mAnkleLeft");parts.Add("mAnkleRight");
            parts.Add("mEyeRight");parts.Add("mChest");parts.Add("mToeLeft");parts.Add("mToeRight");
            parts.Add("mFootLeft");parts.Add("mFootRight");parts.Add("mEyeLeft");
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
                    anim.Joints[j].rotationkeys[i].time = (i*.10f);
                    anim.Joints[j].rotationkeys[i].key_element.X = ((float) rnd.NextDouble()*2 - 1);
                    anim.Joints[j].rotationkeys[i].key_element.Y = ((float) rnd.NextDouble()*2 - 1);
                    anim.Joints[j].rotationkeys[i].key_element.Z = ((float) rnd.NextDouble()*2 - 1);
                    anim.Joints[j].positionkeys[i] = new binBVHJointKey();
                    anim.Joints[j].positionkeys[i].time = (i*.10f);
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
            if (m_scenePresence.IsChildAgent)
                return;

//            m_log.DebugFormat(
//                "[SCENE PRESENCE ANIMATOR]: Sending anim pack with animations '{0}', sequence '{1}', uuids '{2}'", 
//                string.Join(",", Array.ConvertAll<UUID, string>(animations, a => a.ToString())), 
//                string.Join(",", Array.ConvertAll<int, string>(seqs, s => s.ToString())),
//                string.Join(",", Array.ConvertAll<UUID, string>(objectIDs, o => o.ToString())));

            m_scenePresence.Scene.ForEachClient(
                delegate(IClientAPI client) 
                { 
                    client.SendAnimations(animations, seqs, m_scenePresence.ControllingClient.AgentId, objectIDs); 
                });
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

            SendAnimPack(animIDs, sequenceNums, objectIDs);
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
