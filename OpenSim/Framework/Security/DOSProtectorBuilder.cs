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

namespace OpenSim.Framework.Security
{
    /// <summary>
    /// Builder class for creating DOS protector instances based on options type.
    /// Automatically selects the appropriate implementation (Basic or Advanced).
    /// </summary>
    public static class DOSProtectorBuilder
    {
        /// <summary>
        /// Builds a DOS protector instance based on the options type.
        /// - BasicDosProtectorOptions → BasicDOSProtector
        /// - AdvancedDosProtectorOptions → AdvancedDOSProtector
        /// </summary>
        /// <param name="options">Configuration options (Basic or Advanced)</param>
        /// <returns>IDOSProtector instance</returns>
        /// <example>
        /// <code>
        /// // Creates BasicDOSProtector
        /// var basic = DOSProtectorBuilder.Build(new BasicDosProtectorOptions { ... });
        ///
        /// // Creates AdvancedDOSProtector (automatically detected)
        /// var advanced = DOSProtectorBuilder.Build(new AdvancedDosProtectorOptions
        /// {
        ///     LimitBlockExtensions = true,
        ///     MaxBlockExtensions = 3
        /// });
        ///
        /// // Use with SessionScope pattern
        /// using (var session = protector.CreateSession(key, endpoint))
        /// {
        ///     // handle request
        /// }
        /// </code>
        /// </example>
        public static IDOSProtector Build(BasicDosProtectorOptions options)
        {
            return options switch
            {
                AdvancedDosProtectorOptions advancedOptions => new AdvancedDOSProtector(advancedOptions),
                _ => new BasicDOSProtector(options)
            };
        }
    }
}
