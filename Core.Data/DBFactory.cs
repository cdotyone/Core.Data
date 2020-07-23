using System;
using System.Configuration;

namespace Core.Data
{
	public static class DatabaseFactory
	{
        /// <summary>
        /// Method for invoking a specified Database service object.  Reads service settings
        /// from the ConnectionSettings.config file.
        /// </summary>
        /// <example>
        /// <code>
        /// Database dbSvc = DatabaseFactory.CreateDatabase("SQL_Customers");
        /// </code>
        /// </example>
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
                name = DataConfig.Current.GetConnectionName(name);

                if (ConfigurationManager.ConnectionStrings[name] == null)
                    throw new ConfigurationErrorsException(string.Format("could not locate connectionString: {0}", name));
                var connection = new SqlDBConnection(ConfigurationManager.ConnectionStrings[name].ConnectionString);
                connection.DBCode = name;
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
        /// from the ConnectionSettings.config file.
        /// </summary>
        /// <example>
        /// <code>
        /// Database dbSvc = DatabaseFactory.CreateDatabase("SQL_Customers");
        /// </code>
        /// </example>
        /// <param name="name">configuration key for database service</param>
        /// <param name="type">The type that controls the bucket of name/value pairs to store the value into</param>
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
        public static IDBConnection CreateDatabase(string name, Type type)
        {
            return CreateDatabase(name, type, ConfigurationLibrarySection.Current.DefaultProvider);
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
        /// <param name="name">configuration key for database service</param>
        /// <param name="type">The type that controls the bucket of name/value pairs to store the value into</param>
        /// <param name="sourceName">configuration source provider name</param>
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
        public static IDBConnection CreateDatabase(string name, Type type, string sourceName)
        {
            try
            {
                name = DataConfig.Current.GetConnectionName(name);

                var source = ConfigurationFactory.Create(sourceName);
                if (source == null) return null;
                var settings = source.GetConnectionString(type, name);
                if (settings == null) return null;

                return new SqlDBConnection(settings.ConnectionString);
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
	}
}
