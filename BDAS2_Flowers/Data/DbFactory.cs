using System.Data;
using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Data
{
    /// <summary>
    /// Rozhraní továrny pro vytváření a otevírání připojení k databázi Oracle.
    /// </summary>
    public interface IDbFactory
    {
        /// <summary>
        /// Vytvoří nové připojení k databázi Oracle a asynchronně jej otevře.
        /// </summary>
        /// <remarks>
        /// Metoda vrací již otevřené připojení, které je připravené k použití.
        /// Volající je zodpovědný za jeho uzavření a uvolnění prostředků.
        /// </remarks>
        /// <returns>
        /// Asynchronní úloha vracející instanci <see cref="OracleConnection"/>.
        /// </returns>
        Task<OracleConnection> CreateOpenAsync();
    }

    /// <summary>
    /// Implementace <see cref="IDbFactory"/> pro databázi Oracle.
    /// </summary>
    /// <remarks>
    /// Při každém vytvoření připojení nastaví identifikátor session (DBMS_SESSION.SET_IDENTIFIER)
    /// podle aktuálně přihlášeného uživatele HTTP kontextu, aby bylo možné
    /// logovat a auditovat operace v databázi.
    /// </remarks>
    public class OracleDbFactory : IDbFactory
    {
        private readonly OracleConnectionStringBuilder _csb;
        private readonly IHttpContextAccessor _http;

        /// <summary>
        /// Inicializuje novou instanci <see cref="OracleDbFactory"/>.
        /// </summary>
        /// <param name="csb">
        /// Připravený <see cref="OracleConnectionStringBuilder"/> s nastaveným connection stringem.
        /// </param>
        /// <param name="http">
        /// Přístup k aktuálnímu <see cref="HttpContext"/> pro zjištění přihlášeného uživatele.
        /// </param>
        public OracleDbFactory(OracleConnectionStringBuilder csb, IHttpContextAccessor http)
        {
            _csb = csb;
            _http = http;
        }

        /// <summary>
        /// Vytvoří nové připojení k databázi Oracle, asynchronně jej otevře
        /// a nastaví identifikátor session podle aktuálního uživatele.
        /// </summary>
        /// <remarks>
        /// Identifikátor je nastaven pomocí <c>DBMS_SESSION.SET_IDENTIFIER</c> a slouží typicky
        /// pro logování a audit na straně databáze. Pokud není uživatel přihlášen,
        /// použije se hodnota <c>"app"</c>.
        /// </remarks>
        /// <returns>
        /// Asynchronní úloha vracející otevřené připojení <see cref="OracleConnection"/>.
        /// </returns>
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

        /// <summary>
        /// Určí řetězec, který identifikuje aktuálního aktéra (uživatele) pro databázovou session.
        /// </summary>
        /// <remarks>
        /// Pokud je uživatel autentizován, použije se jeho jméno nebo e-mail.  
        /// V opačném případě se vrací výchozí identifikátor <c>"app"</c>.
        /// </remarks>
        /// <returns>
        /// Řetězec s identifikátorem aktéra pro použití v databázi.
        /// </returns>
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

    /// <summary>
    /// Pomocné metody pro čtení hodnot z databázového readeru.
    /// </summary>
    public static class DbRead
    {
        /// <summary>
        /// Přečte hodnotu na daném indexu jako <see cref="int"/> bez ohledu na skutečný typ čísla.
        /// </summary>
        /// <remarks>
        /// Metoda podporuje typy <see cref="int"/>, <see cref="long"/> a <see cref="decimal"/>,
        /// případně provede konverzi přes <see cref="Convert.ToInt32(object)"/>.
        /// </remarks>
        /// <param name="r">Databázový reader, ze kterého se hodnota čte.</param>
        /// <param name="i">Index sloupce v readeru.</param>
        /// <returns>Hodnota převedená na <see cref="int"/>.</returns>
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
