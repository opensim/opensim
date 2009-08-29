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
        //////////////////////////////////////////////////////
        // PKI Zone!
        //
        // HG2 authentication works by using a cryptographic
        // exchange.
        // This method must provide a public key, the other
        // crypto methods must understand hoow to deal with
        // messages encrypted to it.
        //
        // If the public key is of zero length, you will
        // get NO encryption and NO security.
        //
        // For non-HG installations, this is not relevant
        //
        // Implementors who are not using PKI can treat the
        // cyphertext as a string and provide a zero-length
        // key. Encryptionless implementations will not
        // interoperate with implementations using encryption.
        // If one side uses encryption, both must do so.
        //
        byte[] GetPublicKey();

        //////////////////////////////////////////////////////
        // Authentication
        //
        // These methods will return a token, which can be used to access
        // various services.
        //
        // The encrypted versions take the received cyphertext and
        // the public key of the peer, which the connector must have
        // obtained using a remote GetPublicKey call.
        //
        string AuthenticatePassword(UUID principalID, string password);
        byte[] AuthenticatePasswordEncrypted(byte[] cyphertext, byte[] key);

        string AuthenticateWebkey(UUID principalID, string webkey);
        byte[] AuthenticateWebkeyEncrypted(byte[] cyphertext, byte[] key);

        //////////////////////////////////////////////////////
        // Verification
        //
        // Allows to verify the authenticity of a token
        //
        // Tokens expire after 30 minutes and can be refreshed by
        // re-verifying.
        //
        // If encrypted authentication was used, encrypted verification
        // must be used to refresh. Unencrypted verification is still
        // performed, but doesn't refresh token lifetime.
        //
        bool Verify(UUID principalID, string token);
        bool VerifyEncrypted(byte[] cyphertext, byte[] key);

        //////////////////////////////////////////////////////
        // Teardown
        //
        // A token can be returned before the timeout. This
        // invalidates it and it can not subsequently be used
        // or refreshed.
        //
        // Tokens created by encrypted authentication must
        // be returned by encrypted release calls;
        //
        bool Release(UUID principalID, string token);
        bool ReleaseEncrypted(byte[] cyphertext, byte[] key);

        //////////////////////////////////////////////////////
        // Grid
        //
        // We no longer need a shared secret between grid
        // servers. Anything a server requests from another
        // server is either done on behalf of a user, in which
        // case there is a token, or on behalf of a region,
        // which has a session. So, no more keys.
        // If sniffing on the local lan is an issue, admins
        // need to take approriate action (IPSec is recommended)
        // to secure inter-server traffic.

        //////////////////////////////////////////////////////
        // NOTE
        //
        // Session IDs are not handled here. After obtaining
        // a token, the session ID regions use can be
        // obtained from the presence service.
    }
}
