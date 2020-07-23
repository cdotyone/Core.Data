#region References

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using Core.Configuration;
using Core.Logging;
using Core.Logging.Configuration;
using Core.Security;

#endregion References

namespace Core.Data
{
    /// <summary>
    /// Provides an IDBConnection class for SQL Servers
    /// </summary>
    public class SqlDBConnection : IDBConnection
    {
        #region Fields

        private static readonly Hashtable _parameterCache = Hashtable.Synchronized(new Hashtable());
        private string _dbcode = "NONE";        // database code assigned to connection string

        private readonly Dictionary<string, DbParameter> _paramDefault = new Dictionary<string, DbParameter>();
        private readonly List<string> _persistDefault = new List<string>();

        private SqlConnection _connection;      // sql connection if there is one

        #endregion Fields

        #region Constructors

        /// <summary>
        /// constructor - also adds three default parameters
        /// </summary>
        public SqlDBConnection()
        {
            AddDefaults();
        }

        private void AddDefaults()
        {
            AddDefaultParameter(CreateParameter("@computerName", Environment.MachineName), false);
            AddDefaultParameter("@environmentCode", LoggingConfig.Current.EnvironmentCode);
            AddDefaultParameter("@clientCode", LoggingConfig.Current.ClientCode);
            AddDefaultParameter("@moduleCode", LoggingConfig.Current.ApplicationName);
            AddDefaultParameter(CreateParameter("@wasError", false), false);
        }

        public IDBConnection AddClaimsDefaults(ClaimsPrincipal claimsPrincipal)
        {
            var defaults = StandardClaimTypes.GetClaimsDefaultForDataConfig(DataConfig.Current);
            foreach (var claim in defaults)
            {
                AddDefaultParameter(CreateParameter(claim.Key, IdentityManager.GetClaimValue(claimsPrincipal, claim.Value)), false);
            }

            return this;
        }

        /// <summary>
        /// constructor - also adds three default parameters
        /// </summary>
        public SqlDBConnection(string connectionString)
            : this()
        {
            AddDefaults();
            ConnectionString = connectionString;
        }

        #endregion Constructors

        #region Properties

        /// <summary>
        /// get/set the how long it takes a query to timeout once executed
        /// </summary>
        public int CommandTimeout { get; set; } = 30;

        /// <summary>
        /// set the connection string to be used when executing sql commands
        /// </summary>
        public string ConnectionString { set; get; }

        public SqlTransaction Transaction { get; private set; }

        /// <summary>
        /// get/sets if the last sql string executed
        /// </summary>
        public string LastSql { get; set; }

        /// <summary>
        /// get/sets the short code for the database connection
        /// </summary>
        public string DBCode
        {
            get => _dbcode;
            set => _dbcode = value;
        }

        /// <summary>
        /// gets a list of parameters that will be used to be used as default values for defined parameters that do not a have a value
        /// </summary>
        public DbParameter[] DefaultParams
        {
            get
            {
                return _paramDefault.Keys.Select(key => _paramDefault[key]).ToArray();
            }
        }

        #endregion Properties

        #region Methods

        public void AddDefaultParameter(DbParameter param, bool canBeCached)
        {
            addDefaultParameter(param, false, true);
        }

        public void AddDefaultParameter(DbParameter param)
        {
            addDefaultParameter(param, false, true);
        }

        public void AddDefaultParameter(string name, object value)
        {
            AddDefaultParameter(CreateParameter(name, value), false);
        }

        public void AddDefaultParameter(string name, object value, bool canBeCached)
        {
            addDefaultParameter(CreateParameter(name, value), canBeCached, false);
        }

        public void BeginTrans()
        {
            if (Transaction != null) return;
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            Transaction = connection.BeginTransaction();
            _connection = connection;
        }

        public void BeginTrans(bool allowDirty)
        {
            if (Transaction != null) return;
            var connection = new SqlConnection(ConnectionString);
            connection.Open();
            Transaction = connection.BeginTransaction(allowDirty ? IsolationLevel.ReadUncommitted : IsolationLevel.ReadCommitted);
            _connection = connection;
        }

        public bool IsInTransaction => Transaction != null;

        public IDBConnection Clone()
        {
            var newConn = new SqlDBConnection();

            foreach (string key in _paramDefault.Keys)
            {
                if (_persistDefault.Contains(key))
                    newConn.AddDefaultParameter(_paramDefault[key], true);
            }

            newConn._dbcode = _dbcode;
            newConn.ConnectionString = ConnectionString;
            newConn.ConnectionString = ConnectionString;

            return newConn;
        }

        /// <summary>
        /// Commit the transaction
        /// </summary>
        public void Commit()
        {
            if (Transaction != null)
                Transaction.Commit();
            else throw new Exception("commit without begin transaction");
            Transaction = null;
        }

        /// <summary>
        /// Close the connection if not already closed
        /// </summary>
        public void Close()
        {
            _connection?.Close();
            _connection = null;
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

            var param = new SqlParameter(name, value) { Direction = direction };
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
            return new SqlParameter("@RETURN_VALUE", 0) { Direction = ParameterDirection.ReturnValue };
        }

        /// <summary>
        /// Creates an IDBCommand compatible object for the requested stored procedure
        /// </summary>
        /// <param name="schemaName">the schema name of the store procedure</param>
        /// <param name="procName">the name of the stored procedure to request the stored procedure for</param>
        /// <returns>The command object for the requested stored procedure</returns>
        public IDbCommand CreateStoredProcCommand(string schemaName, string procName)
        {
            schemaName = DataConfig.Current.GetSchemaName(schemaName);
            return new DBCommand(this, schemaName, procName);
        }

        /// <summary>
        /// Creates an IDBCommand compatible object for a sql command
        /// </summary>
        /// <param name="commandText">the command to execute</param>
        /// <param name="commandType">the type of command being executed</param>
        /// <returns>The command object for the requested stored procedure</returns>
        public IDbCommand CreateCommand(string commandText, CommandType commandType)
        {
            return new DBCommand(this, commandText, commandType);
        }


        public int ResilientExecuteNonQuery(Action<IDbCommand> sqlCommandBuild, Action<IDbCommand> sqlCommand, string schema, string procedureName, int retries = 3)
        {
            Exception lastException = null;

            while (retries > 0)
            {
                try
                {
                    using (var database = new SqlDBConnection(ConnectionString))
                    {
                        using (var command = database.CreateStoredProcCommand(schema, procedureName))
                        {
                            sqlCommandBuild(command);
                            var returnValue = command.ExecuteNonQuery();
                            sqlCommand(command);
                            return returnValue;
                        }
                    }
                }
                catch (Exception ex)
                {
                    retries--;
                    Logger.HandleException(LoggingBoundaries.DataLayer, ex);
                    lastException = ex;
                    Thread.Sleep(100);
                }
            }

            if (lastException != null) throw lastException;
            return -1;
        }


        public void ResilientExecuteReader(Action<IDbCommand> sqlCommandBuild, Action<IDataReader> reader, string schema, string procedureName, int retries = 3)
        {
            Exception lastException = null;

            while (retries > 0)
            {
                try
                {
                    using (var database = new SqlDBConnection(ConnectionString))
                    {
                        using (var command = database.CreateStoredProcCommand(schema, procedureName))
                        {
                            sqlCommandBuild(command);
                            command.ExecuteReader(reader);
                            return;
                        }
                    }
                }
                catch (Exception ex)
                {
                    retries--;
                    Logger.HandleException(LoggingBoundaries.DataLayer, ex);
                    lastException = ex;
                    Thread.Sleep(100);
                }
            }

            if (lastException != null) throw lastException;
        }


        /// <summary>
        /// executes a simple parameterized sql command
        /// </summary>
        /// <param name="commandText">The sql statement to execute</param>
        /// <param name="parameters">The parameters to pass with the command</param>
        public void ExecuteCommand(string commandText, IEnumerable<DbParameter> parameters)
        {
            using (var dbCommand = CreateCommand(commandText, CommandType.Text))
            {
                if (parameters != null)
                {
                    foreach (var dbParameter in parameters)
                    {
                        dbCommand.AddParameter(dbParameter);
                    }
                }
                dbCommand.ExecuteNonQuery();
            }
        }


        public void SetCommandConnection(SqlCommand cmd)
        {
            if (Transaction != null)
            {
                cmd.Connection = _connection;
                cmd.Transaction = Transaction;
            }
            else
            {
                if (_connection == null) _connection = new SqlConnection(ConnectionString);
                cmd.Connection = _connection;
                if (_connection.State!=ConnectionState.Open)
                    _connection.Open();
            }
        }

        /// <summary>
        /// Initializes the database connection
        /// </summary>
        /// <param name="dbCode">the short name of the database connection string</param>
        /// <param name="connectionString">the connection string used to connect to the database</param>
        public void Init(string dbCode, string connectionString)
        {
            _dbcode = dbCode;
            ConnectionString = connectionString;
        }

        /// <summary>
        /// Aborts current database transaction
        /// </summary>
        public void Rollback()
        {
            if (Transaction != null)
                Transaction.Rollback();
            else throw new Exception("rollback without begin transaction");
            Transaction = null;
        }

        /// <summary>
        /// Adds a default parameter when executing store procedures
        /// </summary>
        /// <param name="param">the database parameter</param>
        /// <param name="canBeCached">can the parameter be used when caching the result set</param>
        /// <param name="makeCopy">should it use the param, or make a copy of it</param>
        private void addDefaultParameter(DbParameter param, bool canBeCached, bool makeCopy)
        {
            string name = param.ParameterName.ToLower();

            if (canBeCached) _persistDefault.Add(name);

            if (makeCopy)
            {
                DbParameter newParam = CreateParameter(param.ParameterName, param.Direction, param.Value);
                _paramDefault[name] = newParam;
            }
            else _paramDefault[name] = param;
        }

        /// <summary>
        /// This method assigns an array of values to an array of SqlParameters.
        /// </summary>
        private SqlParameter _assignParameter(SqlParameter commandParameter, SqlParameter setToParameter)
        {
            if (setToParameter.Value == null && setToParameter.Direction == ParameterDirection.Input)
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
                            if (setToParameter.DbType == DbType.String)
                            {
                                if (setToParameter.Value != null)
                                {
                                    commandParameter.Value = setToParameter.Value.ToString().ToLower() == "true" ? "Y":"N";
                                }
                            }

                            if (setToParameter.Value != null)
                                if (setToParameter.Value.ToString() == "0" || setToParameter.Value.ToString().ToLower() == "false")
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
                        if (pname == ((SqlParameter)parameterValues[m]).ParameterName.ToLower())
                        {
                            commandParameters[i] = _assignParameter(commandParameters[i], ((SqlParameter)parameterValues[m]));
                            bFound = true;
                            break;
                        }
                    }
                }

                if (!bFound)
                {
                    if (_paramDefault.ContainsKey(pname))
                    {
                        commandParameters[i] = _assignParameter(commandParameters[i], _cloneParameter((SqlParameter)_paramDefault[pname], true));
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
        private SqlParameter _cloneParameter(SqlParameter oldParameter, bool copyValue)
        {
            var newparam = new SqlParameter
            {
                ParameterName = oldParameter.ParameterName,
                Value = copyValue ? oldParameter.Value : DBNull.Value,
                DbType = oldParameter.DbType,
                Size = oldParameter.Size,
                Direction = oldParameter.Direction,
                Precision = oldParameter.Precision,
                Scale = oldParameter.Scale
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
        /// <param name="procedureName">The name of the stored procedure</param>
        /// <returns>The parameter collection discovered.</returns>
        private SqlParameterCollection discoverParameters(string schemaName, string procedureName)
        {
            if (string.IsNullOrEmpty(procedureName)) throw new ArgumentNullException(nameof(procedureName));

            string[] parts = procedureName.Split('.');
            if (parts.Length < 2) parts = new[] { schemaName, procedureName };
            if (parts[1].IndexOf('[') < 0) parts[1] = '[' + parts[1] + ']';
            procedureName = parts[0] + '.' + parts[1];

            SqlCommand cmd;
            using (var connection = new SqlConnection(ConnectionString))
            {
                connection.Open();
                cmd = new SqlCommand(procedureName, connection) { CommandType = CommandType.StoredProcedure };
                SqlCommandBuilder.DeriveParameters(cmd);               
            }

            return cmd.Parameters;
        }

        /// <summary>
        /// Retrieves the set of SqlParameters appropriate for the stored procedure
        /// </summary>
        /// <param name="schemaName">the schema the store procedure belongs to</param>
        /// <param name="procedureName">The name of the stored procedure</param>
        /// <returns>An array of SqlParameters</returns>
        internal SqlParameter[] GetParameters(string schemaName, string procedureName)
        {
            if (string.IsNullOrEmpty(procedureName)) throw new ArgumentNullException(nameof(procedureName));

            string key = _dbcode + ":" + schemaName + ":" + procedureName;
            key = key.ToLowerInvariant();

            var cachedParameters = (SqlParameterCollection)_parameterCache[key];
            if (cachedParameters == null)
            {
                SqlParameterCollection spParameters = discoverParameters(schemaName, procedureName);
                _parameterCache[key] = spParameters;
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
        /// <param name="schemaName">the schema name of the store procedure etc</param>
        /// <param name="commandText">the stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">an array of SqlParameters to be associated with the command or 'null' if no parameters are required</param>
        /// <param name="parameterValues">an array of sql parameter values to assign to the commandParameters</param>
        /// <returns>the last command executed</returns>
        internal string PrepareCommand(SqlCommand command, CommandType commandType, string schemaName, string commandText, SqlParameter[] commandParameters, object[] parameterValues)
        {
            //set the command text (stored procedure name or SQL statement)
            string[] parts = commandText.Split('.');
            if (parts.Length < 2) parts = new[] { schemaName, commandText };
            if (parts[1].IndexOf('[') < 0) parts[1] = '[' + parts[1] + ']';
            command.CommandText = parts[0] + '.' + parts[1];

            //set the command type
            command.CommandType = commandType;

            //attach the command parameters if they are provided
            assignParameterValues(command, commandParameters, parameterValues);

            var sb = new StringBuilder();
            sb.AppendLine("exec " + command.CommandText);
            int i = 0;
            foreach (SqlParameter parameter in command.Parameters)
            {
                sb.Append(i > 0 ? "\t," : "\t");

                if (parameter.Value == DBNull.Value)
                    sb.AppendLine(parameter.ParameterName + "=NULL");
                else
                {
                    switch (parameter.DbType.ToString())
                    {
                        case "Int":
                            sb.AppendLine(parameter.ParameterName + "=" + parameter.Value);
                            break;
                        default:
                            sb.AppendLine(parameter.ParameterName + "='" + parameter.Value + "'");
                            break;
                    }
                }
                i++;
            }

            return sb.ToString();
        }

        public void Dispose()
        {
            if (Transaction == null) Close();
        }

        #endregion Methods
    }
}