/**
 * Copyright (c), Contributors. All rights reserved.
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

using System;
using OpenSim.Framework.Servers;
using OpenSim.Framework.Servers.HttpServer;

using OpenMetaverse;

namespace OpenSim.Framework.Communications.Clients
{
    public class InventoryClient
    {
        private string ServerURL;

        public InventoryClient(string url)
        {
            ServerURL = url;
        }

        public void GetInventoryItemAsync(InventoryItemBase item, ReturnResponse<InventoryItemBase> callBack)
        {
            System.Console.WriteLine("[HGrid] GetInventory from " + ServerURL);
            try
            {
                RestSessionObjectPosterResponse<InventoryItemBase, InventoryItemBase> requester
                        = new RestSessionObjectPosterResponse<InventoryItemBase, InventoryItemBase>();
                requester.ResponseCallback = callBack;

                requester.BeginPostObject(ServerURL + "/GetItem/", item, string.Empty, string.Empty);
            }
            catch (Exception e)
            {
                System.Console.WriteLine("[HGrid]: Exception posting to inventory: " + e);
            }
        }

        public InventoryItemBase GetInventoryItem(InventoryItemBase item)
        {
            System.Console.WriteLine("[HGrid] GetInventory " + item.ID + " from " + ServerURL);
            try
            {
                item = SynchronousRestSessionObjectPoster<Guid, InventoryItemBase>.BeginPostObject("POST", ServerURL + "/GetItem/", item.ID.Guid, "", "");
                return item;
            }
            catch (Exception e)
            {
                System.Console.WriteLine("[HGrid]: Exception posting to inventory: " + e);
            }
            return null;
        }

    }
}
