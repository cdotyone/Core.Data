using System.Collections.Concurrent;
using System.Configuration;
using Core.Logging;
using Microsoft.Extensions.Configuration;

namespace Core.Data
{
	public static class DatabaseFactory
    {
        private static readonly ConcurrentDictionary<string, string> _connectionMap = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, string> _schemaMap = new ConcurrentDictionary<string, string>();
        private static IConfiguration _config = null;
        private static readonly object _configLock = new object();
        public static void Init(IConfiguration config)
        {
            lock (_configLock)
            {
                _config = config;


                var section = config.GetSection("Core:Data:Connection");
                foreach (var child in section.GetChildren())
                {
                    _connectionMap[child.Value.ToLowerInvariant()] = child.Value.ToLowerInvariant();
                }

                section = config.GetSection("Core:Data:Schema");
                foreach (var child in section.GetChildren())
                {
                    _schemaMap[child.Value.ToLowerInvariant()] = child.Value.ToLowerInvariant();
                }
            }
        }

        /// <summary>
        /// Method for invoking a specified Database service object.  Reads service settings
        /// from the ConnectionSettings.config file.
        /// </summary>
        /// <example>
        /// <code>
        /// Database dbSvc = DatabaseFactory.CreateDatabase("SQL_Customers");
        /// </code>
        /// </example>
        /// <param name="config">IConfiguration supplies connection string information to database factory</param>
        /// <param name="name">configuration key for database service</param>
        /// <returns>Database</returns>
        /// <exception cref="System.Configuration.ConfigurationException">
        /// <para>- or -</para>
        /// <para>An error exists in the configuration.</para>
        /// <para>- or -</para>
        /// <para>An error occured while reading the configuration.</para>        
        /// </exception>
        /// <exception cref="System.Reflection.TargetInvocationException">
        /// <para>The constructor being called throws an exception.</para>
        /// </exception>
        public static IDBConnection CreateDatabase(string name)
        {
            try
            {
                name = name.ToLowerInvariant();
                var mappedName = GetConnectionName(name);

                var connectionString = _config.GetConnectionString(mappedName);
                if (string.IsNullOrEmpty(connectionString))
                {
                    if (mappedName == name) { throw new ConfigurationErrorsException($"could not locate connectionString: {name}"); }
                    throw new ConfigurationErrorsException($"could not locate connectionString: {name} mapped to {mappedName}");
                }

                var connection = new SqlDBConnection(connectionString) { DBCode = mappedName };
                return connection;
            }
            catch (ConfigurationErrorsException configurationException)
            {
                if (Logger.HandleException(LoggingBoundaries.DataLayer, configurationException))
                    throw;

                throw;
            }
        }

        /// <summary>
        /// Method for invoking a specified Database service object.  Reads service settings
        /// from the given connection string.
        /// </summary>
        /// <example>
        /// <code>    
        /// Database dbSvc = DatabaseFactory.CreateDatabase("SQL_Customers");
        /// </code>
        /// </example>
        /// <param name="connectionString">connection string for database service</param>
        /// <returns>Database</returns>
        /// <exception cref="System.Configuration.ConfigurationException">
        /// <para>- or -</para>
        /// <para>An error exists in the configuration.</para>
        /// <para>- or -</para>
        /// <para>An error occured while reading the string.</para>        
        /// </exception>
        /// <exception cref="System.Reflection.TargetInvocationException">
        /// <para>The constructor being called throws an exception.</para>
        /// </exception>
        public static IDBConnection CreateDatabaseConnectionString(string connectionString)
        {   
            try
            {
                return new SqlDBConnection(connectionString);
            }
            catch (ConfigurationErrorsException configurationException)
            {
                if (Logger.HandleException(LoggingBoundaries.DataLayer, configurationException))
                    throw;

                throw;
            }
        }


        public static string GetConnectionName(string name)
        {
            if (_connectionMap.ContainsKey(name)) return _connectionMap[name];
            var connectionName = name;

            if (_config.GetConnectionString(connectionName) == null)
            {
                return "default";
            }

            return connectionName;
        }

        public static string GetSchemaName(string name)
        {
            if (_schemaMap.ContainsKey(name)) return _schemaMap[name];
            return name;
        }
    }
}
