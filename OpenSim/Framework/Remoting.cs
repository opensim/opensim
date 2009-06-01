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
using System.Security.Cryptography;
using System.Text;

namespace OpenSim.Framework
{
    /// <summary>
    /// NEEDS AUDIT.
    /// </summary>
    /// <remarks>
    /// Suggested implementation
    /// <para>Store two digests for each foreign host. A local copy of the local hash using the local challenge (when issued), and a local copy of the remote hash using the remote challenge.</para>
    /// <para>When sending data to the foreign host - run 'Sign' on the data and affix the returned byte[] to the message.</para>
    /// <para>When receiving data from the foreign host - run 'Authenticate' against the data and the attached byte[].</para>
    /// <para>Both hosts should be performing these operations for this to be effective.</para>
    /// </remarks>
    internal class RemoteDigest
    {
        private byte[] currentHash;
        private byte[] secret;

        private SHA512Managed SHA512;

        /// <summary>
        /// Initialises a new RemoteDigest authentication mechanism
        /// </summary>
        /// <remarks>Needs an audit by a cryptographic professional - was not "roll your own"'d by choice but rather a serious lack of decent authentication mechanisms in .NET remoting</remarks>
        /// <param name="sharedSecret">The shared secret between systems (for inter-sim, this is provided in encrypted form during connection, for grid this is input manually in setup)</param>
        /// <param name="salt">Binary salt - some common value - to be decided what</param>
        /// <param name="challenge">The challenge key provided by the third party</param>
        public RemoteDigest(string sharedSecret, byte[] salt, string challenge)
        {
            SHA512 = new SHA512Managed();
            Rfc2898DeriveBytes RFC2898 = new Rfc2898DeriveBytes(sharedSecret, salt);
            secret = RFC2898.GetBytes(512);
            ASCIIEncoding ASCII = new ASCIIEncoding();

            currentHash = SHA512.ComputeHash(AppendArrays(secret, ASCII.GetBytes(challenge)));
        }

        /// <summary>
        /// Authenticates a piece of incoming data against the local digest. Upon successful authentication, digest string is incremented.
        /// </summary>
        /// <param name="data">The incoming data</param>
        /// <param name="digest">The remote digest</param>
        /// <returns></returns>
        public bool Authenticate(byte[] data, byte[] digest)
        {
            byte[] newHash = SHA512.ComputeHash(AppendArrays(AppendArrays(currentHash, secret), data));
            if (digest == newHash)
            {
                currentHash = newHash;
                return true;
            }
            else
            {
                throw new Exception("Hash comparison failed. Key resync required.");
            }
        }

        /// <summary>
        /// Signs a new bit of data with the current hash. Returns a byte array which should be affixed to the message.
        /// Signing a piece of data will automatically increment the hash - if you sign data and do not send it, the
        /// hashes will get out of sync and throw an exception when validation is attempted.
        /// </summary>
        /// <param name="data">The outgoing data</param>
        /// <returns>The local digest</returns>
        public byte[] Sign(byte[] data)
        {
            currentHash = SHA512.ComputeHash(AppendArrays(AppendArrays(currentHash, secret), data));
            return currentHash;
        }

        /// <summary>
        /// Generates a new challenge string to be issued to a foreign host. Challenges are 1024-bit (effective strength of less than 512-bits) messages generated using the Crytographic Random Number Generator.
        /// </summary>
        /// <returns>A 128-character hexadecimal string containing the challenge.</returns>
        public static string GenerateChallenge()
        {
            RNGCryptoServiceProvider RNG = new RNGCryptoServiceProvider();
            byte[] bytes = new byte[64];
            RNG.GetBytes(bytes);

            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            foreach (byte b in bytes)
            {
                sb.AppendFormat("{0:x2}", b);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Helper function, merges two byte arrays
        /// </summary>
        /// <remarks>Sourced from MSDN Forum</remarks>
        /// <param name="a">A</param>
        /// <param name="b">B</param>
        /// <returns>C</returns>
        private static byte[] AppendArrays(byte[] a, byte[] b)
        {
            byte[] c = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, c, 0, a.Length);
            Buffer.BlockCopy(b, 0, c, a.Length, b.Length);
            return c;
        }
    }
}