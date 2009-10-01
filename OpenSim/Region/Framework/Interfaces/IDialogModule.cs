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
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IDialogModule
    {
        /// <summary>
        /// Send a non-modal alert message to a particular user.  This can disappear from the user's view after a
        /// small interval.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        void SendAlertToUser(IClientAPI client, string message);
        
        /// <summary>
        /// Send an alert message to a particular user.
        /// </summary>
        /// <param name="client"></param>
        /// <param name="message"></param>
        /// <param name="modal"></param>
        void SendAlertToUser(IClientAPI client, string message, bool modal);
        
        /// <summary>
        /// Send a non-modal alert message to a particular user.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="message"></param>
        void SendAlertToUser(UUID agentID, string message);
        
        /// <summary>
        /// Send an alert message to a particular user.
        /// </summary>
        /// <param name="agentID"></param>
        /// <param name="message"></param>
        /// <param name="modal"></param>
        void SendAlertToUser(UUID agentID, string message, bool modal);
        
        /// <summary>
        /// Send an alert message to a particular user.
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="message"></param>
        /// <param name="modal"></param>
        void SendAlertToUser(string firstName, string lastName, string message, bool modal);
        
        /// <summary>
        /// Send an alert message to all users in the scene.
        /// </summary>
        /// <param name="message"></param>
        void SendGeneralAlert(string message);
        
        /// <summary>
        /// Send a dialog box to a particular user.
        /// </summary>
        /// <param name="avatarID"></param>
        /// <param name="objectName"></param>
        /// <param name="objectID"></param>
        /// <param name="ownerID"></param>
        /// <param name="message"></param>
        /// <param name="textureID"></param>
        /// <param name="ch"></param>
        /// <param name="buttonlabels"></param>
        void SendDialogToUser(
            UUID avatarID, string objectName, UUID objectID, UUID ownerID, 
            string message, UUID textureID, int ch, string[] buttonlabels);
        
        /// <summary>
        /// Send a url to a particular user.
        /// </summary>
        /// <param name="avatarID"></param>
        /// <param name="objectName"></param>
        /// <param name="objectID"></param>
        /// <param name="ownerID"></param>
        /// <param name="groupOwned"></param>
        /// <param name="message"></param>
        /// <param name="url"></param>
        void SendUrlToUser(
            UUID avatarID, string objectName, UUID objectID, UUID ownerID, bool groupOwned, string message, string url);
        
        /// <summary>
        /// Send a notification to all users in the scene.  This notification should remain around until the 
        /// user explicitly dismisses it.
        /// </summary>
        /// 
        /// On the Linden Labs Second Client (as of 1.21), this is a big blue box message on the upper right of the 
        /// screen.
        /// 
        /// <param name="fromAvatarID">The user sending the message</param>
        /// <param name="fromAvatarName">The name of the user doing the sending</param>
        /// <param name="message">The message being sent to the user</param>
        void SendNotificationToUsersInRegion(UUID fromAvatarID, string fromAvatarName, string message);
        
        /// <summary>
        /// Send a notification to all users in the estate.  This notification should remain around until the 
        /// user explicitly dismisses it.
        /// </summary>
        /// 
        /// On the Linden Labs Second Client (as of 1.21), this is a big blue box message on the upper right of the 
        /// screen.
        /// 
        /// <param name="fromAvatarID">The user sending the message</param>
        /// <param name="fromAvatarName">The name of the user doing the sending</param>
        /// <param name="message">The message being sent to the user</param>
        void SendNotificationToUsersInEstate(UUID fromAvatarID, string fromAvatarName, string message);
    }
}
