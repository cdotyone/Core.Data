using System;
using System.Data.Common;

namespace Civic.Core.Data
{
    
    public class SqlDBException : Exception
    {
        public SqlDBException(Exception innerException, DbCommand command)
            : base("Database Layer Exeception", innerException)
        {
            Command = command;
        }

        public SqlDBException(Exception innerException, DbCommand command, string lastSql)
            : base("Database Layer Exeception" + (string.IsNullOrEmpty(lastSql) ? "" : "\n:" + lastSql), innerException)
        {
            Command = command;
        }

        public DbCommand Command { get; private set; }
    }

}
