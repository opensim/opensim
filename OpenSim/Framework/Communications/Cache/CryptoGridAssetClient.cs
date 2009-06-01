/*
 * Copyright (c) Contributors, http://www.openmetaverse.org/
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
/*
 * This file includes content derived from Obviex.
 * Copyright (C) 2002 Obviex(TM). All rights reserved.
 * http://www.obviex.com/samples/Encryption.aspx
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Serialization;
using log4net;
using OpenSim.Framework.Servers.HttpServer;

namespace OpenSim.Framework.Communications.Cache
{
    public class CryptoGridAssetClient : AssetServerBase
    {

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private string      _assetServerUrl;
        private bool        m_encryptOnUpload;
        private RjinKeyfile m_encryptKey;
        private readonly Dictionary<string,RjinKeyfile> m_keyfiles = new Dictionary<string, RjinKeyfile>();

        #region IPlugin

        public override string Name
        {
            get { return "Crypto"; }
        }

        public override string Version
        {
            get { return "1.0"; }
        }

        public override void Initialise(ConfigSettings p_set, string p_url, string p_dir, bool p_t)
        {
            m_log.Debug("[CRYPTOGRID] Plugin configured initialisation");
            Initialise(p_url, p_dir, p_t);
        }

        #endregion

        #region Keyfile Classes
        [Serializable]
        public class RjinKeyfile
        {
            public string Secret;
            public string AlsoKnownAs;
            public int Keysize;
            public string IVBytes;
            public string Description = "OpenSim Key";

            private static string SHA1Hash(byte[] bytes)
            {
                SHA1 sha1 = SHA1CryptoServiceProvider.Create();
                byte[] dataMd5 = sha1.ComputeHash(bytes);
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < dataMd5.Length; i++)
                    sb.AppendFormat("{0:x2}", dataMd5[i]);
                return sb.ToString();
            }

            public void GenerateRandom()
            {
                RNGCryptoServiceProvider Gen = new RNGCryptoServiceProvider();

                byte[] genSec = new byte[32];
                byte[] genAKA = new byte[32];
                byte[] genIV = new byte[32];

                Gen.GetBytes(genSec);
                Gen.GetBytes(genAKA);
                Gen.GetBytes(genIV);

                Secret = SHA1Hash(genSec);
                AlsoKnownAs = SHA1Hash(genAKA);
                IVBytes = SHA1Hash(genIV).Substring(0, 16);
                Keysize = 256;
            }
        }
        #endregion

        #region Rjindael
        /// <summary>
        /// This class uses a symmetric key algorithm (Rijndael/AES) to encrypt and
        /// decrypt data. As long as encryption and decryption routines use the same
        /// parameters to generate the keys, the keys are guaranteed to be the same.
        /// The class uses static functions with duplicate code to make it easier to
        /// demonstrate encryption and decryption logic. In a real-life application,
        /// this may not be the most efficient way of handling encryption, so - as
        /// soon as you feel comfortable with it - you may want to redesign this class.
        /// </summary>
        public class UtilRijndael
        {
            /// <summary>
            /// Encrypts specified plaintext using Rijndael symmetric key algorithm
            /// and returns a base64-encoded result.
            /// </summary>
            /// <param name="plainText">
            /// Plaintext value to be encrypted.
            /// </param>
            /// <param name="passPhrase">
            /// Passphrase from which a pseudo-random password will be derived. The
            /// derived password will be used to generate the encryption key.
            /// Passphrase can be any string. In this example we assume that this
            /// passphrase is an ASCII string.
            /// </param>
            /// <param name="saltValue">
            /// Salt value used along with passphrase to generate password. Salt can
            /// be any string. In this example we assume that salt is an ASCII string.
            /// </param>
            /// <param name="hashAlgorithm">
            /// Hash algorithm used to generate password. Allowed values are: "MD5" and
            /// "SHA1". SHA1 hashes are a bit slower, but more secure than MD5 hashes.
            /// </param>
            /// <param name="passwordIterations">
            /// Number of iterations used to generate password. One or two iterations
            /// should be enough.
            /// </param>
            /// <param name="initVector">
            /// Initialization vector (or IV). This value is required to encrypt the
            /// first block of plaintext data. For RijndaelManaged class IV must be
            /// exactly 16 ASCII characters long.
            /// </param>
            /// <param name="keySize">
            /// Size of encryption key in bits. Allowed values are: 128, 192, and 256.
            /// Longer keys are more secure than shorter keys.
            /// </param>
            /// <returns>
            /// Encrypted value formatted as a base64-encoded string.
            /// </returns>
            public static byte[] Encrypt(byte[] plainText,
                                         string passPhrase,
                                         string saltValue,
                                         string hashAlgorithm,
                                         int passwordIterations,
                                         string initVector,
                                         int keySize)
            {
                // Convert strings into byte arrays.
                // Let us assume that strings only contain ASCII codes.
                // If strings include Unicode characters, use Unicode, UTF7, or UTF8
                // encoding.
                byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
                byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);

                // Convert our plaintext into a byte array.
                // Let us assume that plaintext contains UTF8-encoded characters.
                byte[] plainTextBytes = plainText;

                // First, we must create a password, from which the key will be derived.
                // This password will be generated from the specified passphrase and
                // salt value. The password will be created using the specified hash
                // algorithm. Password creation can be done in several iterations.
                PasswordDeriveBytes password = new PasswordDeriveBytes(
                                                                passPhrase,
                                                                saltValueBytes,
                                                                hashAlgorithm,
                                                                passwordIterations);

                // Use the password to generate pseudo-random bytes for the encryption
                // key. Specify the size of the key in bytes (instead
                // of bits).
                #pragma warning disable 0618
                byte[] keyBytes = password.GetBytes(keySize / 8);
                #pragma warning restore 0618

                // Create uninitialized Rijndael encryption object.
                RijndaelManaged symmetricKey = new RijndaelManaged();

                // It is reasonable to set encryption mode to Cipher Block Chaining
                // (CBC). Use default options for other symmetric key parameters.
                symmetricKey.Mode = CipherMode.CBC;

                // Generate encryptor from the existing key bytes and initialization
                // vector. Key size will be defined based on the number of the key
                // bytes.
                ICryptoTransform encryptor = symmetricKey.CreateEncryptor(
                                                                 keyBytes,
                                                                 initVectorBytes);

                // Define memory stream which will be used to hold encrypted data.
                MemoryStream memoryStream = new MemoryStream();

                // Define cryptographic stream (always use Write mode for encryption).
                CryptoStream cryptoStream = new CryptoStream(memoryStream,
                                                             encryptor,
                                                             CryptoStreamMode.Write);
                // Start encrypting.
                cryptoStream.Write(plainTextBytes, 0, plainTextBytes.Length);

                // Finish encrypting.
                cryptoStream.FlushFinalBlock();

                // Convert our encrypted data from a memory stream into a byte array.
                byte[] cipherTextBytes = memoryStream.ToArray();

                // Close both streams.
                memoryStream.Close();
                cryptoStream.Close();

                // Return encrypted string.
                return cipherTextBytes;
            }

            /// <summary>
            /// Decrypts specified ciphertext using Rijndael symmetric key algorithm.
            /// </summary>
            /// <param name="cipherText">
            /// Base64-formatted ciphertext value.
            /// </param>
            /// <param name="passPhrase">
            /// Passphrase from which a pseudo-random password will be derived. The
            /// derived password will be used to generate the encryption key.
            /// Passphrase can be any string. In this example we assume that this
            /// passphrase is an ASCII string.
            /// </param>
            /// <param name="saltValue">
            /// Salt value used along with passphrase to generate password. Salt can
            /// be any string. In this example we assume that salt is an ASCII string.
            /// </param>
            /// <param name="hashAlgorithm">
            /// Hash algorithm used to generate password. Allowed values are: "MD5" and
            /// "SHA1". SHA1 hashes are a bit slower, but more secure than MD5 hashes.
            /// </param>
            /// <param name="passwordIterations">
            /// Number of iterations used to generate password. One or two iterations
            /// should be enough.
            /// </param>
            /// <param name="initVector">
            /// Initialization vector (or IV). This value is required to encrypt the
            /// first block of plaintext data. For RijndaelManaged class IV must be
            /// exactly 16 ASCII characters long.
            /// </param>
            /// <param name="keySize">
            /// Size of encryption key in bits. Allowed values are: 128, 192, and 256.
            /// Longer keys are more secure than shorter keys.
            /// </param>
            /// <returns>
            /// Decrypted string value.
            /// </returns>
            /// <remarks>
            /// Most of the logic in this function is similar to the Encrypt
            /// logic. In order for decryption to work, all parameters of this function
            /// - except cipherText value - must match the corresponding parameters of
            /// the Encrypt function which was called to generate the
            /// ciphertext.
            /// </remarks>
            public static byte[] Decrypt(byte[] cipherText,
                                         string passPhrase,
                                         string saltValue,
                                         string hashAlgorithm,
                                         int passwordIterations,
                                         string initVector,
                                         int keySize)
            {
                // Convert strings defining encryption key characteristics into byte
                // arrays. Let us assume that strings only contain ASCII codes.
                // If strings include Unicode characters, use Unicode, UTF7, or UTF8
                // encoding.
                byte[] initVectorBytes = Encoding.ASCII.GetBytes(initVector);
                byte[] saltValueBytes = Encoding.ASCII.GetBytes(saltValue);

                // Convert our ciphertext into a byte array.
                byte[] cipherTextBytes = cipherText;

                // First, we must create a password, from which the key will be
                // derived. This password will be generated from the specified
                // passphrase and salt value. The password will be created using
                // the specified hash algorithm. Password creation can be done in
                // several iterations.
                PasswordDeriveBytes password = new PasswordDeriveBytes(passPhrase,
                                                                       saltValueBytes,
                                                                       hashAlgorithm,
                                                                       passwordIterations);

                // Use the password to generate pseudo-random bytes for the encryption
                // key. Specify the size of the key in bytes (instead
                // of bits).
                #pragma warning disable 0618
                byte[] keyBytes = password.GetBytes(keySize / 8);
                #pragma warning restore 0618

                // Create uninitialized Rijndael encryption object.
                RijndaelManaged symmetricKey = new RijndaelManaged();

                // It is reasonable to set encryption mode to Cipher Block Chaining
                // (CBC). Use default options for other symmetric key parameters.
                symmetricKey.Mode = CipherMode.CBC;

                // Generate decryptor from the existing key bytes and initialization
                // vector. Key size will be defined based on the number of the key
                // bytes.
                ICryptoTransform decryptor = symmetricKey.CreateDecryptor(
                                                                 keyBytes,
                                                                 initVectorBytes);

                // Define memory stream which will be used to hold encrypted data.
                MemoryStream memoryStream = new MemoryStream(cipherTextBytes);

                // Define cryptographic stream (always use Read mode for encryption).
                CryptoStream cryptoStream = new CryptoStream(memoryStream,
                                                              decryptor,
                                                              CryptoStreamMode.Read);

                // Since at this point we don't know what the size of decrypted data
                // will be, allocate the buffer long enough to hold ciphertext;
                // plaintext is never longer than ciphertext.
                byte[] plainTextBytes = new byte[cipherTextBytes.Length];

                // Start decrypting.
                int decryptedByteCount = cryptoStream.Read(plainTextBytes,
                                                           0,
                                                           plainTextBytes.Length);

                // Close both streams.
                memoryStream.Close();
                cryptoStream.Close();

                byte[] plainText = new byte[decryptedByteCount];
                int i;
                for (i = 0; i < decryptedByteCount; i++)
                    plainText[i] = plainTextBytes[i];

                // Return decrypted string.
                return plainText;
            }
        }
        #endregion

        public CryptoGridAssetClient() {}

        public CryptoGridAssetClient(string serverUrl, string keydir, bool decOnly)
        {
            m_log.Debug("[CRYPTOGRID] Direct constructor");
            Initialise(serverUrl, keydir, decOnly);
        }

        public void Initialise(string serverUrl, string keydir, bool decOnly)
        {

            m_log.Debug("[CRYPTOGRID] Common constructor");

            _assetServerUrl = serverUrl;

            string[] keys = Directory.GetFiles(keydir, "*.deckey");
            foreach (string key in keys)
            {
                XmlSerializer xs = new XmlSerializer(typeof (RjinKeyfile));
                FileStream file = new FileStream(key, FileMode.Open, FileAccess.Read);

                RjinKeyfile rjkey = (RjinKeyfile) xs.Deserialize(file);

                file.Close();

                m_keyfiles.Add(rjkey.AlsoKnownAs, rjkey);
            }


            keys = Directory.GetFiles(keydir, "*.enckey");
            if (keys.Length == 1)
            {
                string Ekey = keys[0];
                XmlSerializer Exs = new XmlSerializer(typeof (RjinKeyfile));
                FileStream Efile = new FileStream(Ekey, FileMode.Open, FileAccess.Read);

                RjinKeyfile Erjkey = (RjinKeyfile) Exs.Deserialize(Efile);

                Efile.Close();

                m_keyfiles.Add(Erjkey.AlsoKnownAs, Erjkey);

                m_encryptKey = Erjkey;
            } else
            {
                if (keys.Length > 1)
                    throw new Exception(
                        "You have more than one asset *encryption* key. (You should never have more than one)," +
                        "If you downloaded this key from someone, rename it to <filename>.deckey to convert it to" +
                        "a decryption-only key.");

                m_log.Warn("No encryption key found, generating a new one for you...");
                RjinKeyfile encKey = new RjinKeyfile();
                encKey.GenerateRandom();

                m_encryptKey = encKey;

                FileStream encExportFile = new FileStream("mysecretkey_rename_me.enckey",FileMode.CreateNew);
                XmlSerializer xs = new XmlSerializer(typeof(RjinKeyfile));
                xs.Serialize(encExportFile, encKey);
                encExportFile.Flush();
                encExportFile.Close();

                m_log.Info(
                    "Encryption file generated, please rename 'mysecretkey_rename_me.enckey' to something more appropriate (however preserve the file extension).");
            }

            // If Decrypt-Only, dont encrypt on upload
            m_encryptOnUpload = !decOnly;
        }

        private static void EncryptAssetBase(AssetBase x, RjinKeyfile file)
        {
            // Make a salt
            RNGCryptoServiceProvider RandomGen = new RNGCryptoServiceProvider();
            byte[] rand = new byte[32];
            RandomGen.GetBytes(rand);

            string salt = Convert.ToBase64String(rand);

            x.Data = UtilRijndael.Encrypt(x.Data, file.Secret, salt, "SHA1", 2, file.IVBytes, file.Keysize);
            x.Description = String.Format("ENCASS#:~:#{0}#:~:#{1}#:~:#{2}#:~:#{3}",
                                          "OPENSIM_AES_AF1",
                                          file.AlsoKnownAs,
                                          salt,
                                          x.Description);
        }

        private bool DecryptAssetBase(AssetBase x)
        {
            // Check it's encrypted first.
            if (!x.Description.Contains("ENCASS"))
                return true;

            // ENCASS:ALG:AKA:SALT:Description
            // 0       1   2   3   4
            string[] splitchars = new string[1];
            splitchars[0] = "#:~:#";

            string[] meta = x.Description.Split(splitchars, StringSplitOptions.None);
            if (meta.Length < 5)
            {
                m_log.Warn("[ENCASSETS] Recieved Encrypted Asset, but header is corrupt");
                return false;
            }

            // Check if we have a matching key
            if (m_keyfiles.ContainsKey(meta[2]))
            {
                RjinKeyfile deckey = m_keyfiles[meta[2]];
                x.Description = meta[4];
                switch (meta[1])
                {
                    case "OPENSIM_AES_AF1":
                        x.Data = UtilRijndael.Decrypt(x.Data,
                                                      deckey.Secret,
                                                      meta[3],
                                                      "SHA1",
                                                      2,
                                                      deckey.IVBytes,
                                                      deckey.Keysize);
                        // Decrypted Successfully
                        return true;
                    default:
                        m_log.Warn(
                            "[ENCASSETS] Recieved Encrypted Asset, but we dont know how to decrypt '" + meta[1] + "'.");
                        // We dont understand this encryption scheme
                        return false;
                }
            }

            m_log.Warn("[ENCASSETS] Recieved Encrypted Asset, but we do not have the decryption key.");
            return false;
        }

        #region IAssetServer Members

        protected override AssetBase GetAsset(AssetRequest req)
        {
#if DEBUG
            //m_log.DebugFormat("[GRID ASSET CLIENT]: Querying for {0}", req.AssetID.ToString());
#endif

            RestClient rc = new RestClient(_assetServerUrl);
            rc.AddResourcePath("assets");
            rc.AddResourcePath(req.AssetID.ToString());
            if (req.IsTexture)
                rc.AddQueryParameter("texture");

            rc.RequestMethod = "GET";

            Stream s = rc.Request();

            if (s == null)
                return null;

            if (s.Length > 0)
            {
                XmlSerializer xs = new XmlSerializer(typeof(AssetBase));

                AssetBase encAsset = (AssetBase)xs.Deserialize(s);

                // Try decrypt it
                if (DecryptAssetBase(encAsset))
                    return encAsset;
            }

            return null;
        }

        public override void UpdateAsset(AssetBase asset)
        {
            throw new Exception("The method or operation is not implemented.");
        }

        public override void StoreAsset(AssetBase asset)
        {
            if (m_encryptOnUpload)
                EncryptAssetBase(asset, m_encryptKey);

            try
            {
                string assetUrl = _assetServerUrl + "/assets/";

                m_log.InfoFormat("[CRYPTO GRID ASSET CLIENT]: Sending store request for asset {0}", asset.FullID);

                RestObjectPoster.BeginPostObject<AssetBase>(assetUrl, asset);
            }
            catch (Exception e)
            {
                m_log.ErrorFormat("[CRYPTO GRID ASSET CLIENT]: {0}", e);
            }
        }

        #endregion
    }
}
