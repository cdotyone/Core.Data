#region Copyright / Comments

// <copyright file="Base64.cs" company="Civic Engineering & IT">Copyright © Civic Engineering & IT 2013</copyright>
// <author>Chris Doty</author>
// <email>dotyc@civicinc.com</email>
// <date>6/4/2013</date>
// <summary></summary>

#endregion Copyright / Comments

#region References

using System;
using System.Text;

#endregion References

namespace Civic.Core.Data
{
    /// <summary>
    /// Internal class used to encode and decode Base64 strings
    /// </summary>
    internal sealed class Base64
    {
        #region Methods

        /// <summary>
        /// decodes a Base64 string
        /// </summary>
        /// <param name="data">the string to decode</param>
        /// <returns>the decoded string</returns>
        public static string Decode(string data)
        {
            try
            {
                var encoder = new UTF8Encoding();
                var utf8Decode = encoder.GetDecoder();

                var todecodeByte = Convert.FromBase64String(data);
                var charCount = utf8Decode.GetCharCount(todecodeByte, 0, todecodeByte.Length);
                var decodedChar = new char[charCount];
                utf8Decode.GetChars(todecodeByte, 0, todecodeByte.Length, decodedChar, 0);

                return new String(decodedChar);
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Decode " + e.Message);
            }
        }

        /// <summary>
        /// encodes a string into a Base64 encoded string
        /// </summary>
        /// <param name="data">the string to encode</param>
        /// <returns>the Base64 encoded string</returns>
        public static string Encode(string data)
        {
            try
            {
                byte[] encDataByte = Encoding.UTF8.GetBytes(data);
                return Convert.ToBase64String(encDataByte);
            }
            catch (Exception e)
            {
                throw new Exception("Error in base64Encode " + e.Message);
            }
        }

        #endregion Methods
    }
}