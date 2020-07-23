using System;
using System.Data.Common;

namespace Core.Data
{
    
    public class SqlDBException : Exception
    {
        public SqlDBException(Exception innerException, DbCommand command)
            : base("Database Layer Exception", innerException)
        {
            Command = command;
        }

        public SqlDBException(Exception innerException, DbCommand command, string lastSql)
            : base("Database Layer Exception" + (string.IsNullOrEmpty(lastSql) ? "" : "\n:" + lastSql), innerException)
        {
            Command = command;
        }

        public DbCommand Command { get; private set; }
    }

}
