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
using OpenMetaverse;
using OpenSim.Framework;

namespace OpenSim.Region.Framework.Interfaces
{
    /// <summary>
    /// Provide mechanisms for messaging groups.
    /// </summary>
    ///
    /// TODO: Provide a mechanism for receiving group messages as well as sending them
    ///
    public interface IGroupsMessagingModule
    {
        /// <summary>
        /// Start a group chat session.
        /// </summary>
        /// You must call this before calling SendMessageToGroup().  If a chat session for this group is already taking
        /// place then the agent will added to that session.
        /// <param name="agentID">
        /// A UUID that represents the agent being added.  If you are agentless (e.g. you are
        /// a region module), then you can use any random ID.
        /// </param>
        /// <param name="groupID">
        /// The ID for the group to join.  Currently, the session ID used is identical to the
        /// group ID.
        /// </param>
        /// <returns>
        /// True if the chat session was started successfully, false otherwise.
        /// </returns>
        bool StartGroupChatSession(UUID agentID, UUID groupID);

        /// <summary>
        /// Send a message to each member of a group whose chat session is active.
        /// </summary>
        /// <param name="im">
        /// The message itself.  The fields that must be populated are
        ///
        /// imSessionID - Populate this with the group ID (session ID and group ID are currently identical)
        /// fromAgentName - Populate this with whatever arbitrary name you want to show up in the chat dialog
        /// message - The message itself
        /// dialog - This must be (byte)InstantMessageDialog.SessionSend
        /// </param>
        /// <param name="groupID"></param>
        void SendMessageToGroup(GridInstantMessage im, UUID groupID);

        /// <summary>
        /// Send a message to all the members of a group that fulfill a condition.
        /// </summary>
        /// <param name="im">
        /// The message itself.  The fields that must be populated are
        ///
        /// imSessionID - Populate this with the group ID (session ID and group ID are currently identical)
        /// fromAgentName - Populate this with whatever arbitrary name you want to show up in the chat dialog
        /// message - The message itself
        /// dialog - This must be (byte)InstantMessageDialog.SessionSend
        /// </param>
        /// <param name="groupID"></param>
        /// <param name="sendingAgentForGroupCalls">
        /// The requesting agent to use when querying the groups service.  Sometimes this is different from
        /// im.fromAgentID, with group notices, for example.
        /// </param>
        /// <param name="sendCondition">
        /// The condition that must be met by a member for the message to be sent.  If null then the message is sent
        /// if the chat session is active.
        /// </param>
        void SendMessageToGroup(
            GridInstantMessage im, UUID groupID, UUID sendingAgentForGroupCalls, Func<GroupMembersData, bool> sendCondition);
    }
}