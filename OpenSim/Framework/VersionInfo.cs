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

namespace OpenSim
{
    public class VersionInfo
    {
        public const string VersionNumber = "0.9.1.0";
        public const string AssemblyVersionNumber = "0.9.1.*";

        private const Flavour VERSION_FLAVOUR = Flavour.Dev;

        public enum Flavour
        {
            Unknown,
            Dev,
            RC1,
            RC2,
            RC3,
            Release,
            Post_Fixes,
            Extended
        }

        public static string Version
        {
            get { return GetVersionString(VersionNumber, VERSION_FLAVOUR); }
        }

        public static string GetVersionString(string versionNumber, Flavour flavour)
        {
            string versionString = "OpenSim " + versionNumber + " " + flavour;
            return versionString.PadRight(VERSIONINFO_VERSION_LENGTH);
        }

        public const int VERSIONINFO_VERSION_LENGTH = 27;

        /// <value>
        /// This is the external interface version.  It is separate from the OpenSimulator project version.
        ///
        /// </value>
        /// Commented because it's not used anymore, see below for new
        /// versioning method.
        //public readonly static int MajorInterfaceVersion = 8;

        /// <summary>
        /// This rules versioning regarding teleports, and compatibility between simulators in that regard.
        /// </summary>
        ///
        /// <remarks>
        /// The protocol version that we will use for outgoing transfers
        /// Valid values are
        /// "SIMULATION/0.3"
        ///   - This is the latest, and it supports teleports to variable-sized regions
        ///   - Older versions can teleport to this one, but only if the destination region
        ///     is 256x256
        /// "SIMULATION/0.2"
        ///   - A source simulator which only implements "SIMULATION/0.1" can still teleport here
        ///   - this protocol is more efficient than "SIMULATION/0.1"
        /// "SIMULATION/0.1"
        ///   - this is an older teleport protocol used in OpenSimulator 0.7.5 and before.
        /// </remarks>
        public readonly static float SimulationServiceVersionAcceptedMin = 0.3f;
        public readonly static float SimulationServiceVersionAcceptedMax = 0.6f;
        public readonly static float SimulationServiceVersionSupportedMin = 0.3f;
        public readonly static float SimulationServiceVersionSupportedMax = 0.6f;
    }
}
