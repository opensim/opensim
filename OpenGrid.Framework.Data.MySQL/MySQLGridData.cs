using System;
using System.Collections.Generic;
using System.Text;
using OpenGrid.Framework.Data;

namespace OpenGrid.Framework.Data.MySQL
{
    public class MySQLGridData : IGridData
    {
        MySQLManager database;

        public void Initialise()
        {
            database = new MySQLManager("localhost", "db", "user", "password", "false");
        }
        public SimProfileData GetProfileByHandle(ulong handle)
        {
            Dictionary<string,string> param = new Dictionary<string,string>();
            param["handle"] = handle.ToString();

            System.Data.IDbCommand result = database.Query("SELECT * FROM regions WHERE handle = @handle", param);
            System.Data.IDataReader reader = result.ExecuteReader();

            SimProfileData row = database.getRow( reader );
            reader.Close();
            result.Dispose();

            return row;
        }
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
        public bool AuthenticateSim(libsecondlife.LLUUID uuid, ulong handle, string authkey)
        {
            bool throwHissyFit = false; // Should be true by 1.0

            if (throwHissyFit)
                throw new Exception("CRYPTOWEAK AUTHENTICATE: Refusing to authenticate due to replay potential.");

            return true;
        }

        /// <summary>
        /// Provides a cryptographic authentication of a region
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
