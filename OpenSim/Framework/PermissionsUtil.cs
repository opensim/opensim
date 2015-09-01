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
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using log4net;

namespace OpenSim.Framework
{
    public static class PermissionsUtil
    {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Logs permissions flags. Useful when debugging permission problems.
        /// </summary>
        /// <param name="message"></param>
        public static void LogPermissions(String name, String message, uint basePerm, uint curPerm, uint nextPerm)
        {
            m_log.DebugFormat("Permissions of \"{0}\" at \"{1}\": Base {2} ({3:X4}), Current {4} ({5:X4}), NextOwner {6} ({7:X4})",
                name, message,
                PermissionsToString(basePerm), basePerm, PermissionsToString(curPerm), curPerm, PermissionsToString(nextPerm), nextPerm);
        }

        /// <summary>
        /// Converts a permissions bit-mask to a string (e.g., "MCT").
        /// </summary>
        private static string PermissionsToString(uint perms)
        {
            string str = "";
            if ((perms & (int)PermissionMask.Modify) != 0)
                str += "M";
            if ((perms & (int)PermissionMask.Copy) != 0)
                str += "C";
            if ((perms & (int)PermissionMask.Transfer) != 0)
                str += "T";
            if (str == "")
                str = ".";
            return str;
        }

        /// <summary>
        /// Applies an object's folded permissions to its regular permissions.
        /// </summary>
        /// <param name="foldedPerms">The folded permissions. Only the lowest 7 bits are examined.</param>
        /// <param name="mainPerms">The permissions variable to modify.</param>
        public static void ApplyFoldedPermissions(uint foldedPerms, ref uint mainPerms)
        {
//            if ((foldedPerms & 7) == 0)
//                return; // assume that if the folded permissions are 0 then this means that they weren't actually recorded

            if ((foldedPerms & ((uint)PermissionMask.Copy >> 13)) == 0)
                mainPerms &= ~(uint)PermissionMask.Copy;
            if ((foldedPerms & ((uint)PermissionMask.Transfer >> 13)) == 0)
                mainPerms &= ~(uint)PermissionMask.Transfer;
            if ((foldedPerms & ((uint)PermissionMask.Modify >> 13)) == 0)
                mainPerms &= ~(uint)PermissionMask.Modify;
        }

    }
}
