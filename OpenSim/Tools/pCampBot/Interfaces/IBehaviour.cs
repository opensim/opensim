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

namespace pCampBot.Interfaces
{
    public interface IBehaviour
    {
        /// <summary>
        /// Abbreviated name of this behaviour.
        /// </summary>
        string AbbreviatedName { get; }

        /// <summary>
        /// Name of this behaviour.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Initialize the behaviour for this bot.
        /// </summary>
        /// <remarks>
        /// This must be invoked before Action() is called.
        /// </remarks>
        /// <param name="bot"></param>
        void Initialize(Bot bot);

        /// <summary>
        /// Interrupt the behaviour.
        /// </summary>
        /// <remarks>
        /// This should cause the current Action call() to terminate if this is active.
        /// </remarks>
        void Interrupt();

        /// <summary>
        /// Close down this behaviour.
        /// </summary>
        /// <remarks>
        /// This is triggered if a behaviour is removed via explicit command and when a bot is disconnected
        /// </remarks>
        void Close();

        /// <summary>
        /// Action to take when this behaviour is invoked.
        /// </summary>
        /// <param name="bot"></param>
        void Action();
    }
}