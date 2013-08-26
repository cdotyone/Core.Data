#region Copyright / Comments

// <copyright file="ICache.cs" company="Civic Engineering & IT">Copyright © Civic Engineering & IT 2013</copyright>
// <author>Chris Doty</author>
// <email>cdoty@polaropposite.com</email>
// <date>6/4/2013</date>
// <summary></summary>

#endregion Copyright / Comments

#region References

using System;
using System.Xml;

#endregion References

namespace Civic.Core.Data
{
    /// <summary>
    /// Describes a cache provider class that will be used to cache the results of executed sql statements
    /// </summary>
    public interface ICache
    {
        #region Methods

        /// <summary>
        /// Used to create the cache provider
        /// </summary>
        /// <param name="config">the configuration xml for the cache provider</param>
        /// <returns>a new intance of the cache provider</returns>
        ICache Create(XmlNode config);

        /// <summary>
        /// Get a cached result from the cache
        /// </summary>
        /// <param name="key">the key that is used to represent the cached resultset</param>
        /// <returns>the cached resultset, or null if not found</returns>
        object Get(string key);

        /// <summary>
        /// Stores a result set in the cache
        /// </summary>
        /// <param name="key">the key that is used to represent the cached resultset</param>
        /// <param name="value">the resultset that is to be cached</param>
        /// <returns></returns>
        bool Set(string key, object value);


        /// <summary>
        /// Stores a result set in the cache
        /// </summary>
        /// <param name="key">the key that is used to represent the cached resultset</param>
        /// <param name="value">the resultset that is to be cached</param>
        /// <param name="expiry">the absolute time the cached item becomes stale and is considered expired</param>
        /// <returns></returns>
        bool Set(string key, object value, DateTime expiry);

        #endregion Methods
    }
}