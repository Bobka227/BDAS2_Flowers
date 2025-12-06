using BDAS2_Flowers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;

namespace BDAS2_Flowers.Controllers.AdminControllers;

/// <summary>
/// Administrátorský controller pro správu adres (CRUD operace nad adresami).
/// </summary>
[Authorize(Roles = "Admin")]
[Route("admin/addresses")]
public class AdminAddressesController : Controller
{
    private readonly IDbFactory _db;

    /// <summary>
    /// Inicializuje novou instanci <see cref="AdminAddressesController"/> s továrnou na databázová připojení.
    /// </summary>
    /// <param name="db">Továrna pro vytváření otevřených databázových připojení.</param>
    public AdminAddressesController(IDbFactory db) => _db = db;

    /// <summary>
    /// Zobrazí seznam adres s možností filtrování podle ulice a PSČ.
    /// </summary>
    /// <param name="qStreet">Část názvu ulice pro fulltextové filtrování (nepovinné).</param>
    /// <param name="qPostal">PSČ pro přesné filtrování (nepovinné).</param>
    /// <returns>View se seznamem adres pro administraci.</returns>
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

    /// <summary>
    /// Vytvoří novou adresu pomocí uložené procedury <c>PRC_CREATE_ADDRESS</c>.
    /// </summary>
    /// <param name="postalcode">PSČ nové adresy.</param>
    /// <param name="street">Název ulice nové adresy.</param>
    /// <param name="housenumber">Číslo domu nové adresy.</param>
    /// <returns>Přesměrování zpět na seznam adres.</returns>
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

    /// <summary>
    /// Aktualizuje existující adresu pomocí uložené procedury <c>PRC_ADDRESS_UPDATE</c>.
    /// </summary>
    /// <param name="id">Identifikátor upravované adresy.</param>
    /// <param name="postalcode">Nové PSČ adresy.</param>
    /// <param name="street">Nový název ulice.</param>
    /// <param name="housenumber">Nové číslo domu.</param>
    /// <returns>Přesměrování zpět na seznam adres se zachováním filtrů.</returns>
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
        catch (OracleException ex)
        {
            string msg = ex.Number switch
            {
                1 => "Upravená adresa koliduje s jinou existující adresou.",
                1438 => "PSČ nebo číslo domu má neplatný formát (příliš velké číslo).",
                _ => "Nelze upravit adresu. Zkontrolujte údaje a zkuste to znovu."
            };

            TempData["Msg"] = msg;
        }

        return RedirectToAction(nameof(Index), new { qStreet = Request.Query["qStreet"], qPostal = Request.Query["qPostal"] });
    }

    /// <summary>
    /// Odstraní existující adresu pomocí uložené procedury <c>PRC_ADDRESS_DELETE</c>.
    /// </summary>
    /// <param name="id">Identifikátor odstraňované adresy.</param>
    /// <returns>Přesměrování zpět na seznam adres se zachováním filtrů.</returns>
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
