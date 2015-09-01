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
        /// Send a non-modal alert message to a particular user. This can
        /// disappear from the user's view after a small interval.
        /// </summary>
        /// <param name="client">
        /// IClientAPI object representing the user.
        /// </param>
        /// <param name="message">Message text to send to the user.</param>
        void SendAlertToUser(IClientAPI client, string message);

        /// <summary>
        /// Send an alert message to a particular user.
        /// </summary>
        /// <param name="client">
        /// IClientAPI object representing the user.
        /// </param>
        /// <param name="message">Message text to send to the user.</param>
        /// <param name="modal">Flag to control modality.</param>
        void SendAlertToUser(IClientAPI client, string message, bool modal);

        /// <summary>
        /// Send a non-modal alert message to a particular user.
        /// </summary>
        /// <param name="agentID">UUID of agent representing the user.</param>
        /// <param name="message">Message text to send to the user.</param>
        void SendAlertToUser(UUID agentID, string message);

        /// <summary>
        /// Send an alert message to a particular user.
        /// </summary>
        /// <param name="agentID">UUID of agent representing the user.</param>
        /// <param name="message">Message text to send to the user.</param>
        /// <param name="modal">Flag to control modality.</param>
        void SendAlertToUser(UUID agentID, string message, bool modal);

        /// <summary>
        /// Send an alert message to a particular user.
        /// </summary>
        /// <param name="firstName">Account first name</param>
        /// <param name="lastName">Account last name</param>
        /// <param name="message">Message text to send to the user.</param>
        /// <param name="modal">Flag to control modality.</param>
        void SendAlertToUser(string firstName, string lastName,
                string message, bool modal);

        /// <summary>
        /// Send an alert message to all users in the scene.
        /// </summary>
        /// <param name="message">Message text to send to all users.</param>
        void SendGeneralAlert(string message);

        /// <summary>
        /// Send a dialog box to a particular user.
        /// </summary>
        /// <param name="avatarID">
        /// UUID of the avatar representing the user.
        /// </param>
        /// <param name="objectName">
        /// Name of the object sending the dialog.
        /// </param>
        /// <param name="objectID">
        /// UUID of the object sending the dialog.
        /// </param>
        /// <param name="ownerID">
        /// UUID of the user that owns the object.
        /// </param>
        /// <param name="message">Message text to send to the user.</param>
        /// <param name="textureID">
        /// Texture UUID to pass along with the dialog.
        /// </param>
        /// <param name="ch">
        /// Channel on which the selected button text should be broadcast.
        /// </param>
        /// <param name="buttonlabels">Dialog button text.</param>
        void SendDialogToUser(UUID avatarID, string objectName, UUID objectID,
                UUID ownerID, string message, UUID textureID, int ch,
                string[] buttonlabels);

        /// <summary>
        /// Send a url to a particular user.
        /// </summary>
        /// <param name="avatarID">
        /// UUID of the avatar representing the user.
        /// </param>
        /// <param name="objectName">
        /// Name of the object sending the dialog.
        /// </param>
        /// <param name="objectID">
        /// UUID of the object sending the dialog.
        /// </param>
        /// <param name="ownerID">
        /// UUID of the user that owns the object.
        /// </param>
        /// <param name="groupOwned">
        /// Flag indicating whether or not the object is group-owned.
        /// </param>
        /// <param name="message">Message text to send to the user.</param>
        /// <param name="url">Url to send to the user.</param>
        void SendUrlToUser(UUID avatarID, string objectName, UUID objectID,
                UUID ownerID, bool groupOwned, string message, string url);

        /// <summary>
        /// Send a notification to all users in the scene. This notification
        /// should remain around until the user explicitly dismisses it.
        /// </summary>
        /// <remarks>
        /// On the Linden Labs Second Client (as of 1.21), this is a big blue
        /// box message on the upper right of the screen.
        /// </remarks>
        /// <param name="fromAvatarID">The user sending the message</param>
        /// <param name="fromAvatarName">
        /// The name of the user doing the sending
        /// </param>
        /// <param name="message">The message being sent to the user</param>
        void SendNotificationToUsersInRegion(UUID fromAvatarID,
                string fromAvatarName, string message);

        /// <summary>
        /// Send a textbox entry for the client to respond to
        /// </summary>
        /// <param name="avatarID">
        /// UUID of the avatar representing the user.
        /// </param>
        /// <param name="message">Message text to send to the user.</param>
        /// <param name="chatChannel">
        /// Chat channel that the user's input should be broadcast on.
        /// </param>
        /// <param name="name">Name of the object sending the dialog.</param>
        /// <param name="objectid">
        /// UUID of the object sending the dialog.
        /// </param>
        /// <param name="ownerid">
        /// UUID of the user that owns the object.
        /// </param>
        void SendTextBoxToUser(UUID avatarid, string message, int chatChannel,
                string name, UUID objectid, UUID ownerid);
    }
}
