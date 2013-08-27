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
using System.Text;
using System.Xml;

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
        private int _sqldbReturnValue;
        private int _commandTimeout = 30;       // timout for commands
        private string _connectionString;       // connection string
        private string _dbcode = "NONE";        // database code assigned to connection string
        private bool _getReturnValue;
        private string _lastCommand = "";       // string of last executed command
        private readonly Dictionary<string, DbParameter> _paramDefault = new Dictionary<string, DbParameter>();
        private readonly List<string> _persistDefault = new List<string>();
        private string _schemaName = "[DBO]";
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
                var list = new List<DbParameter>();

                foreach ( string key in _paramDefault.Keys )
                    list.Add( _paramDefault[key] );

                return list.ToArray();
            }
        }

        /// <summary>
        /// gets the last sql statement that was executed.
        /// </summary>
        public string LastSQL
        {
            get { return _lastCommand; }
        }

        /// <summary>
        /// get/sets of if the query will have a return value
        /// </summary>
        public bool QueryReturnValue
        {
            get { return _getReturnValue; }
            set { _getReturnValue = value; }
        }

        /// <summary>
        /// the return value of the command
        /// </summary>
        public int ReturnValue
        {
            get { return _sqldbReturnValue; }
            set { _sqldbReturnValue = value; }
        }

        /// <summary>
        /// the default database schema prefix to be appended to the store procedure names
        /// </summary>
        public string Schema
        {
            get
            {
                return _schemaName;
            }
            set
            {
                _schemaName = value;
                if (_schemaName.IndexOf('[') < 0) _schemaName = '[' + _schemaName + ']';
            }
        }

        #endregion Properties

        #region Methods

        /// <summary>
        /// RStoJSON -- this converts a DataTable to the popular JSON format
        /// </summary>
        /// <param name="dataTable">The table to convert</param>
        /// <param name="tableName">The name of the table in the JSON formated string.  
        /// This allows you to append multiple tables together in the same string</param>
        /// <returns>The JSON formated data</returns>
        public static string RStoJSON(DataTable dataTable, string tableName)
        {
            var sb = new StringBuilder();

            string[] keys = null;
            try { keys = dataTable.Rows[0]["_keyname"].ToString().Split(','); }
            catch { }

            if (keys != null) sb.Append("[{'name': '" + tableName + "', 'data': {");
            else sb.Append("[{'name': '" + tableName + "', 'data': [");
            for (int r = 0; r < dataTable.Rows.Count; r++)
            {
                bool first = true;
                if (r > 0) sb.Append(", ");
                if (keys != null)
                {
                    sb.Append("'");
                    for (int k = 0; k < keys.Length; k++)
                    {
                        if (k == 0) sb.Append(dataTable.Rows[r][keys[k]].ToString());
                        else sb.Append("_" + dataTable.Rows[r][keys[k]]);
                    }
                    sb.Append("': ");
                }
                sb.Append("{");
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    if (!(dataTable.Rows[r][i] is byte[]))
                    {
                        try
                        {
                            if (first) first = false;
                            else sb.Append(", ");

                            sb.Append("'" + dataTable.Columns[i].ColumnName.ToLower() + "': ");
                            sb.Append("'" + dataTable.Rows[r][i].ToString().Replace("\r", "\\r").Replace("\n", "\\n").Replace("\\", "\\\\").Replace("'", "\'") + "'");
                        }
                        catch { }
                    }
                }
                sb.Append("}");
            }
            sb.Append(keys != null ? "}}]" : "]}]");

            return sb.ToString();
        }

        /// <summary>
        /// RStoPORS -- Converts a table to a javascript light version of the recordset
        /// This requires the use of:
        ///     PORS_Script\poRSLib.js
        ///     PORS_Script\prototype-1.5.0.js
        /// They provide the javascript code that can convert the javascript generated
        /// by this function to hashes and arrays
        /// </summary>
        /// <param name="dataTable">The table to convert</param>
        /// <param name="tableName">The name of the table that this table will be stored under on the client browser</param>
        /// <param name="keyName">Primary key name, used to find values in the object when it is transformed on client browser</param>
        /// <param name="jsPrefix">The recieving object on the client browser</param>
        /// <returns>a string that should be executed on the client browser</returns>
        public static string RStoPORS(DataTable dataTable, string tableName, string keyName, string jsPrefix)
        {
            var sb = new StringBuilder();

            sb.Append(jsPrefix + "RSfld('" + tableName + "',$A(['_tablename'" + (keyName != null ? ",'_keyname'" : ""));
            for (int i = 0; i < dataTable.Columns.Count; i++)
            {
                string col = dataTable.Columns[i].ColumnName.ToLower();
                if (col != "_tablename" && col != "_keyname")
                    sb.Append(",'" + col + "'");
            }
            sb.AppendLine("]));");

            for (int r = 0; r < dataTable.Rows.Count; r++)
            {
                sb.Append(jsPrefix + "RS($A(['" + tableName + "'" + (keyName != null ? ",'" + keyName + "'" : ""));
                for (int i = 0; i < dataTable.Columns.Count; i++)
                {
                    string col = dataTable.Columns[i].ColumnName.ToLower();
                    if (col != "_tablename" && col != "_keyname")
                    {
                        if (!(dataTable.Rows[r][i] is byte[]))
                        {
                            try
                            {
                                sb.Append(",'" + dataTable.Rows[r][i].ToString().Replace("\r", "\\r").Replace("\n", "\\n").Replace("\\", "\\\\").Replace("'", "\\'") + "'");
                            }
                            catch { }
                        }
                    }
                }
                sb.AppendLine("]));");
            }

            return sb.ToString();
        }

        /// <summary>
        /// RStoXML -- converts a DataTable and addes it to a XmlDocument object
        /// </summary>
        /// <param name="parentNode">The xml node to attach this data table to.</param>
        /// <param name="dataTable">The table to convert</param>
        /// <param name="tableName">The name of the table to use when created XML nodes</param>
        /// <param name="nameSpace">The name space to use</param>
        /// <param name="nameSpaceURI">The name space URL to use</param>
        /// <returns>Parent XML Node that now contains the data table</returns>
        public static XmlNode RStoXML(XmlNode parentNode, DataTable dataTable, string tableName, string nameSpace, string nameSpaceURI)
        {
            XmlDocument xdoc = parentNode.OwnerDocument;
            if (xdoc != null)
            {
                XmlNode rnode = xdoc.CreateNode(XmlNodeType.Element, nameSpace, tableName.ToUpper() + "S", nameSpaceURI);
                parentNode.AppendChild(rnode);

                for (int r = 0; r < dataTable.Rows.Count; r++)
                {
                    XmlNode node = xdoc.CreateNode(XmlNodeType.Element, nameSpace, tableName.ToUpper(), nameSpaceURI);

                    for (int i = 0; i < dataTable.Columns.Count; i++)
                    {
                        if (!(dataTable.Rows[r][i] is byte[]))
                        {
                            try
                            {
                                var xattr = (XmlAttribute)xdoc.CreateNode(XmlNodeType.Attribute, dataTable.Columns[i].ColumnName.ToUpper(), null);
                                xattr.Value = dataTable.Rows[r][i].ToString();
                                if (node.Attributes != null) node.Attributes.Append(xattr);
                            }
                            catch { }
                        }
                    }

                    rnode.AppendChild(node);
                }
            }

            return parentNode;
        }

        /// <summary>
        /// RStoXML -- converts a DataTable and addes it to a XmlDocument object
        /// </summary>
        /// <param name="parentNode">The xml node to attach this data table to.</param>
        /// <param name="dataTable">The table to convert</param>
        /// <param name="tableName">The name of the table to use when created XML nodes</param>
        /// <returns>Parent XML Node that now contains the data table</returns>
        public static XmlNode RStoXML(XmlNode parentNode, DataTable dataTable, string tableName)
        {
            return RStoXML(parentNode, dataTable, tableName, null, null);
        }

        /// <summary>
        /// RStoXML -- converts a DataTable and addes it to a XmlDocument object
        /// </summary>
        /// <param name="parentNode">The xml node to attach this data table to.</param>
        /// <param name="dataTable">The table to convert</param>
        /// <returns>Parent XML Node that now contains the data table</returns>
        public static XmlNode RStoXML(XmlNode parentNode, DataTable dataTable)
        {
            return RStoXML(parentNode, dataTable, string.IsNullOrEmpty(dataTable.TableName) ? "TABLE" : dataTable.TableName, null, null);
        }

        /// <summary>
        /// RStoXML -- converts a DataTable and addes it to a XmlDocument object
        /// </summary>
        /// <param name="parentName">The xml node to attach this data table to.</param>
        /// <param name="dataTable">The table to convert</param>
        /// <param name="tableName">The name of the table to use when created XML nodes</param>
        /// <param name="nameSpace">The name space to use</param>
        /// <param name="nameSpaceURI">The name space URL to use</param>
        /// <returns>Parent XML Node that now contains the data table</returns>
        public static XmlNode RStoXML(string parentName, DataTable dataTable, string tableName, string nameSpace, string nameSpaceURI)
        {
            var xdoc = new XmlDocument();
            XmlNode xroot = xdoc.AppendChild(xdoc.CreateElement(parentName));
            return RStoXML(xroot, dataTable, tableName, nameSpace, nameSpaceURI);
        }

        /// <summary>
        /// RStoXML -- converts a DataTable and addes it to a XmlDocument object
        /// </summary>
        /// <param name="parentName">The name of the parent node to create</param>
        /// <param name="dataTable">The table to convert</param>
        /// <param name="tableName">The name of the table to use when created XML nodes</param>
        /// <returns>Parent XML Node that now contains the data table</returns>
        public static XmlNode RStoXML(string parentName, DataTable dataTable, string tableName)
        {
            return RStoXML(parentName, dataTable, tableName, null, null);
        }

        /// <summary>
        /// RStoXML -- converts a DataTable and addes it to a XmlDocument object
        /// </summary>
        /// <param name="parentName">The name of the parent node to create</param>
        /// <param name="dataTable">The table to convert</param>
        /// <returns>Parent XML Node that now contains the data table</returns>
        public static XmlNode RStoXML(string parentName, DataTable dataTable)
        {
            return RStoXML(parentName, dataTable, string.IsNullOrEmpty(dataTable.TableName) ? "TABLE" : dataTable.TableName, null, null);
        }

        /// <summary>
        /// RStoXML -- converts a DataTable and addes it to a XmlDocument object
        /// </summary>
        /// <param name="dataTable">The table to convert</param>
        /// <param name="tableName">The name of the table to use when created XML nodes</param>
        /// <param name="nameSpace">The name space to use</param>
        /// <param name="nameSpaceURI">The name space URL to use</param>
        /// <returns>Parent XML Node that now contains the data table</returns>
        public static XmlNode RStoXML(DataTable dataTable, string tableName, string nameSpace, string nameSpaceURI)
        {
            return RStoXML("DATA", dataTable, tableName, nameSpace, nameSpaceURI);
        }

        /// <summary>
        /// RStoXML -- converts a DataTable and addes it to a XmlDocument object
        /// </summary>
        /// <param name="dataTable">The table to convert</param>
        /// <param name="tableName">The name of the table to use when created XML nodes</param>
        /// <returns>Parent XML Node that now contains the data table</returns>
        public static XmlNode RStoXML(DataTable dataTable, string tableName)
        {
            return RStoXML("DATA", dataTable, tableName, null, null);
        }

        /// <summary>
        /// RStoXML -- converts a DataTable and addes it to a XmlDocument object
        /// </summary>
        /// <param name="dataTable">The table to convert</param>
        /// <returns>Parent XML Node that now contains the data table</returns>
        public static XmlNode RStoXML(DataTable dataTable)
        {
            return RStoXML("DATA", dataTable, string.IsNullOrEmpty(dataTable.TableName) ? "TABLE" : dataTable.TableName, null, null);
        }

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

            newConn._schemaName = _schemaName;
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

        public IDBCommand CreateStoredProcCommand(string procName)
        {
            return new DBCommand(this, procName);
        }

        public int ExecuteCommand( string commandText, params object[] parameterValues )
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand {CommandTimeout = CommandTimeout};

            if ( _transaction != null ) { cmd.Connection = _transaction.Connection; cmd.Transaction = _transaction; }
            else cmd.Connection = new SqlConnection( _connectionString );
            cmd.Connection.Open();

            cmd.CommandType = CommandType.Text;
            cmd.CommandText = commandText;

            foreach ( object obj in parameterValues )
            {
                if ( obj is DbParameter )
                {
                    var param = (DbParameter)obj;
                    cmd.Parameters.AddWithValue( param.ParameterName.Replace( "@", "" ), param.Value );
                }
                else
                {
                    cmd.Parameters.Add( parameterValues );
                }
            }

            int retval = cmd.ExecuteNonQuery();

            if ( _transaction == null ) cmd.Connection.Close();
            if ( _getReturnValue ) _sqldbReturnValue = (int)cmd.Parameters[0].Value;

            // detach the SqlParameters from the command object, so they can be used again.
            cmd.Parameters.Clear();
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
        ///  DataSet ds = ExecuteDataset("GetOrders", 24, 36);
        /// </remarks>
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public DataSet ExecuteDataset(string spName, params object[] parameterValues)
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand {CommandTimeout = CommandTimeout};

            if (_transaction != null) { cmd.Connection = _transaction.Connection; cmd.Transaction = _transaction; }
            else cmd.Connection = new SqlConnection(_connectionString);

            //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
            SqlParameter[] commandParameters = getSpParameters(spName);

            //assign the provided values to these parameters based on parameter order
            prepareCommand(cmd, CommandType.StoredProcedure, spName, commandParameters, parameterValues);

            //create the DataAdapter & DataSet
            var da = new SqlDataAdapter(cmd);
            var ds = new DataSet();

            //fill the DataSet using default values for DataTable names, etc.
            da.Fill(ds);

            if (_transaction == null) cmd.Connection.Close();
            if (_getReturnValue) _sqldbReturnValue = (int)cmd.Parameters[0].Value;

            // detach the SqlParameters from the command object, so they can be used again.
            cmd.Parameters.Clear();

            return ds;
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
        /// <param name="spName">the name of the stored prcedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an int representing the number of rows affected by the command</returns>
        public int ExecuteNonQuery(string spName, params object[] parameterValues)
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand {CommandTimeout = CommandTimeout};

            if (_transaction != null) { cmd.Connection = _transaction.Connection; cmd.Transaction = _transaction; }
            else cmd.Connection = new SqlConnection(_connectionString);

            //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
            SqlParameter[] commandParameters = getSpParameters(spName);

            //assign the provided values to these parameters based on parameter order
            prepareCommand(cmd, CommandType.StoredProcedure, spName, commandParameters, parameterValues);

            int retval = cmd.ExecuteNonQuery();

            if (_transaction == null) cmd.Connection.Close();
            if (_getReturnValue) _sqldbReturnValue = (int)cmd.Parameters[0].Value;

            // detach the SqlParameters from the command object, so they can be used again.
            cmd.Parameters.Clear();
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
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public IDataReader ExecuteReader(string spName, params object[] parameterValues)
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand {CommandTimeout = CommandTimeout};

            if (_transaction != null) { cmd.Connection = _transaction.Connection; cmd.Transaction = _transaction; }
            else cmd.Connection = new SqlConnection(_connectionString);

            //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
            SqlParameter[] commandParameters = getSpParameters(spName);

            //assign the provided values to these parameters based on parameter order
            prepareCommand(cmd, CommandType.StoredProcedure, spName, commandParameters, parameterValues);

            //create a reader

            // call ExecuteReader with the appropriate CommandBehavior
            SqlDataReader dr = _transaction != null ? cmd.ExecuteReader() : cmd.ExecuteReader(CommandBehavior.CloseConnection);
            if (_getReturnValue) _sqldbReturnValue = (int)cmd.Parameters[0].Value;

            // detach the SqlParameters from the command object, so they can be used again.
            cmd.Parameters.Clear();

            return dr;
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
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>an object containing the value in the 1x1 resultset generated by the command</returns>
        public object ExecuteScalar(string spName, params object[] parameterValues)
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand {CommandTimeout = CommandTimeout};

            if (_transaction != null) { cmd.Connection = _transaction.Connection; cmd.Transaction = _transaction; }
            else cmd.Connection = new SqlConnection(_connectionString);

            //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
            SqlParameter[] commandParameters = getSpParameters(spName);

            //assign the provided values to these parameters based on parameter order
            prepareCommand(cmd, CommandType.StoredProcedure, spName, commandParameters, parameterValues);

            //execute the command & return the results
            object retval = cmd.ExecuteScalar();

            // detach the SqlParameters from the command object, so they can be used again.
            cmd.Parameters.Clear();

            if (_transaction == null) cmd.Connection.Close();
            if (_getReturnValue) _sqldbReturnValue = (int)cmd.Parameters[0].Value;

            // detach the SqlParameters from the command object, so they can be used again.
            cmd.Parameters.Clear();

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
        /// <param name="spName">the name of the stored procedure</param>
        /// <param name="parameterValues">an array of objects to be assigned as the input values of the stored procedure</param>
        /// <returns>a dataset containing the resultset generated by the command</returns>
        public IDataReader ExecuteSequentialReader(string spName, params object[] parameterValues)
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand {CommandTimeout = CommandTimeout};

            if (_transaction != null) { cmd.Connection = _transaction.Connection; cmd.Transaction = _transaction; }
            else cmd.Connection = new SqlConnection(_connectionString);

            //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
            SqlParameter[] commandParameters = getSpParameters(spName);

            //assign the provided values to these parameters based on parameter order
            prepareCommand(cmd, CommandType.StoredProcedure, spName, commandParameters, parameterValues);

            //create a reader

            // call ExecuteReader with the appropriate CommandBehavior
            SqlDataReader dr = cmd.ExecuteReader(CommandBehavior.SequentialAccess);
            if (_getReturnValue) _sqldbReturnValue = (int)cmd.Parameters[0].Value;

            // detach the SqlParameters from the command object, so they can be used again.
            cmd.Parameters.Clear();

            return dr;
        }

        public void FillDataset(string spName, DataSet dataSet, string[] tableNames, params object[] parameterValues)
        {
            //create a command and prepare it for execution
            var cmd = new SqlCommand {CommandTimeout = CommandTimeout};

            if (_transaction != null) { cmd.Connection = _transaction.Connection; cmd.Transaction = _transaction; }
            else cmd.Connection = new SqlConnection(_connectionString);

            //pull the parameters for this stored procedure from the parameter cache (or discover them & populate the cache)
            SqlParameter[] commandParameters = getSpParameters(spName);

            //assign the provided values to these parameters based on parameter order
            prepareCommand(cmd, CommandType.StoredProcedure, spName, commandParameters, parameterValues);

            using (var dataAdapter = new SqlDataAdapter(cmd))
            {

                // Add the table mappings specified by the user
                if (tableNames != null && tableNames.Length > 0)
                {
                    string tableName = "Table";
                    for (int index = 0; index < tableNames.Length; index++)
                    {
                        if (string.IsNullOrEmpty(tableNames[index])) throw new ArgumentException("The tableNames parameter must contain a list of tables, a value was provided as null or empty string.", "tableNames");
                        dataAdapter.TableMappings.Add(tableName, tableNames[index]);
                        tableName = "Table" + (index + 1);
                    }
                }

                // Fill the DataSet using default values for DataTable names, etc
                dataAdapter.Fill(dataSet);
            }

            if (_transaction == null) cmd.Connection.Close();
            if (_getReturnValue) _sqldbReturnValue = (int)cmd.Parameters[0].Value;

            // detach the SqlParameters from the command object, so they can be used again.
            cmd.Parameters.Clear();
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
            if (_getReturnValue)
            {
                _sqldbReturnValue = 0;
                var param = new SqlParameter("@RETURN_VALUE", 0) {Direction = ParameterDirection.ReturnValue};
                command.Parameters.Add(param);
            }

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
            var clonedParameters = new SqlParameter[_getReturnValue ? originalParameters.Count : (originalParameters.Count - 1)];

            int cnt = originalParameters.Count;
            for (int i = (_getReturnValue ? 0 : 1), j = 0; i < cnt; i++)
            {
                clonedParameters[j++] = _cloneParameter(originalParameters[i], false);
            }

            return clonedParameters;
        }

        /// <summary>
        /// Resolve at run time the appropriate set of SqlParameters for a stored procedure
        /// </summary>
        /// <param name="spName">The name of the stored procedure</param>
        /// <returns>The parameter collection discovered.</returns>
        private SqlParameterCollection discoverSpParameterSet(string spName)
        {
            if (string.IsNullOrEmpty(spName)) throw new ArgumentNullException("spName");

            var connection = new SqlConnection(_connectionString);

            string[] parts = spName.Split('.');
            if ( parts.Length < 2 ) parts = new [] { _schemaName, spName };
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
        /// <param name="spName">The name of the stored procedure</param>
        /// <returns>An array of SqlParameters</returns>
        private SqlParameter[] getSpParameters(string spName)
        {
            if (string.IsNullOrEmpty(spName)) throw new ArgumentNullException("spName");

            string key = _dbcode + ":" + spName;

            var cachedParameters = (SqlParameterCollection)_paramcache[key];
            if (cachedParameters == null)
            {
                SqlParameterCollection spParameters = discoverSpParameterSet(spName);
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
        /// <param name="commandText">the stored procedure name or T-SQL command</param>
        /// <param name="commandParameters">an array of SqlParameters to be associated with the command or 'null' if no parameters are required</param>
        /// <param name="parameterValues">an array of sqlparameter values to assign to the commandParameters</param>
        private void prepareCommand(SqlCommand command, CommandType commandType, string commandText, SqlParameter[] commandParameters, object[] parameterValues)
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
            if ( parts.Length < 2 ) parts = new [] { _schemaName, commandText };
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

            _lastCommand = sb.ToString();
        }

        #endregion Methods
    }
}