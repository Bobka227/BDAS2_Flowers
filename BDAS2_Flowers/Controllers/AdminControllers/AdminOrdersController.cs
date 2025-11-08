using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/orders")]
public class AdminOrdersController : Controller
{
    private readonly IDbFactory _db;
    public AdminOrdersController(IDbFactory db) => _db = db;

    // POST /admin/orders/{orderNo}/status
    [ValidateAntiForgeryToken]
    [HttpPost("{orderNo}/status")]
    public async Task<IActionResult> ChangeOrderStatus(string orderNo, string statusName, string returnEmail)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_CHANGE_ORDER_STATUS_UI", (OracleConnection)conn)
        { CommandType = CommandType.StoredProcedure };
        cmd.BindByName = true;
        cmd.Parameters.Add("p_order_no", OracleDbType.Varchar2, 50).Value = orderNo;
        cmd.Parameters.Add("p_status_name", OracleDbType.Varchar2, 50).Value = statusName;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";
        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = $"Order {orderNo} → {statusName}.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze změnit status: " + ex.Message;
        }
        return Redirect($"/admin/users/{returnEmail}/orders");
    }
}
