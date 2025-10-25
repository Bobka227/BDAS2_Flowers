using System.Data;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Data
{
    public interface IDbFactory { Task<OracleConnection> CreateOpenAsync(); }

    public class OracleDbFactory : IDbFactory
    {
        private readonly OracleConnectionStringBuilder _csb;
        public OracleDbFactory(OracleConnectionStringBuilder csb) => _csb = csb;
        public async Task<OracleConnection> CreateOpenAsync()
        {
            var c = new OracleConnection(_csb.ConnectionString);
            await c.OpenAsync();
            return c;
        }
    }

    public static class DbRead
    {
        public static int GetInt32(System.Data.Common.DbDataReader r, int i)
        {
            var v = r.GetValue(i);
            if (v is int ii) return ii;
            if (v is long l) return (int)l;
            if (v is decimal d) return (int)d;
            return Convert.ToInt32(v);
        }
    }
}