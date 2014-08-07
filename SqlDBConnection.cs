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

        private SqlConnection _connection;      // sql connection if there is one

        #endregion Fields

        #region Constructors

        /// <summary>
        /// constructor - also adds three default parameters
        /// </summary>
        public SqlDBConnection()
        {
            AddDefaultParameter(CreateParameter("@computerName", Environment.MachineName), false);
            AddDefaultParameter(CreateParameter("@wasError", false), false);
            AddDefaultParameter(CreateParameter("@modifiedBy", 0), false);
        }

        /// <summary>
        /// constructor - also adds three default parameters
        /// </summary>
        public SqlDBConnection(string connectionString)
            : this()
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
            set { _connectionString = value; }
            get { return _connectionString; }
        }

        public SqlTransaction Transaction
        {
            get { return _transaction;  }
        }

        /// <summary>
        /// get/sets if the last sql string executed
        /// </summary>
        public string LastSql { get; set; }

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
            if (_transaction != null) return;
            var connection = new SqlConnection(_connectionString);
            connection.Open();
            _transaction = connection.BeginTransaction();
            _connection = connection;
        }

        public IDBConnection Clone()
        {
            var newConn = new SqlDBConnection();

            foreach (string key in _paramDefault.Keys)
            {
                if (_persistDefault.Contains(key))
                    newConn.AddDefaultParameter(_paramDefault[key], true);
            }

            newConn._dbcode = _dbcode;
            newConn._connectionString = _connectionString;
            newConn._connectionString = _connectionString;

            return newConn;
        }

        /// <summary>
        /// Commit the transaction
        /// </summary>
        public void Commit()
        {
            if (_transaction != null)
                _transaction.Commit();
            else throw new Exception("commit without begin transaction");
            _transaction = null;
        }

        /// <summary>
        /// Close the connection if not already closed
        /// </summary>
        public void Close()
        {
            if (_connection != null)
            {
                _connection.Close();
            }
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
        public IDBCommand CreateStoredProcCommand(string schemaName, string procName)
        {
            return new DBCommand(this, schemaName, procName);
        }

        /// <summary>
        /// Creates an IDBCommand compatible object for a sql command
        /// </summary>
        /// <param name="commandText">the command to execute</param>
        /// <param name="commandType">the type of command being excuted</param>
        /// <returns>The command object for the requested stored procedure</returns>
        public IDBCommand CreateCommand(string commandText, CommandType commandType)
        {
            return new DBCommand(this, commandText, commandType);
        }


        /// <summary>
        /// executes a simple parameratized sql command
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
            if (_transaction != null)
            {
                cmd.Connection = _connection;
                cmd.Transaction = _transaction;
            }
            else
            {
                if (_connection == null) _connection = new SqlConnection(_connectionString);
                cmd.Connection = _connection;
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
        private void addDefaultParameter(DbParameter param, bool canBeCached, bool makeCopy)
        {
            string pname = param.ParameterName.ToLower();

            if (canBeCached) _persistDefault.Add(pname);

            if (makeCopy)
            {
                DbParameter newParam = CreateParameter(param.ParameterName, param.Direction, param.Value);
                _paramDefault[pname] = newParam;
            }
            else _paramDefault[pname] = param;
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
        private SqlParameter _cloneParameter(SqlParameter oldparam, bool copyValue)
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

            string[] parts = spName.Split('.');
            if (parts.Length < 2) parts = new[] { schemaName, spName };
            if (parts[1].IndexOf('[') < 0) parts[1] = '[' + parts[1] + ']';
            spName = parts[0] + '.' + parts[1];

            SqlCommand cmd;
            using (var connection = new SqlConnection(_connectionString))
            {
                connection.Open();
                cmd = new SqlCommand(spName, connection) { CommandType = CommandType.StoredProcedure };
                SqlCommandBuilder.DeriveParameters(cmd);               
            }

            return cmd.Parameters;
        }

        /// <summary>
        /// Retrieves the set of SqlParameters appropriate for the stored procedure
        /// </summary>
        /// <param name="schemaName">the schema the store procedure belongs to</param>
        /// <param name="spName">The name of the stored procedure</param>
        /// <returns>An array of SqlParameters</returns>
        internal SqlParameter[] GetSpParameters(string schemaName, string spName)
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

        public void Dispose()
        {
            if (_transaction != null) _transaction.Rollback();
            Close();
        }

        #endregion Methods
    }
}