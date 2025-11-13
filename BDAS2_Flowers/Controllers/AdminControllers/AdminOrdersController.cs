using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using BDAS2_Flowers.Models.ViewModels.OrderModels;

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

    // GET /admin/orders
    [HttpGet("")]
    public async Task<IActionResult> Index(string? q = null, string? status = null)
    {
        var rows = new List<AdminOrderRowVm>();

        await using var con = await _db.CreateOpenAsync();
        await using var cmd = con.CreateCommand();
        cmd.BindByName = true;

        cmd.CommandText = @"
            SELECT ORDER_NO, ORDERDATE, CUSTOMER, STATUS, DELIVERY, SHOP, TOTAL
              FROM VW_ADMIN_ORDERS
             WHERE (:q IS NULL OR UPPER(ORDER_NO) LIKE UPPER('%' || :q || '%')
                    OR UPPER(CUSTOMER) LIKE UPPER('%' || :q || '%'))
               AND (:s IS NULL OR UPPER(STATUS) = UPPER(:s))
             ORDER BY ORDERDATE DESC";

        cmd.Parameters.Add("q", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(q) ? (object)DBNull.Value : q!.Trim();
        cmd.Parameters.Add("s", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(status) ? (object)DBNull.Value : status!.Trim();

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new AdminOrderRowVm
            {
                OrderNo = r.GetString(0),
                OrderDate = r.GetDateTime(1),
                Customer = r.GetString(2),
                Status = r.GetString(3),
                Delivery = r.GetString(4),
                Shop = r.GetString(5),
                Total = (decimal)r.GetDecimal(6)
            });
        }

        ViewData["Title"] = "Objednávky";
        return View("/Views/AdminPanel/Orders/Index.cshtml", rows);
    }

    [HttpPost("{orderNo}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string orderNo)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = con.CreateCommand();

        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;
        cmd.CommandText = "ST72861.PRC_ADMIN_ORDER_DELETE"; 

        cmd.Parameters.Add("p_order_no", OracleDbType.Varchar2, 50).Value = orderNo;

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = $"Objednávka {orderNo} byla smazána.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Chyba při mazání objednávky: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }


    // GET /admin/orders/{orderNo}
    [HttpGet("{orderNo}")]
    public async Task<IActionResult> Details(string orderNo)
    {
        var vm = new OrderDetailsVm();

        await using var con = await _db.CreateOpenAsync();

        await using (var head = con.CreateCommand())
        {
            head.BindByName = true;
            head.CommandText = @"
            SELECT ORDERID, ORDER_NO, ORDERDATE, CUSTOMER, STATUS, DELIVERY, SHOP, TOTAL
            FROM VW_ADMIN_ORDER_DETAILS
            WHERE ORDER_NO = :no";
            head.Parameters.Add("no", OracleDbType.Varchar2, 50).Value = orderNo;

            await using var hr = await head.ExecuteReaderAsync();
            if (!await hr.ReadAsync()) return NotFound();

            vm.OrderId = hr.GetInt32(0);
            vm.PublicNo = hr.GetString(1);
            vm.OrderDate = hr.GetDateTime(2);
            vm.Customer = hr.GetString(3);
            vm.Status = hr.GetString(4);
            vm.Delivery = hr.GetString(5);
            vm.Shop = hr.GetString(6);
            vm.Total = (decimal)hr.GetDecimal(7);
        }

        await using (var items = con.CreateCommand())
        {
            items.BindByName = true;
            items.CommandText = @"
            SELECT PRODUCTID, PRODUCT_NAME, QUANTITY, UNITPRICE, LINE_TOTAL
            FROM VW_ADMIN_ORDER_ITEMS
            WHERE ORDER_NO = :no
            ORDER BY PRODUCT_NAME";
            items.Parameters.Add("no", OracleDbType.Varchar2, 50).Value = orderNo;

            await using var ir = await items.ExecuteReaderAsync();
            while (await ir.ReadAsync())
            {
                vm.Items.Add(new OrderItemDetailsVm
                {
                    ProductId = ir.GetInt32(0),
                    ProductName = ir.GetString(1),
                    Quantity = ir.GetInt32(2),
                    UnitPrice = (decimal)ir.GetDecimal(3),
                    LineTotal = (decimal)ir.GetDecimal(4)
                });
            }
        }

        ViewData["Title"] = "Detail objednávky";
        return View("/Views/AdminPanel/Orders/Details.cshtml", vm);
    }
}
