using System.Data;
using Microsoft.Extensions.Configuration;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Data
{
    public interface IDbFactory
    {
        IDbConnection Create();
    }

    public class OracleDbFactory : IDbFactory
    {
        private readonly string _cs;
        public OracleDbFactory(IConfiguration cfg)
        {
            _cs = cfg.GetConnectionString("Oracle")
                ?? throw new InvalidOperationException("ConnectionStrings:Oracle отсутствует.");
        }

        public IDbConnection Create() => new OracleConnection(_cs);
    }
}