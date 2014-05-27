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

using OpenSim.Framework;

namespace OpenSim.Region.Framework.Interfaces
{
    public delegate void MessageResultNotification(bool success);
    public delegate void UndeliveredMessage(GridInstantMessage im);
   
    public interface IMessageTransferModule
    {
        event UndeliveredMessage OnUndeliveredMessage;

        /// <summary>
        /// Attempt to send an instant message to a given destination.
        /// </summary>
        /// <remarks>
        /// If the message cannot be delivered for any reason, this will be signalled on the OnUndeliveredMessage
        /// event.  result(false) will also be called if the message cannot be delievered unless the type is
        /// InstantMessageDialog.MessageFromAgent.  For successful message delivery, result(true) is called.
        /// </remarks>
        /// <param name="im"></param>
        /// <param name="result"></param>
        void SendInstantMessage(GridInstantMessage im, MessageResultNotification result);

        /// <summary>
        /// Appropriately handle a known undeliverable message without attempting a send.
        /// </summary>
        /// <remarks>
        /// Essentially, this invokes the OnUndeliveredMessage event.
        /// </remarks>
        /// <param name="im"></param>
        /// <param name="result"></param>
        void HandleUndeliverableMessage(GridInstantMessage im, MessageResultNotification result);
    }
}
