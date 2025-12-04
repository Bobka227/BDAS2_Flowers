using System.Data;
using System.ComponentModel.DataAnnotations;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.AdminModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/payments")]
public class AdminPaymentsController : Controller
{
    private readonly IDbFactory _db;
    public AdminPaymentsController(IDbFactory db) => _db = db;

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var rows = new List<AdminPaymentRowVm>();

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
          SELECT ID,
                 PAY_DATE,
                 METHOD_NAME,
                 AMOUNT,
                 ACCEPTED,
                 RETURNED,
                 CARD_NUMBER,
                 COUPON_DATE,
                 BONUS
            FROM VW_ADMIN_PAYMENTS
           ORDER BY PAY_DATE DESC";

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add(new AdminPaymentRowVm
            {
                Id = DbRead.GetInt32(r, 0),
                PayDate = r.GetDateTime(1),
                Method = r.IsDBNull(2) ? "" : r.GetString(2),
                Amount = (decimal)r.GetDecimal(3),
                Accepted = r.IsDBNull(4) ? (decimal?)null : (decimal)r.GetDecimal(4),
                Returned = r.IsDBNull(5) ? (decimal?)null : (decimal)r.GetDecimal(5),
                CardNumber = r.IsDBNull(6) ? null : r.GetString(6),
                CouponDate = r.IsDBNull(7) ? (DateTime?)null : r.GetDateTime(7),
                Bonus = r.IsDBNull(8) ? (decimal?)null : (decimal)r.GetDecimal(8)
            });
        }

        return View("/Views/AdminPanel/Payments/Index.cshtml", rows);
    }

    [HttpGet("create")]
    public IActionResult Create()
    {
        var vm = new AdminPaymentEditVm { PayDate = DateTime.Today };
        return View("/Views/AdminPanel/Payments/Edit.cshtml", vm);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AdminPaymentEditVm vm)
    {
        if (!ModelState.IsValid)
            return View("/Views/AdminPanel/Payments/Edit.cshtml", vm);

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_PAYMENT_CREATE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_paydate", OracleDbType.Date).Value = vm.PayDate;
        cmd.Parameters.Add("p_amount", OracleDbType.Decimal).Value = vm.Amount;
        cmd.Parameters.Add("p_method", OracleDbType.Varchar2).Value = vm.MethodCode;

        await cmd.ExecuteNonQueryAsync();
        TempData["Msg"] = "Platba byla vytvořena.";
        return RedirectToAction(nameof(Index));
    }


    [HttpGet("edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = new AdminPaymentEditVm();

        await using var conn = await _db.CreateOpenAsync();
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = @"
          SELECT ID,
                 PAY_DATE,
                 AMOUNT,
                 METHOD_CODE
            FROM VW_ADMIN_PAYMENTS
           WHERE ID = :id";

            cmd.Parameters.Add(new OracleParameter("id", OracleDbType.Int32, id, ParameterDirection.Input));

            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync())
                return NotFound();

            vm.Id = DbRead.GetInt32(r, 0);   
            vm.PayDate = r.GetDateTime(1);         
            vm.Amount = (decimal)r.GetDecimal(2); 
            vm.MethodCode = r.GetString(3);        
        }

        return View("/Views/AdminPanel/Payments/Edit.cshtml", vm);
    }


    [HttpPost("edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AdminPaymentEditVm vm)
    {
        if (id != vm.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View("/Views/AdminPanel/Payments/Edit.cshtml", vm);

        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_PAYMENT_UPDATE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = vm.Id;
        cmd.Parameters.Add("p_paydate", OracleDbType.Date).Value = vm.PayDate;
        cmd.Parameters.Add("p_amount", OracleDbType.Decimal).Value = vm.Amount;
        cmd.Parameters.Add("p_method", OracleDbType.Varchar2).Value = vm.MethodCode;

        await cmd.ExecuteNonQueryAsync();
        TempData["Msg"] = "Platba byla upravena.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        await using var conn = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_PAYMENT_DELETE", (OracleConnection)conn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };
        cmd.Parameters.Add("p_id", OracleDbType.Int32).Value = id;

        try
        {
            await cmd.ExecuteNonQueryAsync();
            TempData["Msg"] = "Platba byla smazána.";
        }
        catch (OracleException ex)
        {
            TempData["Msg"] = "Nelze smazat platbu: " + ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }
}
