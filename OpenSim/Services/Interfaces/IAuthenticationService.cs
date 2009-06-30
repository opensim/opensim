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

namespace OpenSim.Services.Interfaces
{
    // Generic Authentication service used for identifying
    // and authenticating principals.
    // Principals may be clients acting on users' behalf,
    // or any other components that need 
    // verifiable identification.
    //
    public interface IAuthenticationService
    {
        //////////////////////////////////////////////////
        // Web login key portion
        //

        // Get a service key given that principal's 
        // authentication token (master key). 
        //
        string GetKey(UUID principalID, string authToken);

        // Verify that a principal key is valid
        //
        bool VerifyKey(UUID principalID, string key);

        //////////////////////////////////////////////////
        // Password auth portion
        //

        // Here's how thos works, and why.
        //
        // The authentication methods will return the existing session,
        // or UUID.Zero if authentication failed. If there is no session,
        // they will create one.
        // The CreateUserSession method will unconditionally create a session
        // and invalidate the prior session.
        // Grid login uses this method to make sure that the session is
        // fresh and new. Other software, like management applications,
        // can obtain this existing session if they have a key or password
        // for that account, this allows external apps to obtain credentials
        // and use authenticating interface methods.
        //
        
        // Check the pricipal's password
        //
        UUID AuthenticatePassword(UUID principalID, string password);

        // Check the principal's key
        //
        UUID AuthenticateKey(UUID principalID, string password);

        // Create a new session, invalidating the old ones
        //
        UUID CreateUserSession(UUID principalID, UUID oldSessionID);

        // Verify that a user session ID is valid. A session ID is
        // considered valid when a user has successfully authenticated
        // at least one time inside that session.
        //
        bool VerifyUserSession(UUID principalID, UUID sessionID);

        // Deauthenticate user
        //
        bool DestroyUserSession(UUID principalID, UUID sessionID);
    }
}
