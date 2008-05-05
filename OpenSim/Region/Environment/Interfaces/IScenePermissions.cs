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
 *     * Neither the name of the OpenSim Project nor the
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

using libsecondlife;

namespace OpenSim.Region.Environment.Interfaces
{ 
    public interface IScenePermissions
    {
        bool BypassPermissions { get; set; }

        #region Object Permissions
        
        bool CanRezObject(LLUUID user, LLVector3 position);
        
        /// <summary>
        /// Permissions check - can user delete an object?
        /// </summary>
        /// <param name="user">User attempting the delete</param>
        /// <param name="obj">Target object</param>
        /// <returns>Has permission?</returns>
        bool CanDeRezObject(LLUUID user, LLUUID obj);
        
        bool CanCopyObject(LLUUID user, LLUUID obj);
        
        bool CanEditObject(LLUUID user, LLUUID obj);
        
        bool CanEditObjectPosition(LLUUID user, LLUUID obj);
        
        /// <summary>
        /// Permissions check - can user enter an object?
        /// </summary>
        /// <param name="user">User attempting move an object</param>
        /// <param name="oldPos">Source object-position</param>
        /// <param name="newPos">Target object-position</param>
        /// <returns>Has permission?</returns>
        bool CanObjectEntry(LLUUID user, LLVector3 oldPos, LLVector3 newPos);
            
        bool CanReturnObject(LLUUID user, LLUUID obj);
                
        #endregion
                
        #region Uncategorized permissions
        
        bool CanInstantMessage(LLUUID user, LLUUID target);
        
        bool CanInventoryTransfer(LLUUID user, LLUUID target);
        
        bool CanEditScript(LLUUID user, LLUUID script);
        
        bool CanRunScript(LLUUID user, LLUUID script);
        
        bool CanRunConsoleCommand(LLUUID user);
        
        bool CanTerraform(LLUUID user, LLVector3 position);
        
        #endregion
        
        #region Estate Permissions
        
        bool IsEstateManager(LLUUID user);
        
        bool GenericEstatePermission(LLUUID user);
        
        bool CanEditEstateTerrain(LLUUID user);
        
        bool CanRestartSim(LLUUID user);
        
        bool CanEditParcel(LLUUID user, ILandObject parcel);

        bool CanSellParcel(LLUUID user, ILandObject parcel);

        bool CanAbandonParcel(LLUUID user, ILandObject parcel);

        #endregion        
        
        uint GenerateClientFlags(LLUUID user, LLUUID objID);
    }
}
