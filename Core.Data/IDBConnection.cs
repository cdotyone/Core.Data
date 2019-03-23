#region Copyright / Comments

// <copyright file="IDBConnection.cs" company="Civic Engineering & IT">Copyright © Civic Engineering & IT 2013</copyright>
// <author>Chris Doty</author>
// <email>dotyc@civicinc.com</email>
// <date>6/4/2013</date>
// <summary></summary>

#endregion Copyright / Comments

#region References

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Security.Claims;

#endregion References

namespace Core.Data
{
    /// <summary>
    /// Defines an IDBConnection class that is used to connect to a database server
    /// </summary>
    public interface IDBConnection : IDisposable
    {
        #region Properties

        /// <summary>
        /// get/set the how long it takes a query to timeout once executed
        /// </summary>
        int CommandTimeout
        {
            get; set;
        }

        /// <summary>
        /// set the connection string to be used when executing sql commands
        /// </summary>
        string ConnectionString
        {
            set;
            get;
        }

        /// <summary>
        /// get/sets if the last sql string executed
        /// </summary>
        string LastSql
        {
            get;
            set;
        }

        /// <summary>
        /// get/sets the short code for the database connection
        /// </summary>
        string DBCode
        {
            get; set;
        }

        /// <summary>
        /// gets a list of parameters that will be used to be used as default values for defined parameters that do not ahave a value
        /// </summary>
        DbParameter[] DefaultParams
        {
            get;
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Adds a default parameter when executing store procedures
        /// </summary>
        /// <param name="param">the database parameter</param>
        /// <param name="canBeCached">can the parameter be used when caching the result set</param>
        void AddDefaultParameter( DbParameter param, bool canBeCached );


        /// <summary>
        /// Adds default parameters based on claims
        /// </summary>
        /// <param name="claimsPrincipal">The claims principle</param>
        /// <returns>Self</returns>
        IDBConnection AddClaimsDefaults(ClaimsPrincipal claimsPrincipal);

        /// <summary>
        /// Adds a default parameter when executing store procedures
        /// </summary>
        /// <param name="param">the database parameter</param>
        void AddDefaultParameter(DbParameter param);

        /// <summary>
        /// Adds a default parameter when executing store procedures
        /// </summary>
        /// <param name="name">the name of the parameter on the stored procedure</param>
        /// <param name="value">the value for the parameter</param>
        /// <param name="canBeCached">can the parameter be used when caching the result set</param>
        void AddDefaultParameter(string name, object value, bool canBeCached);

        /// <summary>
        /// Adds a default parameter when executing store procedures
        /// </summary>
        /// <param name="name">the name of the parameter on the stored procedure</param>
        /// <param name="value">the value for the parameter</param>
        void AddDefaultParameter(string name, object value);

        /// <summary>
        /// start a sql transaction
        /// </summary>
        void BeginTrans();


        /// <summary>
        /// start a sql transaction
        /// </summary>
        void BeginTrans(bool allowDirty);

        /// <summary>
        /// start a sql transaction
        /// </summary>
        bool IsInTransaction { get; }

        /// <summary>
        /// clones the database connection
        /// </summary>
        /// <returns>the newly cloned database connection</returns>
        IDBConnection Clone();

        /// <summary>
        /// Commit the transaction
        /// </summary>
        void Commit();

        /// <summary>
        /// Close the connection if not already closed
        /// </summary>
        void Close();

        /// <summary>
        /// Creates a DbParameter for the underlying database access layer
        /// </summary>
        /// <param name="name">Name of the parameter to create</param>
        /// <param name="direction">the direction the parameter is meant for in/out</param>
        /// <param name="value">the value for the new parameter</param>
        /// <returns>a DbParameter representing the requested parameter</returns>
        DbParameter CreateParameter(string name, ParameterDirection direction, object value);

        /// <summary>
        /// Creates an input only DbParameter for the underlying database access layer
        /// </summary>
        /// <param name="name">Name of the parameter to create</param>
        /// <param name="value">the value for the new parameter</param>
        /// <returns>a DbParameter representing the requested parameter</returns>
        DbParameter CreateParameter(string name, object value);

        /// <summary>
        /// Creates a return parameter for this database
        /// </summary>
        /// <returns>a DbParameter representing the requested parameter</returns>
        DbParameter CreateReturnParameter();

        /// <summary>
        /// Creates an IDBCommand compatible object for the requested stored procedure
        /// </summary>
        /// <param name="schema">the schema name of the store procedure</param>
        /// <param name="procName">the name of the stored procedure to request the stored procedure for</param>
        /// <returns>The command object for the requested stored procedure</returns>
        IDBCommand CreateStoredProcCommand(string schema, string procName);

        /// <summary>
        /// Creates an IDBCommand compatible object for a sql command
        /// </summary>
        /// <param name="commandText">the command to execute</param>
        /// <param name="commandType">the type of command being excuted</param>
        /// <returns>The command object for the requested stored procedure</returns>
        IDBCommand CreateCommand(string commandText, CommandType commandType);

        /// <summary>
        /// executes a simple parameratized sql command
        /// </summary>
        /// <param name="commandText">The sql statement to execute</param>
        /// <param name="parameters">The parameters to pass with the command</param>
        void ExecuteCommand(string commandText, IEnumerable<DbParameter> parameters);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sqlCommandBuild"></param>
        /// <param name="sqlCommand"></param>
        /// <param name="dbcode"></param>
        /// <param name="schema"></param>
        /// <param name="procname"></param>
        /// <param name="retries"></param>
        /// <returns></returns>
        int ResilentExecuteNonQuery(Action<IDBCommand> sqlCommandBuild, Action<IDBCommand> sqlCommand, string schema, string procname, int retries = 3);
        
        /// <summary>
        /// 
        /// </summary>
        /// <param name="sqlCommandBuild"></param>
        /// <param name="reader"></param>
        /// <param name="dbcode"></param>
        /// <param name="schema"></param>
        /// <param name="procname"></param>
        /// <param name="retries"></param>
        void ResilentExecuteReader(Action<IDBCommand> sqlCommandBuild, Action<IDataReader> reader, string schema, string procname, int retries = 3);


        /// <summary>
        /// Initializes the database connection
        /// </summary>
        /// <param name="dbCode">the short name of the database connection string</param>
        /// <param name="connectionString">the connection string used to connect to the database</param>
        void Init(string dbCode, string connectionString);

        /// <summary>
        /// Aborts current database transaction
        /// </summary>
        void Rollback();

        #endregion Methods
    }
}