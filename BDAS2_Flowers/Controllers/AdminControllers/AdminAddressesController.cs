using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers;

[Authorize(Roles = "Admin")]
[Route("admin/addresses")]
public class AdminAddressesController : Controller
{
    private readonly IDbFactory _db;
    public AdminAddressesController(IDbFactory db) => _db = db;

    // GET /admin/addresses
    [HttpGet("")]
    public async Task<IActionResult> Index(string? qStreet, int? qPostal)
    {
        var rows = new List<(int Id, string Street, int House, int Postal, DateTime? LastUsed, int UsedCount)>();

        await using var con = await _db.CreateOpenAsync();
        await using var cmd = con.CreateCommand();

        var sql = @"SELECT ADDRESSID, STREET, HOUSENUMBER, POSTALCODE, LAST_USED, USED_COUNT
                    FROM VW_ADMIN_ADDRESSES WHERE 1=1";
        if (!string.IsNullOrWhiteSpace(qStreet))
        {
            sql += " AND UPPER(STREET) LIKE UPPER('%' || :qs || '%')";
            var p = cmd.CreateParameter(); p.ParameterName = "qs"; p.Value = qStreet.Trim(); cmd.Parameters.Add(p);
        }
        if (qPostal.HasValue)
        {
            sql += " AND POSTALCODE = :qp";
            var p = cmd.CreateParameter(); p.ParameterName = "qp"; p.Value = qPostal.Value; cmd.Parameters.Add(p);
        }
        sql += " ORDER BY STREET, HOUSENUMBER";
        cmd.CommandText = sql;

        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            rows.Add((r.GetInt32(0),
                      r.GetString(1),
                      r.GetInt32(2),
                      r.GetInt32(3),
                      r.IsDBNull(4) ? (DateTime?)null : r.GetDateTime(4),
                      r.GetInt32(5)));
        }

        ViewBag.qStreet = qStreet;
        ViewBag.qPostal = qPostal;
        ViewData["Title"] = "Adresy";
        return View("/Views/AdminPanel/Addresses/Index.cshtml", rows);
    }

    // POST /admin/addresses/create
    [ValidateAntiForgeryToken]
    [HttpPost("create")]
    public async Task<IActionResult> Create(int postalcode, string street, int housenumber)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_CREATE_ADDRESS", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };

        var outId = new OracleParameter("o_address_id", OracleDbType.Int32) { Direction = ParameterDirection.Output };

        cmd.Parameters.Add("p_postalcode", OracleDbType.Int32).Value = postalcode;
        cmd.Parameters.Add("p_street", OracleDbType.Varchar2, 200).Value = street?.Trim();
        cmd.Parameters.Add("p_housenumber", OracleDbType.Int32).Value = housenumber;
        cmd.Parameters.Add(outId);

        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Adresa vytvořena."; }
        catch (OracleException ex) { TempData["Msg"] = "Nelze vytvořit adresu: " + ex.Message; }

        return RedirectToAction(nameof(Index));
    }

    // POST /admin/addresses/{id}/update
    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/update")]
    public async Task<IActionResult> Update(int id, int postalcode, string street, int housenumber)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_ADDRESS_UPDATE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };

        cmd.Parameters.Add("p_address_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_postalcode", OracleDbType.Int32).Value = postalcode;
        cmd.Parameters.Add("p_street", OracleDbType.Varchar2, 200).Value = street?.Trim();
        cmd.Parameters.Add("p_housenumber", OracleDbType.Int32).Value = housenumber;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Adresa upravena."; }
        catch (OracleException ex) { TempData["Msg"] = "Nelze upravit adresu: " + ex.Message; }

        return RedirectToAction(nameof(Index), new { qStreet = Request.Query["qStreet"], qPostal = Request.Query["qPostal"] });
    }

    // POST /admin/addresses/{id}/delete
    [ValidateAntiForgeryToken]
    [HttpPost("{id:int}/delete")]
    public async Task<IActionResult> Delete(int id)
    {
        await using var con = await _db.CreateOpenAsync();
        await using var cmd = new OracleCommand("PRC_ADDRESS_DELETE", (OracleConnection)con)
        { CommandType = CommandType.StoredProcedure, BindByName = true };

        cmd.Parameters.Add("p_address_id", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = User.Identity?.Name ?? "admin";

        try { await cmd.ExecuteNonQueryAsync(); TempData["Msg"] = "Adresa odstraněna."; }
        catch (OracleException ex) { TempData["Msg"] = "Nelze odstranit adresu: " + ex.Message; }

        return RedirectToAction(nameof(Index), new { qStreet = Request.Query["qStreet"], qPostal = Request.Query["qPostal"] });
    }
}