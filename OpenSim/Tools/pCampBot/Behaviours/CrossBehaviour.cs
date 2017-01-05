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
using System.Threading;
using log4net;
using OpenMetaverse;
using OpenSim.Framework;
using pCampBot.Interfaces;

namespace pCampBot
{
    /// <summary>
    /// Get the bot to make a region crossing.
    /// </summary>
    public class CrossBehaviour : AbstractBehaviour
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        public AutoResetEvent m_regionCrossedMutex = new AutoResetEvent(false);

        public const int m_regionCrossingTimeout = 1000 * 60;

        public CrossBehaviour()
        {
            AbbreviatedName = "c";
            Name = "Cross";
        }

        public override void Action()
        {
            GridClient client = Bot.Client;

//            // Fly to make the border cross easier.
//            client.Self.Movement.Fly = true;
//            client.Self.Movement.Fly = false;

            // Seek out neighbouring region
            Simulator currentSim = client.Network.CurrentSim;
            ulong currentHandle = currentSim.Handle;
            uint currentX, currentY;
            Utils.LongToUInts(currentHandle, out currentX, out currentY);

            List<GridRegion> candidateRegions = new List<GridRegion>();
            TryAddRegion(Utils.UIntsToLong(Math.Max(0, currentX - Constants.RegionSize), currentY), candidateRegions); // West
            TryAddRegion(Utils.UIntsToLong(currentX + Constants.RegionSize, currentY), candidateRegions);              // East
            TryAddRegion(Utils.UIntsToLong(currentX, Math.Max(0, currentY - Constants.RegionSize)), candidateRegions); // South
            TryAddRegion(Utils.UIntsToLong(currentX, currentY + Constants.RegionSize), candidateRegions);              // North

            if (candidateRegions.Count != 0)
            {
                GridRegion destRegion = candidateRegions[Bot.Manager.Rng.Next(candidateRegions.Count)];

                uint targetX, targetY;
                Utils.LongToUInts(destRegion.RegionHandle, out targetX, out targetY);

                Vector3 pos = client.Self.SimPosition;
                if (targetX < currentX)
                    pos.X = -1;
                else if (targetX > currentX)
                    pos.X = Constants.RegionSize + 1;

                if (targetY < currentY)
                    pos.Y = -1;
                else if (targetY > currentY)
                    pos.Y = Constants.RegionSize + 1;

                m_log.DebugFormat(
                    "[CROSS BEHAVIOUR]: {0} moving to cross from {1} into {2}, target {3}",
                    Bot.Name, currentSim.Name, destRegion.Name, pos);

                // Face in the direction of the candidate region
                client.Self.Movement.TurnToward(pos);

                // Listen for event so that we know when we've crossed the region boundary
                Bot.Client.Self.RegionCrossed += Self_RegionCrossed;

                // Start moving
                Bot.Client.Self.Movement.AtPos = true;

                // Stop when reach region target or border cross detected
                if (!m_regionCrossedMutex.WaitOne(m_regionCrossingTimeout))
                {
                    m_log.ErrorFormat(
                        "[CROSS BEHAVIOUR]: {0} failed to cross from {1} into {2} with {3}ms",
                        Bot.Name, currentSim.Name, destRegion.Name, m_regionCrossingTimeout);
                }
                else
                {
                    m_log.DebugFormat(
                        "[CROSS BEHAVIOUR]: {0} crossed from {1} into {2}",
                        Bot.Name, currentSim.Name, destRegion.Name);
                }

                Bot.Client.Self.RegionCrossed -= Self_RegionCrossed;

                // We will hackishly carry on travelling into the region for a little bit.
                Thread.Sleep(6000);

                m_log.DebugFormat(
                    "[CROSS BEHAVIOUR]: {0} stopped moving after cross from {1} into {2}",
                    Bot.Name, currentSim.Name, destRegion.Name);

                Bot.Client.Self.Movement.AtPos = false;
            }
            else
            {
                m_log.DebugFormat(
                    "[CROSS BEHAVIOUR]: No candidate region for {0} to cross into from {1}.  Ignoring.",
                    Bot.Name, currentSim.Name);
            }
        }

        private bool TryAddRegion(ulong handle, List<GridRegion> regions)
        {
            Dictionary<ulong, GridRegion> knownRegions = Bot.Manager.RegionsKnown;

            lock (knownRegions)
            {
                if (knownRegions.Count == 0)
                    return false;

                m_log.DebugFormat("[CROSS BEHAVIOUR]: Looking for region with handle {0} in known regions", handle);

                if (knownRegions.ContainsKey(handle))
                {
                    GridRegion region = knownRegions[handle];
                    m_log.DebugFormat(
                        "[CROSS BEHAVIOUR]: Adding region {0} to crossing candidates for {1}", region.Name, Bot.Name);

                    regions.Add(region);

                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        internal void Self_RegionCrossed(object o, RegionCrossedEventArgs args)
        {
            m_regionCrossedMutex.Set();
        }
    }
}