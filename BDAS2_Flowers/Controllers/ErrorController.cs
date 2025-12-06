using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers
{
    /// <summary>
    /// Řadič pro zobrazení chybových stránek aplikace.
    /// </summary>
    /// <remarks>
    /// Slouží jako cílový bod pro globální zachytávání výjimek
    /// a pro zobrazení speciální chybové stránky při problému s databázovým
    /// připojením.
    /// </remarks>
    [Route("Error")]
    public class ErrorController : Controller
    {
        /// <summary>
        /// Obecný handler pro neošetřené výjimky v aplikaci.
        /// </summary>
        /// <remarks>
        /// Získá detail výjimky z <see cref="IExceptionHandlerPathFeature"/>.
        /// Pokud jde o chybu připojení k databázi (např. ORA-12545),
        /// vrátí speciální chybovou stránku pro databázi. V ostatních případech
        /// zobrazí obecnou chybovou stránku.
        /// </remarks>
        /// <returns>
        /// Chybový pohled <c>ErrorDbConnection.cshtml</c> nebo obecný
        /// chybový pohled <c>Error.cshtml</c>.
        /// </returns>
        [Route("Handle")]
        public IActionResult Handle()
        {
            var feature = HttpContext.Features.Get<IExceptionHandlerPathFeature>();
            var ex = feature?.Error;

            if (ex is OracleException oex && oex.Message.Contains("ORA-12545"))
            {
                return View("~/Views/Shared/ErrorDbConnection.cshtml");
            }

            return View("~/Views/Shared/Error.cshtml");
        }

        /// <summary>
        /// Zobrazí chybovou stránku pro problém s připojením k databázi.
        /// </summary>
        /// <remarks>
        /// Tuto akci lze použít i samostatně, například při explicitním
        /// zjištění, že databáze není dostupná.
        /// </remarks>
        /// <returns>
        /// Chybový pohled <c>ErrorDbConnection.cshtml</c>.
        /// </returns>
        [Route("DbConnection")]
        public IActionResult DbConnection()
        {
            return View("~/Views/Shared/ErrorDbConnection.cshtml");
        }
    }
}
