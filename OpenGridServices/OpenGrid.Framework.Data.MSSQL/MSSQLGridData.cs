/*
* Copyright (c) OpenSim project, http://www.openmetaverse.org/
*
* Redistribution and use in source and binary forms, with or without
* modification, are permitted provided that the following conditions are met:
*     * Redistributions of source code must retain the above copyright
*       notice, this list of conditions and the following disclaimer.
*     * Redistributions in binary form must reproduce the above copyright
*       notice, this list of conditions and the following disclaimer in the
*       documentation and/or other materials provided with the distribution.
*     * Neither the name of the <organization> nor the
*       names of its contributors may be used to endorse or promote products
*       derived from this software without specific prior written permission.
*
* THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
* EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
* WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
* DISCLAIMED. IN NO EVENT SHALL <copyright holder> BE LIABLE FOR ANY
* DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
* (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
* LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
* ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
* (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
* SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
* 
*/
using System;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Data;

namespace OpenGrid.Framework.Data.MSSQL
{
    /// <summary>
    /// A grid data interface for Microsoft SQL Server
    /// </summary>
    public class SqlGridData : IGridData
    {
        /// <summary>
        /// Database manager
        /// </summary>
        private MSSqlManager database;

        /// <summary>
        /// Initialises the Grid Interface
        /// </summary>
        public void Initialise()
        {
            database = new MSSqlManager("localhost", "db", "user", "password", "false");
        }

        /// <summary>
        /// Shuts down the grid interface
        /// </summary>
        public void Close()
        {
            database.Close();
        }

        /// <summary>
        /// Returns the storage system name
        /// </summary>
        /// <returns>A string containing the storage system name</returns>
        public string getName()
        {
            return "Sql OpenGridData";
        }

        /// <summary>
        /// Returns the storage system version
        /// </summary>
        /// <returns>A string containing the storage system version</returns>
        public string getVersion()
        {
            return "0.1";
        }

        /// <summary>
        /// Returns a list of regions within the specified ranges
        /// </summary>
        /// <param name="a">minimum X coordinate</param>
        /// <param name="b">minimum Y coordinate</param>
        /// <param name="c">maximum X coordinate</param>
        /// <param name="d">maximum Y coordinate</param>
        /// <returns>An array of region profiles</returns>
        public SimProfileData[] GetProfilesInRange(uint a, uint b, uint c, uint d)
        {
            return null;
        }

        /// <summary>
        /// Returns a sim profile from it's location
        /// </summary>
        /// <param name="handle">Region location handle</param>
        /// <returns>Sim profile</returns>
        public SimProfileData GetProfileByHandle(ulong handle)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["handle"] = handle.ToString();

            System.Data.IDbCommand result = database.Query("SELECT * FROM regions WHERE handle = @handle", param);
            System.Data.IDataReader reader = result.ExecuteReader();

            SimProfileData row = database.getRow(reader);
            reader.Close();
            result.Dispose();

            return row;
        }

        /// <summary>
        /// Returns a sim profile from it's UUID
        /// </summary>
        /// <param name="uuid">The region UUID</param>
        /// <returns>The sim profile</returns>
        public SimProfileData GetProfileByLLUUID(libsecondlife.LLUUID uuid)
        {
            Dictionary<string, string> param = new Dictionary<string, string>();
            param["uuid"] = uuid.ToStringHyphenated();

            System.Data.IDbCommand result = database.Query("SELECT * FROM regions WHERE uuid = @uuid", param);
            System.Data.IDataReader reader = result.ExecuteReader();

            SimProfileData row = database.getRow(reader);
            reader.Close();
            result.Dispose();

            return row;
        }

        /// <summary>
        /// Adds a new specified region to the database
        /// </summary>
        /// <param name="profile">The profile to add</param>
        /// <returns>A dataresponse enum indicating success</returns>
        public DataResponse AddProfile(SimProfileData profile)
        {
            if (database.insertRow(profile))
            {
                return DataResponse.RESPONSE_OK;
            }
            else
            {
                return DataResponse.RESPONSE_ERROR;
            }
        }

        /// <summary>
        /// DEPRECIATED. Attempts to authenticate a region by comparing a shared secret.
        /// </summary>
        /// <param name="uuid">The UUID of the challenger</param>
        /// <param name="handle">The attempted regionHandle of the challenger</param>
        /// <param name="authkey">The secret</param>
        /// <returns>Whether the secret and regionhandle match the database entry for UUID</returns>
        public bool AuthenticateSim(libsecondlife.LLUUID uuid, ulong handle, string authkey)
        {
            bool throwHissyFit = false; // Should be true by 1.0

            if (throwHissyFit)
                throw new Exception("CRYPTOWEAK AUTHENTICATE: Refusing to authenticate due to replay potential.");

            SimProfileData data = GetProfileByLLUUID(uuid);

            return (handle == data.regionHandle && authkey == data.regionSecret);
        }

        /// <summary>
        /// NOT YET FUNCTIONAL. Provides a cryptographic authentication of a region
        /// </summary>
        /// <remarks>This requires a security audit.</remarks>
        /// <param name="uuid"></param>
        /// <param name="handle"></param>
        /// <param name="authhash"></param>
        /// <param name="challenge"></param>
        /// <returns></returns>
        public bool AuthenticateSim(libsecondlife.LLUUID uuid, ulong handle, string authhash, string challenge)
        {
            System.Security.Cryptography.SHA512Managed HashProvider = new System.Security.Cryptography.SHA512Managed();
            System.Text.ASCIIEncoding TextProvider = new ASCIIEncoding();

            byte[] stream = TextProvider.GetBytes(uuid.ToStringHyphenated() + ":" + handle.ToString() + ":" + challenge);
            byte[] hash = HashProvider.ComputeHash(stream);

            return false;
        }
    }


}
