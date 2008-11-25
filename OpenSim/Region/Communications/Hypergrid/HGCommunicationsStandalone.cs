/**
 * Copyright (c) 2008, Contributors. All rights reserved.
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 * 
 * Redistribution and use in source and binary forms, with or without modification, 
 * are permitted provided that the following conditions are met:
 * 
 *     * Redistributions of source code must retain the above copyright notice, 
 *       this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright notice, 
 *       this list of conditions and the following disclaimer in the documentation 
 *       and/or other materials provided with the distribution.
 *     * Neither the name of the Organizations nor the names of Individual
 *       Contributors may be used to endorse or promote products derived from 
 *       this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND 
 * ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES 
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL 
 * THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, 
 * EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE 
 * GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED 
 * AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING 
 * NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED 
 * OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 */


using OpenSim.Framework;
using OpenSim.Framework.Communications;
using OpenSim.Framework.Communications.Cache;
using OpenSim.Region.Communications.Local;
using OpenSim.Framework.Servers;

namespace OpenSim.Region.Communications.Hypergrid
{
    public class HGCommunicationsStandalone : CommunicationsLocal
    {
        public HGCommunicationsStandalone(
            NetworkServersInfo serversInfo,
            BaseHttpServer httpServer,
            AssetCache assetCache,
            IUserService userService,
            IUserServiceAdmin userServiceAdmin,
            LocalInventoryService inventoryService,
            IInterRegionCommunications interRegionService,
            HGGridServices gridService, IMessagingService messageService, LibraryRootFolder libraryRootFolder, bool dumpAssetsToFile)
            : base(serversInfo, httpServer, assetCache, userService, userServiceAdmin, inventoryService, interRegionService, gridService, messageService, libraryRootFolder, dumpAssetsToFile)
        {
            gridService.UserProfileCache = m_userProfileCacheService;
            m_assetCache = assetCache;
            // Let's swap to always be secure access to inventory
            AddSecureInventoryService((ISecureInventoryService)inventoryService);
            m_inventoryServices = null;
        }

    }
}
