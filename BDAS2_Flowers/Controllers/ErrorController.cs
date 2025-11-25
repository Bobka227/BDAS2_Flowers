using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers
{
    [Route("Error")]
    public class ErrorController : Controller
    {
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

        [Route("DbConnection")]
        public IActionResult DbConnection()
        {
            return View("~/Views/Shared/ErrorDbConnection.cshtml");
        }
    }
}
