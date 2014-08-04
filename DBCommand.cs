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
using System.Linq;

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

        private readonly IDBConnection _dbconn;          // the database connections
        private readonly List<DbParameter> _params;      // the parameters to be used when excuting the command
        private readonly string _procname;               // the store procedure name to execute
        private string _schema;                          // the schema name of the procedure/command being executed

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
        }


        #endregion Constructors

        #region Properties

        /// <summary>
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
            var val = value as System.DBNull;

            if (val != null)
                value = null;

            _params.Add(_dbconn.CreateParameter(name, direction, value));
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
        /// the connection string using the provided parameter values.  
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int result = ExecuteNonQuery();
        /// </remarks>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public int ExecuteNonQuery()
        {
            return _dbconn.ExecuteNonQuery(_schema, _procname, _params.ToArray());
        }

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns a resultset) against the database specified in 
        /// the connection string using the provided parameter values.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  IDataReader dr = ExecuteReader();
        /// </remarks>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public IDataReader ExecuteReader()
        {
            return _dbconn.ExecuteReader(_schema, _procname, _params.ToArray());
        }

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns a 1x1 resultset) against the database specified in 
        /// the connection string using the provided parameter values. 
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar();
        /// </remarks>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public object ExecuteScalar()
        {
            return _dbconn.ExecuteReader(_schema, _procname, _params.ToArray());
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
        public IDataReader ExecuteSequentialReader()
        {
            return _dbconn.ExecuteSequentialReader(_schema, _procname, _params.ToArray());
        }

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