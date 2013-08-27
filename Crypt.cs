#region Copyright / Comments

// <copyright file="Crypt.cs" company="Civic Engineering & IT">Copyright © Civic Engineering & IT 2013</copyright>
// <author>Chris Doty</author>
// <email>dotyc@civicinc.com</email>
// <date>6/4/2013</date>
// <summary>
// derived from code written by Tiberius OsBurn
// http://tiberi.us/view_article.aspx?article_id=20
// </summary>

#endregion Copyright / Comments

#region References

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

#endregion References

namespace Civic.Core.Data
{
    /// <summary>
    /// Internal class used to encrypt and decrypt connection strings
    /// </summary>
    internal class Crypt
    {
        #region Methods

        /// <summary>
        /// decrypts a string that was previously encrypted with a 3DES cypher
        /// </summary>
        /// <param name="encText">the encrypted string that is to be decrypted</param>
        /// <param name="decryptKB">the key to decrypt the string with</param>
        /// <returns>the decrypted string, or an error message why the string could not be decrypted</returns>
        public static string Decrypt3DES(string encText, byte[] decryptKB)
        {
            // byte[] SB = Encoding.ASCII.GetBytes(strSaltValue);
            byte[] iv = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };

            // Convert our ciphertext into a byte array.
            byte[] cipherTextBytes = Convert.FromBase64String(encText);

            try
            {
                // get the decryptor;
                var symmetricKey = new TripleDESCryptoServiceProvider {Mode = CipherMode.CBC};
                ICryptoTransform decryptor = symmetricKey.CreateDecryptor(decryptKB, iv);

                // Define memory stream which will be used to hold encrypted data.
                var memoryStream = new MemoryStream(cipherTextBytes);

                // Define cryptographic stream (always use Write mode for encryption).
                var cryptoStream = new CryptoStream(memoryStream,
                                                             decryptor,
                                                             CryptoStreamMode.Read);

                var plainTextBytes = new byte[cipherTextBytes.Length];

                // Start decrypting.
                int decryptedByteCount = cryptoStream.Read(plainTextBytes,
                                                           0,
                                                           plainTextBytes.Length);

                // Close both streams.
                memoryStream.Close();
                cryptoStream.Close();

                return Encoding.UTF8.GetString(plainTextBytes,
                                                   0,
                                                   decryptedByteCount);
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// decrypts a string that was previously encrypted with 3DES cypher and a RSA key
        /// </summary>
        /// <param name="encText">the encrypted string that is to be decrypted</param>
        /// <param name="keyContainer">the name of the RSA to be used when dycrypting the string</param>
        /// <returns>the decrypted string, or an error message why the string could not be decrypted</returns>
        public static string Decrypt3DESUsingRSAKey(string encText, string keyContainer)
        {
            var cspParam = new CspParameters
                               {Flags = CspProviderFlags.UseMachineKeyStore, KeyContainerName = keyContainer};
            var prov = new RSACryptoServiceProvider(cspParam);

            string str = encText.Substring(0, encText.IndexOf("##|##", StringComparison.Ordinal));
            string key = encText.Substring(encText.IndexOf("##|##", StringComparison.Ordinal) + 5);

            byte[] bufkey = Convert.FromBase64String(key);
            byte[] radkey = prov.Decrypt(bufkey, false);

            return Decrypt3DES(str, radkey);
        }

        /// <summary>
        /// decrypts a string that was previously encrypted with an AES cypher
        /// </summary>
        /// <param name="encText">the encrypted string that is to be decrypted</param>
        /// <param name="decrKey">the key to use when dycrypting the string</param>
        /// <returns>the decrypted string, or an error message why the string could not be decrypted</returns>
        public static string DecryptAES(string encText, string decrKey)
        {
            byte[] iv = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };

            try
            {
                byte[] byKey = Encoding.UTF8.GetBytes(decrKey.Substring(0, 16));
                var des = new AesCryptoServiceProvider();
                byte[] inputByteArray = Convert.FromBase64String(encText);
                var ms = new MemoryStream();
                var cs = new CryptoStream(ms, des.CreateDecryptor(byKey, iv), CryptoStreamMode.Write);

                cs.Write(inputByteArray, 0, inputByteArray.Length);
                cs.FlushFinalBlock();

                Encoding encoding = Encoding.UTF8;

                return (encoding.GetString(ms.ToArray()));
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// decrypts a string that was previously encrypted with a DES cypher
        /// </summary>
        /// <param name="encText">the encrypted string that is to be decrypted</param>
        /// <param name="decrKey">the key to use when dycrypting the string</param>
        /// <returns>the decrypted string, or an error message why the string could not be decrypted</returns>
        public static string DecryptDES(string encText, string decrKey)
        {
            byte[] iv = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };

            try
            {
                byte[] byKey = Encoding.UTF8.GetBytes(decrKey.Substring(0, 8));
                var des = new DESCryptoServiceProvider();
                byte[] inputByteArray = Convert.FromBase64String(encText);
                var ms = new MemoryStream();
                var cs = new CryptoStream(ms, des.CreateDecryptor(byKey, iv), CryptoStreamMode.Write);

                cs.Write(inputByteArray, 0, inputByteArray.Length);
                cs.FlushFinalBlock();

                Encoding encoding = Encoding.UTF8;

                return (encoding.GetString(ms.ToArray()));
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        /// <summary>
        /// encrypts a string using a 3DES cypher
        /// </summary>
        /// <param name="strText">the string to encrypt</param>
        /// <param name="encryptKB">the key to encrypt the string with</param>
        /// <returns>the encrypted string</returns>
        public static string Encrypt3DES(string strText, byte[] encryptKB)
        {
            // byte[] SB = Encoding.ASCII.GetBytes(strSaltValue);
            byte[] iv = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };

            try
            {
                // get text in bytes
                byte[] inputByteArray = Encoding.UTF8.GetBytes(strText);

                // get the encryptor;
                var symmetricKey = new TripleDESCryptoServiceProvider {Mode = CipherMode.CBC};
                ICryptoTransform encryptor = symmetricKey.CreateEncryptor(encryptKB, iv);

                // Define memory stream which will be used to hold encrypted data.
                var memoryStream = new MemoryStream();

                // Define cryptographic stream (always use Write mode for encryption).
                var cryptoStream = new CryptoStream(memoryStream,
                                                             encryptor,
                                                             CryptoStreamMode.Write);
                // Start encrypting.
                cryptoStream.Write(inputByteArray, 0, inputByteArray.Length);

                // Finish encrypting.
                cryptoStream.FlushFinalBlock();

                // Convert our encrypted data from a memory stream into a byte array.
                byte[] cipherTextBytes = memoryStream.ToArray();

                // Close both streams.
                memoryStream.Close();
                cryptoStream.Close();

                return Convert.ToBase64String(cipherTextBytes);

            }
            catch (Exception ex)
            {
                return (ex.Message);
            }
        }

        /// <summary>
        /// encrypts a string using an 3DES cypher with a RSA key
        /// </summary>
        /// <param name="strText">the string to encrypt</param>
        /// <param name="keyContainer">the name of the RSA key</param>
        /// <returns>the encrypted string</returns>
        public static string Encrypt3DESUsingRSAKey(string strText, string keyContainer)
        {
            var cspParam = new CspParameters
                               {Flags = CspProviderFlags.UseMachineKeyStore, KeyContainerName = keyContainer};
            var prov = new RSACryptoServiceProvider(cspParam);
            string pubKey = prov.ToXmlString(false);

            var pub = new RSACryptoServiceProvider();
            pub.FromXmlString(pubKey);

            // build a random 24 byte key;
            byte[] radkey = getRandom(24);
            byte[] buf = pub.Encrypt(radkey, false);

            string key = Convert.ToBase64String(buf);
            return Encrypt3DES(strText, radkey) + "##|##" + key;
        }

        /// <summary>
        /// encrypts a string using an AES cypher
        /// AES complies with FIPS
        /// </summary>
        /// <param name="strText">string to encrypt</param>
        /// <param name="strEncrKey">the encyrption key to encrypt the string with</param>
        /// <returns>the encrypted string</returns>
        public static string EncryptAES(string strText, string strEncrKey)
        {
            byte[] iv = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF, 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };

            try
            {
                byte[] byKey = Encoding.UTF8.GetBytes(strEncrKey.Substring(0, 16));
                var aes = new AesCryptoServiceProvider();
                byte[] inputByteArray = Encoding.UTF8.GetBytes(strText);
                var ms = new MemoryStream();
                var cs = new CryptoStream(ms, aes.CreateEncryptor(byKey, iv), CryptoStreamMode.Write);

                cs.Write(inputByteArray, 0, inputByteArray.Length);
                cs.FlushFinalBlock();

                return Convert.ToBase64String(ms.ToArray());

            }
            catch (Exception ex)
            {
                return (ex.Message);
            }
        }

        /// <summary>
        /// encrypts a string using a DES cypher
        /// </summary>
        /// <param name="strText">string to encrypt</param>
        /// <param name="strEncrKey">the encryption key to use when encrypting</param>
        /// <returns>the encrypted string</returns>
        public static string EncryptDES(string strText, string strEncrKey)
        {
            byte[] iv = { 0x12, 0x34, 0x56, 0x78, 0x90, 0xAB, 0xCD, 0xEF };

            try
            {
                byte[] byKey = Encoding.UTF8.GetBytes(strEncrKey.Substring(0, 8));
                var des = new DESCryptoServiceProvider();
                byte[] inputByteArray = Encoding.UTF8.GetBytes(strText);
                var ms = new MemoryStream();
                var cs = new CryptoStream(ms, des.CreateEncryptor(byKey, iv), CryptoStreamMode.Write);

                cs.Write(inputByteArray, 0, inputByteArray.Length);
                cs.FlushFinalBlock();

                return Convert.ToBase64String(ms.ToArray());

            }
            catch (Exception ex)
            {
                return (ex.Message);
            }
        }

        /// <summary>
        /// makes a random string of characters
        /// </summary>
        /// <param name="cb">the length of the random string</param>
        /// <returns>the random string</returns>
        public static string GetRandom(int cb)
        {
            return Convert.ToBase64String(getRandom(cb));
        }

        /// <summary>
        /// makes a random array of bytes
        /// </summary>
        /// <param name="cb">the number of random bytes in the array</param>
        /// <returns>the array of random bytes</returns>
        private static byte[] getRandom(int cb)
        {
            var randomData = new byte[cb];
            var srng = new RNGCryptoServiceProvider();
            srng.GetBytes(randomData);
            return randomData;
        }

        #endregion Methods
    }
}