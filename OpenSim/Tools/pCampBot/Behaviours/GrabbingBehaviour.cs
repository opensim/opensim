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

using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using pCampBot.Interfaces;

namespace pCampBot
{
    /// <summary>
    /// Click (grab) on random objects in the scene.
    /// </summary>
    /// <remarks>
    /// The viewer itself does not give the option of grabbing objects that haven't been signalled as grabbable.
    /// </remarks>
    public class GrabbingBehaviour : AbstractBehaviour
    {
        public GrabbingBehaviour()
        {
            AbbreviatedName = "g";
            Name = "Grabbing";
        }

        public override void Action()
        {
            Dictionary<UUID, Primitive> objects = Bot.Objects;

            if (objects.Count <= 0)
                return;

            Primitive prim = objects.ElementAt(Bot.Random.Next(0, objects.Count - 1)).Value;

            // This appears to be a typical message sent when a viewer user clicks a clickable object
            Bot.Client.Self.Grab(prim.LocalID);
            Bot.Client.Self.GrabUpdate(prim.ID, Vector3.Zero);
            Bot.Client.Self.DeGrab(prim.LocalID);

            Thread.Sleep(1000);
        }
    }
}