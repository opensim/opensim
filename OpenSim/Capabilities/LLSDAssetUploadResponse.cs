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

namespace OpenSim.Framework.Capabilities
{
    [OSDMap]
    public class LLSDAssetUploadError
    {
        public string message = String.Empty;
        public UUID identifier = UUID.Zero;
    }

    [OSDMap]
    public class LLSDAssetUploadResponsePricebrkDown
    {
        public int mesh_streaming;
        public int mesh_physics;
        public int mesh_instance;
        public int texture;
        public int model;
    }

    [OSDMap]
    public class LLSDAssetUploadResponseData
    {
        public double resource_cost;
        public double model_streaming_cost;
        public double simulation_cost;
        public double physics_cost;
        public LLSDAssetUploadResponsePricebrkDown upload_price_breakdown = new LLSDAssetUploadResponsePricebrkDown();
    }

    [OSDMap]
    public class LLSDAssetUploadResponse
    {
        public string uploader = String.Empty;
        public string state = String.Empty;
        public int upload_price = 0;
        public LLSDAssetUploadResponseData data = null;
        public LLSDAssetUploadError error = null;
        public LLSDAssetUploadResponse()
        {
        }
    }


    [OSDMap]
    public class LLSDNewFileAngentInventoryVariablePriceReplyResponse
    {
        public int resource_cost;
        public string state;
        public int upload_price;
        public string rsvp;
        
        public LLSDNewFileAngentInventoryVariablePriceReplyResponse()
        {
            state = "confirm_upload";
        }
    }
}