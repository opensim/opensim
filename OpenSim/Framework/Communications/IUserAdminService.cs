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

namespace OpenSim.Framework.Communications
{
    /// <summary>
    /// Interface for the service for administrating users
    /// </summary>
    public interface IUserAdminService
    {
        /// <summary>
        /// Add a new user
        /// </summary>
        /// <param name="firstName">The first name</param>
        /// <param name="lastName">The last name</param>
        /// <param name="pass">password of avatar</param>
        /// <param name="email">email of user</param>
        /// <param name="regX">region X</param>
        /// <param name="regY">region Y</param>
        /// <returns>The UUID of the created user profile.  On failure, returns UUID.Zero</returns>
        UUID AddUser(string firstName, string lastName, string pass, string email, uint regX, uint regY);

        /// <summary>
        /// Add a new user with a specified UUID.   SHOULD ONLY BE USED in very special circumstances from modules!
        /// </summary>
        /// <param name="firstName">The first name</param>
        /// <param name="lastName">The last name</param>
        /// <param name="pass">password of avatar</param>
        /// <param name="email">email of user</param>
        /// <param name="regX">region X</param>
        /// <param name="regY">region Y</param>
        /// <param name="setUUID">The set UUID</param>
        /// <returns>The UUID of the created user profile.  On failure, returns UUID.Zero</returns>
        UUID AddUser(string firstName, string lastName, string pass, string email, uint regX, uint regY, UUID setUUID);

        /// <summary>
        /// Reset a user password
        /// </summary>
        /// <param name="firstName"></param>
        /// <param name="lastName"></param>
        /// <param name="newPassword"></param>
        /// <returns>true if the update was successful, false otherwise</returns>
        bool ResetUserPassword(string firstName, string lastName, string newPassword);
    }
}
