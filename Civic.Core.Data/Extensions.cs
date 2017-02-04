using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Civic.Core.Data
{
    public static class Extensions
    {
        public static SqlParameter Copy(this SqlParameter self)
        {
            return new SqlParameter(self.ParameterName, self.Value) {Direction = self.Direction};
        }
    }
}
