#region Copyright / Comments

// <copyright file="IDBConnection.cs" company="Civic Engineering & IT">Copyright © Civic Engineering & IT 2013</copyright>
// <author>Chris Doty</author>
// <email>dotyc@civicinc.com</email>
// <date>6/4/2013</date>
// <summary></summary>

#endregion Copyright / Comments

#region References

using System.Data;
using System.Data.Common;

#endregion References

namespace Civic.Core.Data
{
    /// <summary>
    /// Defines an IDBConnection class that is used to connect to a database server
    /// </summary>
    public interface IDBConnection
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

        /// <summary>
        /// gets the last sql statement that was executed.
        /// </summary>
        string LastSQL
        {
            get;
        }

        /// <summary>
        /// get/sets of if the query will have a return value
        /// </summary>
        bool QueryReturnValue
        {
            get; set;
        }

        /// <summary>
        /// the return value of the command
        /// </summary>
        int ReturnValue
        {
            get; set;
        }

        /// <summary>
        /// the default database schema prefix to be appended to the store procedure names
        /// </summary>
        string Schema
        {
            get; set;
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
        /// clones the database connection
        /// </summary>
        /// <returns>the newly cloned database connection</returns>
        IDBConnection Clone();

        void Commit();

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
        /// Creates an IDBCommand compatible object for the requested stored procedure
        /// </summary>
        /// <param name="procName">the name of the stored procedure to request the stored procedure for</param>
        /// <returns>The command object for the requested stored procedure</returns>
        IDBCommand CreateStoredProcCommand(string procName);

        int ExecuteCommand( string commandText, params object[] parameterValues );

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns a resultset) against the database specified in 
        /// the connection string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  DataSet ds = ExecuteDataset("GetOrders", 24, 36);
        /// </remarks>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        DataSet ExecuteDataset(string spName, params object[] parameterValues);

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
        /// <param name="spName">the name of the stored prcedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        int ExecuteNonQuery(string spName, params object[] parameterValues);

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
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        IDataReader ExecuteReader(string spName, params object[] parameterValues);

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
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        object ExecuteScalar(string spName, params object[] parameterValues);

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns a resultset) against the database specified in 
        /// the connection string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// 
        /// Programmer is responsible to close reader to kill connection.  This is used for large binary objects from the database.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// When you use ExecuteSequentialReader, you must retrieve columns in sequence. For example, if you have three columns, and the BLOB data is in the third column, you must retrieve the data from the first and second columns, before you retrieve the data from the third column
        /// e.g.:  
        ///  SqlDataReader dr = ExecuteReader("GetOrders", 24, 36);
        /// </remarks>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        IDataReader ExecuteSequentialReader(string spName, params object[] parameterValues);

        /// <summary>
        /// 
        /// </summary>
        /// <param name="spName">the name of the stored procedure to execute</param>
        /// <param name="dataSet">the name of the date set to store the results in</param>
        /// <param name="tableNames">an array of the table names for each result set</param>
        /// <param name="parameterValues">the parameters to be used when executing the store procedure</param>
        void FillDataset(string spName, DataSet dataSet, string[] tableNames, params object[] parameterValues);

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