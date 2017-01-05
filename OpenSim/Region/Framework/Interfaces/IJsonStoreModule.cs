/*
 * Copyright (c) Contributors
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

using System;
using System.Reflection;
using OpenMetaverse;

namespace OpenSim.Region.Framework.Interfaces
{
    // these could be expanded at some point to provide more type information
    // for now value accounts for all base types
    public enum JsonStoreNodeType
    {
        Undefined = 0,
        Object = 1,
        Array = 2,
        Value = 3
    }

    public enum JsonStoreValueType
    {
        Undefined = 0,
        Boolean = 1,
        Integer = 2,
        Float = 3,
        String = 4,
        UUID = 5
    }

    public struct JsonStoreStats
    {
        public int StoreCount;
    }

    public delegate void TakeValueCallback(string s);

    public interface IJsonStoreModule
    {
        JsonStoreStats GetStoreStats();

        bool AttachObjectStore(UUID objectID);
        bool CreateStore(string value, ref UUID result);
        bool DestroyStore(UUID storeID);

        JsonStoreNodeType GetNodeType(UUID storeID, string path);
        JsonStoreValueType GetValueType(UUID storeID, string path);

        bool TestStore(UUID storeID);

        bool SetValue(UUID storeID, string path, string value, bool useJson);
        bool RemoveValue(UUID storeID, string path);
        bool GetValue(UUID storeID, string path, bool useJson, out string value);

        void TakeValue(UUID storeID, string path, bool useJson, TakeValueCallback cback);
        void ReadValue(UUID storeID, string path, bool useJson, TakeValueCallback cback);

        int GetArrayLength(UUID storeID, string path);
    }
}
