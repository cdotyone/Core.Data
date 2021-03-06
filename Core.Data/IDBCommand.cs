#region References

using System;
using System.Data;
using System.Data.Common;

#endregion References

namespace Core.Data
{
    /// <summary>
    /// Describes a DBCommand class that is used to build sql statements to be sent to the sql database
    /// </summary>
    public interface IDBCommand : IDisposable
    {
        #region Properties

        /// <summary>
        /// The database connection that will be executing the sql command
        /// </summary>
        IDBConnection DbConnection
        {
            get;
        }

        /// <summary>
        /// the default database schema prefix to be appended to the store procedure names
        /// </summary>
        string Schema
        {
            get;
            set;
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// Adds a new In DbParameter object to the given command.
        /// </summary>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <remarks>
        /// <para>This version of the method is used when you can have the same parameter object multiple times with different values.</para>
        /// </remarks>        
        void AddInParameter(string name);

        /// <summary>
        /// Adds a new In DbParameter object to the given command.
        /// </summary>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="value"><para>The value of the parameter.</para></param>      
        void AddInParameter(string name, object value);

        /// <summary>
        /// Clears all of the parameters.  Useful when needing to reuse a command
        /// </summary>
        void ClearParameters();

        /// <summary>
        /// Adds a new Out DbParameter object to the given command.
        /// </summary>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="value"><para>The value of the parameter.</para></param>   
        void AddOutParameter(string name, object value);

        /// <summary>
        /// Adds a new Out DbParameter object to the given command.
        /// </summary>
        /// <param name="name"><para>The name of the parameter.</para></param>
        void AddOutParameter(string name);

        /// <summary>
        /// Adds a return parameter request
        /// </summary>
        void AddReturnParameter();

        /// <summary>
        /// <para>Adds a new instance of a <see cref="DbParameter"/> object to the command.</para>
        /// </summary>
        /// <param name="name"><para>The name of the parameter.</para></param>
        /// <param name="direction"><para>One of the <see cref="ParameterDirection"/> values.</para></param>                
        /// <param name="value"><para>The value of the parameter.</para></param>    
        void AddParameter(string name, ParameterDirection direction, object value);

        /// <summary>
        /// Add a parameter directly to the parameter list
        /// </summary>
        /// <param name="parameter">the parameter to add</param>
        void AddParameter(DbParameter parameter);

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns no result-set) against the database specified in 
        /// the connection string using the provided parameter values.  
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int result = ExecuteNonQuery();
        /// </remarks>
        /// <returns>an int representing the number of rows affected by the command</returns>
        int ExecuteNonQuery();

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns a result-set) against the database specified in 
        /// the connection string using the provided parameter values.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  IDataReader dr = ExecuteReader();
        /// </remarks>
        /// <returns>a data-set containing the result-set) generated by the command</returns>
        void ExecuteReader(Action<IDataReader> predicate);

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns a 1x1 result-set)) against the database specified in 
        /// the connection string using the provided parameter values. 
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  int orderCount = (int)ExecuteScalar();
        /// </remarks>
        /// <returns>an object containing the value in the 1x1 result-set) generated by the command</returns>
        object ExecuteScalar();

        /// <summary>
        /// Execute a stored procedure via a SqlCommand (that returns a result-set)) against the database specified in 
        /// the connection string using the provided parameter values.  This method will query the database to discover the parameters for the 
        /// stored procedure (the first time each stored procedure is called), and assign the values based on parameter order.
        /// </summary>
        /// <remarks>
        /// This method provides no access to output parameters or the stored procedure's return value parameter.
        /// 
        /// e.g.:  
        ///  SqlDataReader dr = ExecuteReader("GetOrders", 24, 36);
        /// </remarks>
        /// <returns>a data-set containing the result-set) generated by the command</returns>
        void ExecuteSequentialReader(Action<IDataReader> predicate);

        /// <summary>
        /// Get the value of a output parameter that was returned during the execution of the command.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        DbParameter GetOutParameter( string name );

        /// <summary>
        /// Get the value return parameter
        /// </summary>
        /// <returns></returns>
        object GetReturnParameter();

        #endregion Methods      
    }
}