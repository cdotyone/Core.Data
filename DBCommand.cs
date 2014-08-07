#region Copyright / Comments

// <copyright file="DBCommand.cs" company="Civic Engineering & IT">Copyright © Civic Engineering & IT 2013</copyright>
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
using System.Linq;
using Civic.Core.Logging;

#endregion References

// ReSharper disable CoVariantArrayConversion

namespace Civic.Core.Data
{

    /// <summary>
    /// This class is used to construct DBCommands that will be executed against a sql database.
    /// </summary>
    public class DBCommand : IDBCommand
    {
        #region Fields

        private IDBConnection _dbconn;          // the database connections
        private readonly List<DbParameter> _params;      // the parameters to be used when excuting the command
        private readonly string _procname;               // the store procedure name to execute
        private string _schema;                          // the schema name of the procedure/command being executed
        private readonly CommandType _commandType;

        #endregion Fields

        #region Constructors

        /// <summary>
        /// internal constructor for DBCommand
        /// </summary>
        /// <param name="dbconn">the database connection to be used when executing the SQL command</param>
        /// <param name="schemaName">schema name of the storeprocedure</param>
        /// <param name="procname">the name of the stored procedure that will be executed</param>
        internal DBCommand(IDBConnection dbconn, string schemaName, string procname)
        {
            _dbconn = dbconn;
            _procname = procname;
            _schema = schemaName;
            _params = new List<DbParameter>();
            _commandType = CommandType.StoredProcedure;
        }

        internal DBCommand(SqlDBConnection dbconn, string commandText, CommandType commandType)
        {
            _dbconn = dbconn;
            _procname = commandText;
            _params = new List<DbParameter>();
            _commandType = commandType;
        }

        #endregion Constructors

        #region Properties

        /// <summary>`
        /// The database connection
        /// </summary>
        public IDBConnection DBConnection
        {
            get { return _dbconn; }
        }

        public string Schema
        {
            get { return _schema; }
            set { _schema = value; }
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Adds a new In DbParameterobject to the given command.
        /// </summary>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <remarks>
        /// <para>This version of the method is used when you can have the same parameter object multiple times with different values.</para>
        /// </remarks>        
        public void AddInParameter(string name)
        {
            AddParameter(name, ParameterDirection.Input, null);
        }

        /// <summary>
        /// Adds a new In DbParameter object to the given command.
        /// </summary>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="value"><para>The value of the parameter.</para></param>      
        public void AddInParameter(string name, object value)
        {
            AddParameter(name, ParameterDirection.Input, value);
        }

        /// <summary>
        /// Adds a new Out DbParameter object to the given command.
        /// </summary>
        /// <param name="name"><para>The name of the parameter.</para></param>
        public void AddOutParameter(string name)
        {
            AddParameter(name, ParameterDirection.Output, DBNull.Value);
        }

        /// <summary>
        /// Adds a return parameter request
        /// </summary>
        public void AddReturnParameter()
        {
            _params.Add(_dbconn.CreateReturnParameter());
        }

        /// <summary>
        /// Adds a new Out DbParameter object to the given command.
        /// </summary>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="value"><para>The value of the parameter.</para></param>      
        public void AddOutParameter(string name, object value)
        {
            AddParameter(name, ParameterDirection.InputOutput, value);
        }

        /// <summary>
        /// <para>Adds a new instance of a <see cref="DbParameter"/> object to the command.</para>
        /// </summary>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="direction"><para>One of the <see cref="ParameterDirection"/> values.</para></param>                
        /// <param name="value"><para>The value of the parameter.</para></param>    
        public void AddParameter(string name, ParameterDirection direction, object value)
        {
            var val = value as DBNull;

            if (val != null)
                value = null;

            _params.Add(_dbconn.CreateParameter(name, direction, value));
        }

        /// <summary>
        /// Add a parameter directly to the parameter list
        /// </summary>
        /// <param name="parameter">the parameter to add</param>
        public void AddParameter(DbParameter parameter)
        {
            _params.Add(parameter);
        }

        /// <summary>
        /// Clears all of the parameters.  Useful when needing to reuse a command
        /// </summary>
        public void ClearParameters()
        {
            _params.Clear();
        }

        public void Dispose()
        {
            _params.Clear();
            _dbconn = null;
        }

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
        /// <returns>an int representing the number of rows affected by the command, returns -1 when there is an error</returns>
        public int ExecuteNonQuery()
        {
            if (_commandType != CommandType.StoredProcedure)
            {
                return executeCommandNonQuery();
            }

            using (var cmd = new SqlCommand { CommandTimeout = _dbconn.CommandTimeout })
            {
                _dbconn.LastSql = string.Empty;
                int retval = -1;

                try
                {
                    using (Logger.CreateTrace(LoggingBoundaries.Database, "ExecuteReader", _schema, _procname))
                    {
                        var sqlDBConnection = _dbconn as SqlDBConnection;
                        if (sqlDBConnection == null) return -1;
                        sqlDBConnection.SetCommandConnection(cmd);

                        //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                        var commandParameters = new List<SqlParameter>(sqlDBConnection.GetSpParameters(_schema, _procname))
                            {
                                new SqlParameter("@RETURN_VALUE", 0) {Direction = ParameterDirection.ReturnValue}
                            };

                        //assign the provided values to these parameters based on parameter order
                        _dbconn.LastSql = sqlDBConnection.PrepareCommand(cmd, CommandType.StoredProcedure, _schema, _procname,
                                                 commandParameters.ToArray(), _params.ToArray());
                        commandParameters.Clear();
                        Logger.LogTrace(LoggingBoundaries.Database, "ExecuteNonQuery Called:\n{0}", _dbconn.LastSql);

                        retval = cmd.ExecuteNonQuery();
                        if (cmd.Transaction == null)
                        {
                            cmd.Connection.Close();
                            cmd.Connection = null;
                        }

                        return retval;
                    }
                }
                catch (Exception ex)
                {
                    cmd.Connection = null;
                    var ex2 = new SqlDBException(ex, cmd, _dbconn.LastSql);
                    if (Logger.HandleException(LoggingBoundaries.Database, ex2))
                        throw ex2;
                }

                return retval;
            }
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
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public void ExecuteReader(Action<IDataReader> predicate)
        {
            if (_commandType != CommandType.StoredProcedure)
            {
                executeCommandReader(predicate);
                return;
            }

            var sqlDBConnection = _dbconn as SqlDBConnection;
            if (sqlDBConnection == null) return;

            if (sqlDBConnection.Transaction != null)
                executeProcReaderTran(predicate, sqlDBConnection.Transaction, false);
            else
            {
                executeProcReader(predicate, sqlDBConnection, false);
            }
        }

        private void executeProcReader(Action<IDataReader> predicate, SqlDBConnection dbConn, bool sequential)
        {
            using (var connection = new SqlConnection(dbConn.ConnectionString))
            {
                //create a command and prepare it for execution
                using (var command = new SqlCommand {CommandTimeout = _dbconn.CommandTimeout, Connection = connection})
                {
                    _dbconn.LastSql = string.Empty;

                    try
                    {
                        using (Logger.CreateTrace(LoggingBoundaries.Database, "executeProcReader", _schema, _procname))
                        {

                            //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                            SqlParameter[] commandParameters = dbConn.GetSpParameters(_schema, _procname);

                            //assign the provided values to these parameters based on parameter order
                            _dbconn.LastSql = dbConn.PrepareCommand(command, CommandType.StoredProcedure, _schema,
                                                                    _procname, commandParameters, _params.ToArray());
                            Logger.LogTrace(LoggingBoundaries.Database, "Execute Reader Called:\n{0}", _dbconn.LastSql);

                            if (connection.State != ConnectionState.Open)
                            {
                                connection.Open();
                            }
                            using (SqlDataReader dr = sequential ? command.ExecuteReader(CommandBehavior.SequentialAccess) : command.ExecuteReader())
                            {
                                predicate(dr);
                            }

                            int i = 1;  // debug line
                            i++;
                        }
                    }
                    catch (Exception ex)
                    {
                        var ex2 = new SqlDBException(ex, command, _dbconn.LastSql);
                        if (Logger.HandleException(LoggingBoundaries.Database, ex2))
                            throw ex2;
                    }
                }
            }
        }

        private void executeProcReaderTran(Action<IDataReader> predicate, SqlTransaction transaction, bool sequential)
        {
            //create a command and prepare it for execution
            using (var command = new SqlCommand { CommandTimeout = _dbconn.CommandTimeout, Connection = transaction.Connection, Transaction = transaction })
            {
                _dbconn.LastSql = string.Empty;

                try
                {
                    using (Logger.CreateTrace(LoggingBoundaries.Database, "executeProcReaderTran", _schema, _procname))
                    {
                        var sqlDBConnection = _dbconn as SqlDBConnection;
                        if (sqlDBConnection == null) return;

                        //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                        SqlParameter[] commandParameters = sqlDBConnection.GetSpParameters(_schema, _procname);

                        //assign the provided values to these parameters based on parameter order
                        _dbconn.LastSql = sqlDBConnection.PrepareCommand(command, CommandType.StoredProcedure, _schema,
                                                                         _procname, commandParameters, _params.ToArray());
                        Logger.LogTrace(LoggingBoundaries.Database, "Execute Reader Called:\n{0}", _dbconn.LastSql);

                        using (SqlDataReader dr = sequential ? command.ExecuteReader(CommandBehavior.SequentialAccess) :  command.ExecuteReader())
                        {
                            predicate(dr);
                        }
                    }
                }
                catch (Exception ex)
                {
                    command.Connection = null;
                    var ex2 = new SqlDBException(ex, command, _dbconn.LastSql);
                    if (Logger.HandleException(LoggingBoundaries.Database, ex2))
                        throw ex2;
                }
            }
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
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public object ExecuteScalar()
        {
            //create a command and prepare it for execution
            using (var cmd = new SqlCommand { CommandTimeout = _dbconn.CommandTimeout })
            {
                _dbconn.LastSql = string.Empty;

                try
                {
                    using (Logger.CreateTrace(LoggingBoundaries.Database, "ExecuteScalar", _schema, _procname))
                    {
                        var sqlDBConnection = _dbconn as SqlDBConnection;
                        if (sqlDBConnection == null) return null;
                        sqlDBConnection.SetCommandConnection(cmd);

                        //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
                        SqlParameter[] commandParameters = sqlDBConnection.GetSpParameters(_schema, _procname);

                        //assign the provided values to these parameters based on parameter order
                        _dbconn.LastSql = sqlDBConnection.PrepareCommand(cmd, CommandType.StoredProcedure, _schema, _procname, commandParameters, _params.ToArray());

                        //execute the command & return the results
                        object retval = cmd.ExecuteScalar();
                        Logger.LogTrace(LoggingBoundaries.Database, "ExecuteScalar Called:\n{0}", _dbconn.LastSql);
                        if (cmd.Transaction == null)
                        {
                            cmd.Connection.Close();
                            cmd.Connection = null;
                        }


                        return retval;
                    }
                }
                catch (Exception ex)
                {
                    cmd.Connection = null;
                    var ex2 = new SqlDBException(ex, cmd, _dbconn.LastSql);
                    if (Logger.HandleException(LoggingBoundaries.Database, ex2))
                        throw ex2;
                }
                return null;
            }
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
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public void ExecuteSequentialReader(Action<IDataReader> predicate)
        {
            var sqlDBConnection = _dbconn as SqlDBConnection;
            if (sqlDBConnection == null) return;

            if (sqlDBConnection.Transaction != null)
                executeProcReaderTran(predicate, sqlDBConnection.Transaction, true);
            else
            {
                executeProcReader(predicate, sqlDBConnection, true);
            }
        }


        #region CommandText Commands 

        private int executeCommandNonQuery()
        {
            //create a command and prepare it for execution
            using (var cmd = new SqlCommand { CommandTimeout = _dbconn.CommandTimeout })
            {
                var sqlDBConnection = _dbconn as SqlDBConnection;
                if (sqlDBConnection == null) return -1;
                sqlDBConnection.SetCommandConnection(cmd);

                cmd.CommandType = _commandType;
                cmd.CommandText = _procname;

                foreach (DbParameter param in _params)
                {
                    cmd.Parameters.AddWithValue(param.ParameterName.Replace("@", ""), param.Value);
                }

                int retval = cmd.ExecuteNonQuery();
                if (cmd.Transaction == null)
                {
                    cmd.Connection.Close();
                    cmd.Connection = null;
                }

                return retval;
            }
        }

        /// <summary>
        /// Execute a generic query and return IDataReader
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  SqlDataReader dr = ExecuteReader("SELECT * FROM USER");
        /// </remarks>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        private void executeCommandReader(Action<IDataReader> predicate)
        {
            //create a command and prepare it for execution
            using (var command = new SqlCommand { CommandTimeout = _dbconn.CommandTimeout, CommandType = _commandType, CommandText = _procname })
            {
                try
                {
                    using (Logger.CreateTrace(LoggingBoundaries.Database, "ExecuteReader", _procname))
                    {
                        foreach (DbParameter param in _params)
                        {
                            command.Parameters.AddWithValue(param.ParameterName.Replace("@", ""), param.Value);
                        }

                        var sqlDBConnection = _dbconn as SqlDBConnection;
                        if (sqlDBConnection == null) return;
                        sqlDBConnection.SetCommandConnection(command);

                        Logger.LogTrace(LoggingBoundaries.Database, "Execute Reader Called:\n{0}", _procname);

                        // call ExecuteReader with the appropriate CommandBehavior
                        using (SqlDataReader dr = command.ExecuteReader())
                        {
                            predicate(dr);                            
                        }
                    }
                }
                catch (Exception ex)
                {
                    command.Connection = null;
                    var ex2 = new SqlDBException(ex, command, _procname);
                    if (Logger.HandleException(LoggingBoundaries.Database, ex2))
                        throw ex2;
                }
            }
        }

        #endregion // CommandText Command

        /// <summary>
        /// Get the value of a output paramter that was returned during the execution of the command.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public DbParameter GetOutParameter(string name)
        {
            DbParameter tparam = _dbconn.CreateParameter(name, null);
            return _params.FirstOrDefault(param => string.Compare(param.ParameterName, tparam.ParameterName, StringComparison.InvariantCultureIgnoreCase) == 0);
        }

        /// <summary>
        /// Get the value return parameter
        /// </summary>
        /// <returns></returns>
        public object GetReturnParameter()
        {
            return (from param in _params where param.Direction == ParameterDirection.ReturnValue select param.Value).FirstOrDefault();
        }

        #endregion Methods
    }

}