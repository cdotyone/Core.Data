#region Copyright / Comments

// <copyright file="SqlDBConnection.cs" company="Civic Engineering & IT">Copyright © Civic Engineering & IT 2013</copyright>
// <author>Chris Doty</author>
// <email>dotyc@civicinc.com</email>
// <date>6/4/2013</date>
// <summary></summary>

#endregion Copyright / Comments

#region References

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using Civic.Core.Logging;

#endregion References

namespace Civic.Core.Data
{
    /// <summary>
    /// Provides an IDBConnection class for SQL Servers
    /// </summary>
    public class SqlDBConnection : IDBConnection
    {
        #region Fields

        private static readonly Hashtable _paramcache = Hashtable.Synchronized(new Hashtable());
        private int _commandTimeout = 30;       // timout for commands
        private string _connectionString;       // connection string
        private string _dbcode = "NONE";        // database code assigned to connection string

        private readonly Dictionary<string, DbParameter> _paramDefault = new Dictionary<string, DbParameter>();
        private readonly List<string> _persistDefault = new List<string>();
        private SqlTransaction _transaction;    // open sql transaction

        #endregion Fields

        #region Constructors

        /// <summary>
        /// constructor - also adds three default parameters
        /// </summary>
        public SqlDBConnection()
        {
            AddDefaultParameter( CreateParameter( "@computerName", Environment.MachineName ) , false );
            AddDefaultParameter( CreateParameter( "@wasError", false ) , false );
            AddDefaultParameter( CreateParameter( "@modifiedBy", 0 ) , false );
        }

        /// <summary>
        /// constructor - also adds three default parameters
        /// </summary>
        public SqlDBConnection(string connectionString) : this()
        {
            _connectionString = connectionString;
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// get/set the how long it takes a query to timeout once executed
        /// </summary>
        public int CommandTimeout
        {
            get { return _commandTimeout; }
            set { _commandTimeout = value; }
        }

        /// <summary>
        /// set the connection string to be used when executing sql commands
        /// </summary>
        public string ConnectionString
        {
            set
            {
                _connectionString = value;
            }
        }

        /// <summary>
        /// get/sets the short code for the database connection
        /// </summary>
        public string DBCode
        {
            get { return _dbcode; }
            set { _dbcode = value; }
        }

        /// <summary>
        /// gets a list of parameters that will be used to be used as default values for defined parameters that do not ahave a value
        /// </summary>
        public DbParameter[] DefaultParams
        {
            get {
                return _paramDefault.Keys.Select(key => _paramDefault[key]).ToArray();
            }
        }

        #endregion Properties

        #region Methods

        public void AddDefaultParameter( DbParameter param, bool canBeCached )
        {
            addDefaultParameter( param, false, true );
        }

        public void AddDefaultParameter( DbParameter param )
        {
            addDefaultParameter( param, false, true );
        }

        public void AddDefaultParameter( string name, object value )
        {
            AddDefaultParameter( CreateParameter( name, value ), false );
        }

        public void AddDefaultParameter( string name, object value, bool canBeCached )
        {
            addDefaultParameter( CreateParameter( name, value ), canBeCached, false );
        }

        public void BeginTrans()
        {
            if (_transaction != null) return;
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            _transaction = connection.BeginTransaction();
        }

        public IDBConnection Clone()
        {
            var newConn = new SqlDBConnection();

            foreach ( string key in _paramDefault.Keys )
            {
                if(_persistDefault.Contains(key))
                    newConn.AddDefaultParameter( _paramDefault[key], true );
            }

            newConn._dbcode = _dbcode;
            newConn._connectionString = _connectionString;
            newConn._connectionString = _connectionString;

            return newConn;
        }

        public void Commit()
        {
            if (_transaction != null)
                _transaction.Commit();
            else throw new Exception("commit without begin transaction");
            _transaction = null;
        }

        /// <summary>
        /// Creates a DbParameter for the underlying database access layer
        /// </summary>
        /// <param name="name">Name of the parameter to create</param>
        /// <param name="direction">the direction the parameter is meant for in/out</param>
        /// <param name="value">the value for the new parameter</param>
        /// <returns>a DbParameter representing the requested parameter</returns>
        public DbParameter CreateParameter(string name, ParameterDirection direction, object value)
        {
            name = name.Trim();
            if (!name.StartsWith("@")) name = "@" + name;

            var param = new SqlParameter(name, value) {Direction = direction};
            return param;
        }

        /// <summary>
        /// Creates an input only DbParameter for the underlying database access layer
        /// </summary>
        /// <param name="name">Name of the parameter to create</param>
        /// <param name="value">the value for the new parameter</param>
        /// <returns>a DbParameter representing the requested parameter</returns>
        public DbParameter CreateParameter(string name, object value)
        {
            return CreateParameter(name, ParameterDirection.Input, value);
        }

        /// <summary>
        /// Creates a return parameter for this database
        /// </summary>
        /// <returns>a DbParameter representing the requested parameter</returns>
        public DbParameter CreateReturnParameter()
        {
            return new SqlParameter("@RETURN_VALUE", 0) {Direction = ParameterDirection.ReturnValue};        }

        public IDBCommand CreateStoredProcCommand(string schemaName, string procName)
        {
            return new DBCommand(this, schemaName, procName);
        }

        //public int ExecuteCommand( string commandText, params object[] parameterValues )
        //{
        //    //create a command and prepare it for execution
        //    var cmd = new SqlCommand {CommandTimeout = CommandTimeout};

        //    if ( _transaction != null ) { cmd.Connection = _transaction.Connection; cmd.Transaction = _transaction; }
        //    else cmd.Connection = new SqlConnection( _connectionString );
        //    cmd.Connection.Open();

        //    cmd.CommandType = CommandType.Text;
        //    cmd.CommandText = commandText;

        //    foreach ( object obj in parameterValues )
        //    {
        //        if ( obj is DbParameter )
        //        {
        //            var param = (DbParameter)obj;
        //            cmd.Parameters.AddWithValue( param.ParameterName.Replace( "@", "" ), param.Value );
        //        }
        //        else
        //        {
        //            cmd.Parameters.Add( parameterValues );
        //        }
        //    }

        //    int retval = cmd.ExecuteNonQuery();

        //    if ( _transaction == null ) cmd.Connection.Close();
        //    if ( _getReturnValue ) _sqldbReturnValue = (int)cmd.Parameters[0].Value;

        //    return retval;
        //}

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns no resultset) against the database specified in 
        /// the connection string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int result = ExecuteNonQuery(connString, "PublishOrders", 24, 36);
        /// </remarks>
        /// <param name="schemaName">The schema the store procedure belongs to</param>
        /// <param name="spName">the name of the stored prcedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an int representing the number of rows affected by the command, returns -1 when there is an error</returns>
        public int ExecuteNonQuery(string schemaName, string spName, params object[] parameterValues)
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand {CommandTimeout = CommandTimeout};
            var lastSql = string.Empty;
            int retval = -1;

            try
            {
                using (Logger.CreateTrace(LoggingBoundaries.Database, "ExecuteReader", schemaName, spName))
                {

                    if (_transaction != null)
                    {
                        cmd.Connection = _transaction.Connection;
                        cmd.Transaction = _transaction;
                    }
                    else cmd.Connection = new SqlConnection(_connectionString);

                    //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                    var commandParameters = new List<SqlParameter>(getSpParameters(schemaName, spName))
                        {
                            new SqlParameter("@RETURN_VALUE", 0) {Direction = ParameterDirection.ReturnValue}
                        };

                    //assign the provided values to these parameters based on parameter order
                    lastSql = prepareCommand(cmd, CommandType.StoredProcedure, schemaName, spName, commandParameters.ToArray(),parameterValues);
                    commandParameters.Clear();
                    Logger.LogTrace(LoggingBoundaries.Database, "ExecuteNonQuery Called:\n{0}", lastSql);

                    retval = cmd.ExecuteNonQuery();

                    if (_transaction == null) cmd.Connection.Close();

                    return retval;
                }
            }
            catch (SqlException ex)
            {
                cmd.Connection = null;
                if (Logger.HandleException(LoggingBoundaries.Database, ex))
                    throw new SqlDBException(ex, cmd, lastSql);
            }

            return retval;
        }

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns a resultset) against the database specified in 
        /// the connection string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  SqlDataReader dr = ExecuteReader("GetOrders", 24, 36);
        /// </remarks>
        /// <param name="schemaName">The schema the store procedure belongs to</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public IDataReader ExecuteReader(string schemaName, string spName, params object[] parameterValues)
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand { CommandTimeout = CommandTimeout };
            var lastSql = string.Empty;

            try
            {
                using (Logger.CreateTrace(LoggingBoundaries.Database, "ExecuteReader", schemaName, spName))
                {
                    if (_transaction != null)
                    {
                        cmd.Connection = _transaction.Connection;
                        cmd.Transaction = _transaction;
                    }
                    else cmd.Connection = new SqlConnection(_connectionString);

                    //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                    SqlParameter[] commandParameters = getSpParameters(schemaName, spName);

                    //assign the provided values to these parameters based on parameter order
                    lastSql = prepareCommand(cmd, CommandType.StoredProcedure, schemaName, spName, commandParameters, parameterValues);
                    Logger.LogTrace(LoggingBoundaries.Database, "Execute Reader Called:\n{0}", lastSql);

                    // call ExecuteReader with the appropriate CommandBehavior
                    SqlDataReader dr = _transaction != null ? cmd.ExecuteReader() : cmd.ExecuteReader(CommandBehavior.CloseConnection);

                    return dr;
                }
            }
            catch (SqlException ex)
            {
                cmd.Connection = null;
                if(Logger.HandleException(LoggingBoundaries.Database, ex))
                    throw new SqlDBException(ex, cmd, lastSql);
            }
            return null;
        }

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns a 1x1 resultset) against the database specified in 
        /// the connection string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar(connString, "GetOrderCount", 24, 36);
        /// </remarks>
        /// <param name="schemaName">The schema the store procedure belongs to</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public object ExecuteScalar(string schemaName, string spName, params object[] parameterValues)
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand {CommandTimeout = CommandTimeout};
            var lastSql = string.Empty;

            try
            {
                using (Logger.CreateTrace(LoggingBoundaries.Database, "ExecuteScalar", schemaName, spName))
                {

                    if (_transaction != null)
                    {
                        cmd.Connection = _transaction.Connection;
                        cmd.Transaction = _transaction;
                    }
                    else cmd.Connection = new SqlConnection(_connectionString);

                    //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                    SqlParameter[] commandParameters = getSpParameters(schemaName, spName);

                    //assign the provided values to these parameters based on parameter order
                    lastSql = prepareCommand(cmd, CommandType.StoredProcedure, schemaName, spName, commandParameters, parameterValues);

                    //execute the command & return the results
                    object retval = cmd.ExecuteScalar();
                    Logger.LogTrace(LoggingBoundaries.Database, "ExecuteScalar Called:\n{0}", lastSql);

                    if (_transaction == null) cmd.Connection.Close();

                    return retval;
                }
            }
            catch (SqlException ex)
            {
                cmd.Connection = null;
                if (Logger.HandleException(LoggingBoundaries.Database, ex))
                    throw new SqlDBException(ex, cmd, lastSql);
            }
            return null;
        }

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns a resultset) against the database specified in 
        /// the connection string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  SqlDataReader dr = ExecuteReader("GetOrders", 24, 36);
        /// </remarks>
        /// <param name="schemaName">The schema the store procedure belongs to</param>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public IDataReader ExecuteSequentialReader(string schemaName, string spName, params object[] parameterValues)
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand {CommandTimeout = CommandTimeout};
            var lastSql = string.Empty;

            try
            {
                using (Logger.CreateTrace(LoggingBoundaries.Database, "ExecuteSequentialReader", schemaName, spName))
                {

                    if (_transaction != null)
                    {
                        cmd.Connection = _transaction.Connection;
                        cmd.Transaction = _transaction;
                    }
                    else cmd.Connection = new SqlConnection(_connectionString);

                    //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                    SqlParameter[] commandParameters = getSpParameters(schemaName, spName);

                    //assign the provided values to these parameters based on parameter order
                    lastSql = prepareCommand(cmd, CommandType.StoredProcedure, schemaName, spName, commandParameters, parameterValues);

                    // call ExecuteReader with the appropriate CommandBehavior
                    SqlDataReader dr = cmd.ExecuteReader(CommandBehavior.SequentialAccess);
                    Logger.LogTrace(LoggingBoundaries.Database, "ExecuteSequentialReader Called:\n{0}", lastSql);

                    return dr;
                }
            }
            catch (SqlException ex)
            {
                cmd.Connection = null;
                if (Logger.HandleException(LoggingBoundaries.Database, ex))
                    throw new SqlDBException(ex, cmd, lastSql);
            }
            return null;
        }

        /// <summary>
        /// Initializes the database connection
        /// </summary>
        /// <param name="dbCode">the short name of the database connection string</param>
        /// <param name="connectionString">the connection string used to connect to the database</param>
        public void Init(string dbCode, string connectionString)
        {
            _dbcode = dbCode;
            _connectionString = connectionString;
        }

        /// <summary>
        /// Aborts current database transaction
        /// </summary>
        public void Rollback()
        {
            if (_transaction != null)
                _transaction.Rollback();
            else throw new Exception("rollback without begin transaction");
            _transaction = null;
        }

        /// <summary>
        /// Adds a default parameter when executing store procedures
        /// </summary>
        /// <param name="param">the database parameter</param>
        /// <param name="canBeCached">can the parameter be used when caching the result set</param>
        /// <param name="makeCopy">should it use the param, or make a copy of it</param>
        private void addDefaultParameter( DbParameter param, bool canBeCached, bool makeCopy )
        {
            string pname = param.ParameterName.ToLower();

            if ( canBeCached ) _persistDefault.Add( pname );

            if ( makeCopy )
            {
                DbParameter newParam = CreateParameter( param.ParameterName, param.Direction, param.Value );
                _paramDefault[pname] = newParam;
            }
            else _paramDefault[pname] = param;
        }

        /// <summary>
        /// This method assigns an array of values to an array of SqlParameters.
        /// </summary>
        private SqlParameter _assignParameter(SqlParameter commandParameter, SqlParameter setToParameter)
        {
            if (setToParameter.Value == null && setToParameter.Direction==ParameterDirection.Input)
                commandParameter.Value = DBNull.Value;
            else
            {
                if (commandParameter.DbType != setToParameter.DbType)
                {
                    switch (commandParameter.DbType.ToString())
                    {
                        case "Guid":
                            if (setToParameter.Value != null)
                                commandParameter.Value = new Guid(setToParameter.Value.ToString());
                            break;
                        case "Boolean":
                            if (setToParameter.Value != null)
                                if (setToParameter.Value.ToString() == "0" || setToParameter.ToString().ToLower() == "false")
                                    commandParameter.Value = false;
                                else
                                    commandParameter.Value = true;
                            break;
                        default:
                            if (setToParameter.Value != null) commandParameter.Value = setToParameter.Value.ToString();
                            break;
                    }
                }
                else
                {
                    if (setToParameter.Direction != ParameterDirection.Input)
                    {
                        setToParameter.Size = commandParameter.Size;
                        setToParameter.Scale = commandParameter.Scale;
                    }
                    return setToParameter;
                }
            }

            return commandParameter;
        }

        /// <summary>
        /// This method assigns an array of values to an array of SqlParameters.
        /// </summary>
        /// <param name="command">command object to ass parameters to</param>
        /// <param name="commandParameters">array of SqlParameters to be assigned values</param>
        /// <param name="parameterValues">array of objects holding the values to be assigned</param>
        private void assignParameterValues(SqlCommand command, SqlParameter[] commandParameters, object[] parameterValues)
        {
            if (commandParameters == null)
            {
                //do nothing if we get no data
                return;
            }

            for (int i = 0, j = commandParameters.Length; i < j; i++)
            {
                string pname = commandParameters[i].ParameterName.ToLower();

                bool bFound = false;
                if (parameterValues != null)
                {
                    for (int m = 0, n = parameterValues.Length; m < n; m++)
                    {
                        if ( pname == ( (SqlParameter)parameterValues[m] ).ParameterName.ToLower() )
                        {
                            commandParameters[i] = _assignParameter(commandParameters[i], ((SqlParameter)parameterValues[m]));
                            bFound = true;
                            break;
                        }
                    }
                }

                if (!bFound)
                {
                    if ( _paramDefault.ContainsKey( pname ) )
                    {
                        commandParameters[i] = _assignParameter( commandParameters[i], _cloneParameter( (SqlParameter)_paramDefault[pname], true ));
                    }
                }
            }

            foreach (SqlParameter p in commandParameters)
            {
                if ((p.Direction == ParameterDirection.InputOutput) && (p.Value == null))
                {
                    p.Value = DBNull.Value;
                }
                command.Parameters.Add(p);
            }
        }

        /// <summary>
        /// Deep copy of cached SqlParameter array
        /// </summary>
        private SqlParameter _cloneParameter( SqlParameter oldparam, bool copyValue )
        {
            var newparam = new SqlParameter
                               {
                                   ParameterName = oldparam.ParameterName,
                                   Value = copyValue ? oldparam.Value : DBNull.Value,
                                   DbType = oldparam.DbType,
                                   Size = oldparam.Size,
                                   Direction = oldparam.Direction,
                                   Precision = oldparam.Precision,
                                   Scale = oldparam.Scale
                               };

            return newparam;
        }

        /// <summary>
        /// Deep copy of cached SqlParameter array
        /// </summary>
        /// <param name="originalParameters"></param>
        /// <returns></returns>
        private SqlParameter[] cloneParameters(SqlParameterCollection originalParameters)
        {
            var clonedParameters = new SqlParameter[originalParameters.Count - 1];

            int cnt = originalParameters.Count;
            for (int i = 1, j = 0; i < cnt; i++)
            {
                clonedParameters[j++] = _cloneParameter(originalParameters[i], false);
            }

            return clonedParameters;
        }

        /// <summary>
        /// Resolve at run time the appropriate set of SqlParameters for a stored procedure
        /// </summary>
        /// <param name="schemaName">the schema the stored procedure belongs to</param>
        /// <param name="spName">The name of the stored procedure</param>
        /// <returns>The parameter collection discovered.</returns>
        private SqlParameterCollection discoverSpParameterSet(string schemaName, string spName)
        {
            if (string.IsNullOrEmpty(spName)) throw new ArgumentNullException("spName");

            var connection = new SqlConnection(_connectionString);

            string[] parts = spName.Split('.');
            if ( parts.Length < 2 ) parts = new [] { schemaName, spName };
            if ( parts[1].IndexOf( '[' ) < 0 ) parts[1] = '[' + parts[1] + ']';
            spName = parts[0] + '.' + parts[1];

            var cmd = new SqlCommand(spName, connection) {CommandType = CommandType.StoredProcedure};

            connection.Open();
            SqlCommandBuilder.DeriveParameters(cmd);
            connection.Close();

            return cmd.Parameters;
        }

        /// <summary>
        /// Retrieves the set of SqlParameters appropriate for the stored procedure
        /// </summary>
        /// <param name="schemaName">the schema the store procedure belongs to</param>
        /// <param name="spName">The name of the stored procedure</param>
        /// <returns>An array of SqlParameters</returns>
        private SqlParameter[] getSpParameters(string schemaName, string spName)
        {
            if (string.IsNullOrEmpty(spName)) throw new ArgumentNullException("spName");

            string key = _dbcode + ":" + spName;

            var cachedParameters = (SqlParameterCollection)_paramcache[key];
            if (cachedParameters == null)
            {
                SqlParameterCollection spParameters = discoverSpParameterSet(schemaName, spName);
                _paramcache[key] = spParameters;
                cachedParameters = spParameters;
            }

            return cloneParameters(cachedParameters);
        }

        /// <summary>
        /// This method opens (if necessary) and assigns a connection, transaction, command type and parameters 
        /// to the provided command.
        /// </summary>
        /// <param name="command">the SqlCommand to be prepared</param>
        /// <param name="commandType">the CommandType (stored procedure, text, etc.)</param>
        /// <param name="schemaName">the scehma name of the store procedure etc</param>
        /// <param name="commandText">the stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">an array of SqlParameters to be associated with the command or 'null' if no parameters are required</param>
        /// <param name="parameterValues">an array of sqlparameter values to assign to the commandParameters</param>
        /// <returns>the last command executed</returns>
        private string prepareCommand(SqlCommand command, CommandType commandType, string schemaName, string commandText, SqlParameter[] commandParameters, object[] parameterValues)
        {
            //if we were provided a transaction, assign it.
            if (_transaction != null)
            {
                command.Connection = _transaction.Connection;
                command.Transaction = _transaction;
            }
            else
            {
                //associate the connection with the command
                command.Connection = new SqlConnection(_connectionString);
                command.Connection.Open();
            }

            //set the command text (stored procedure name or SQL statement)
            string[] parts = commandText.Split( '.' );
            if (parts.Length < 2) parts = new[] { schemaName, commandText };
            if ( parts[1].IndexOf( '[' ) < 0 ) parts[1] = '[' + parts[1] + ']';
            command.CommandText = parts[0] + '.' + parts[1];

            //set the command type
            command.CommandType = commandType;

            //attach the command parameters if they are provided
            assignParameterValues(command, commandParameters, parameterValues);

            var sb = new StringBuilder();
            sb.AppendLine("exec " + command.CommandText);
            int i = 0;
            foreach (SqlParameter sqlp in command.Parameters)
            {
                sb.Append(i > 0 ? "\t," : "\t");

                if (sqlp.Value == DBNull.Value)
                    sb.AppendLine(sqlp.ParameterName + "=NULL");
                else
                {
                    switch (sqlp.DbType.ToString())
                    {
                        case "Int":
                            sb.AppendLine(sqlp.ParameterName + "=" + sqlp.Value);
                            break;
                        default:
                            sb.AppendLine(sqlp.ParameterName + "='" + sqlp.Value + "'");
                            break;
                    }
                }
                i++;
            }

            return sb.ToString();
        }

        #endregion Methods
    }
}