using System.Data;
using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Data
{
    public interface IDbFactory
    {
        Task<OracleConnection> CreateOpenAsync();
    }

    public class OracleDbFactory : IDbFactory
    {
        private readonly OracleConnectionStringBuilder _csb;
        private readonly IHttpContextAccessor _http;   

        public OracleDbFactory(OracleConnectionStringBuilder csb, IHttpContextAccessor http)
        {
            _csb = csb;
            _http = http;
        }

        public async Task<OracleConnection> CreateOpenAsync()
        {
            var con = new OracleConnection(_csb.ConnectionString);

            await con.OpenAsync();

            var actor = ResolveActor();
            await using (var cmd = new OracleCommand("begin dbms_session.set_identifier(:id); end;", con))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add("id", OracleDbType.Varchar2, 100).Value = actor;
                await cmd.ExecuteNonQueryAsync();
            }

            return con;
        }



        private string ResolveActor()
        {
            var user = _http.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                return user.Identity!.Name
                       ?? user.FindFirst("email")?.Value
                       ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value
                       ?? "app";
            }
            return "app";
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
