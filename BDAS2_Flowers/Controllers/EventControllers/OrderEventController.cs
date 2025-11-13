using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.EventModels;

namespace BDAS2_Flowers.Controllers.EventControllers
{
    [Authorize]
    public class OrderEventController : Controller
    {
        private readonly IDbFactory _db;
        public OrderEventController(IDbFactory db) => _db = db;


        private int CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        private static int AsInt(object v)
        {
            if (v is null || v is DBNull) throw new InvalidOperationException("Očekáváno číselné ID, ale je NULL.");
            if (v is OracleDecimal od) return od.ToInt32();
            if (v is int i) return i;
            if (v is long l) return checked((int)l);
            if (v is decimal d) return (int)d;
            if (v is string s && int.TryParse(s, out var si)) return si;
            return Convert.ToInt32(v.ToString());
        }

        private static List<(int Id, string Name)> ReadIdName(IDataReader rd)
        {
            var list = new List<(int, string)>();
            while (rd.Read())
            {
                var id = AsInt(rd.GetValue(0));
                var name = Convert.ToString(rd.GetValue(1)) ?? "";
                list.Add((id, name));
            }
            return list;
        }

        private async Task<int> GetCurrentUserIdAsync()
        {
            if (CurrentUserId > 0) return CurrentUserId;

            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Nelze zjistit přihlášeného uživatele.");

            await using var con = await _db.CreateOpenAsync();
            await using var cmd = con.CreateCommand();
            (cmd as OracleCommand)!.BindByName = true;
            cmd.CommandText = "SELECT u.USERID FROM \"USER\" u WHERE UPPER(u.EMAIL)=UPPER(:e)";
            (cmd as OracleCommand)!.Parameters.Add("e", OracleDbType.Varchar2, 100).Value = email;
            var res = await cmd.ExecuteScalarAsync();
            return AsInt(res);
        }

        private static async Task<int> GetOrCreateDeliveryMethodId(OracleConnection con, OracleTransaction tx, string name)
        {
            await using (var find = con.CreateCommand())
            {
                find.Transaction = tx;
                find.BindByName = true;
                find.CommandText = "SELECT ID FROM VW_DELIVERY_METHODS WHERE UPPER(NAME)=UPPER(:n)";
                find.Parameters.Add("n", OracleDbType.Varchar2, 200).Value = name;
                var r = await find.ExecuteScalarAsync();
                if (r != null && r != DBNull.Value) return AsInt(r);
            }

            await using (var add = con.CreateCommand())
            {
                add.Transaction = tx;
                add.BindByName = true;
                add.CommandType = CommandType.StoredProcedure;
                add.CommandText = "ST72861.PRC_DELIVERY_METHOD_CREATE";
                add.Parameters.Add("p_name", OracleDbType.Varchar2, 200).Value = name;
                add.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = "system";
                await add.ExecuteNonQueryAsync();
            }

            await using (var read = con.CreateCommand())
            {
                read.Transaction = tx;
                read.BindByName = true;
                read.CommandText = "SELECT MAX(DELIVERYMETHODID) FROM DELIVERY_METHOD WHERE UPPER(DELIVERYNAME)=UPPER(:n)";
                read.Parameters.Add("n", OracleDbType.Varchar2, 200).Value = name;
                var v = await read.ExecuteScalarAsync();
                return AsInt(v);
            }
        }

        private static async Task<int> ResolveEventOrganizationProductId(
            OracleConnection con, int eventTypeId, OracleTransaction tx)
        {
            await using (var cmd = con.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.BindByName = true;
                cmd.CommandText = @"
            SELECT p.productid
              FROM product p
              JOIN event_type et ON et.eventtypeid = :id
             WHERE UPPER(p.name) = UPPER('Organization: ' || et.eventname)";
                cmd.Parameters.Add("id", OracleDbType.Int32).Value = eventTypeId;

                var res = await cmd.ExecuteScalarAsync();
                if (res != null && res != DBNull.Value) return AsInt(res);
            }

            await using (var cmd2 = con.CreateCommand())
            {
                cmd2.Transaction = tx;
                cmd2.BindByName = true;
                cmd2.CommandText = @"SELECT productid FROM product WHERE UPPER(name) = 'EVENT ORGANIZATION'";
                var res2 = await cmd2.ExecuteScalarAsync();
                if (res2 != null && res2 != DBNull.Value) return AsInt(res2);
            }

            await using (var cmd3 = con.CreateCommand())
            {
                cmd3.Transaction = tx;
                cmd3.BindByName = true;
                cmd3.CommandText = @"SELECT MIN(productid) FROM product WHERE UPPER(name) LIKE 'ORGANIZATION%'";
                var res3 = await cmd3.ExecuteScalarAsync();
                if (res3 != null && res3 != DBNull.Value) return AsInt(res3);
            }

            await using (var cmd4 = con.CreateCommand())
            {
                cmd4.Transaction = tx;
                cmd4.CommandText = @"SELECT MIN(productid) FROM product";
                var res4 = await cmd4.ExecuteScalarAsync();
                if (res4 != null && res4 != DBNull.Value) return AsInt(res4);
            }

            throw new InvalidOperationException(
                "V tabulce PRODUCT není žádná položka. Přidej alespoň jeden produkt (např. 'Event Organization'), " +
                "aby šlo objednávku dokončit.");
        }


        [HttpGet]
        public async Task<IActionResult> OrderForm(int eventTypeId)
        {
            var userId = await GetCurrentUserIdAsync();

            var vm = new EventOrderVm
            {
                UserId = userId,
                EventTypeId = eventTypeId,
                EventDate = DateTime.Today.AddDays(3)
            };

            await using var con = (OracleConnection)await _db.CreateOpenAsync();
            await using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "SELECT ID, NAME FROM VW_SHOPS ORDER BY NAME";
                await using var rd = await cmd.ExecuteReaderAsync();
                vm.Shops = ReadIdName(rd);
            }

            return View("OrderForm", vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Order(EventOrderVm m)
        {
            var userId = await GetCurrentUserIdAsync();

            if (string.IsNullOrWhiteSpace(m.Street))
                ModelState.AddModelError(nameof(m.Street), "Ulice je povinná.");
            if (m.HouseNumber is null)
                ModelState.AddModelError(nameof(m.HouseNumber), "Číslo domu je povinné.");
            if (m.PostalCode is null)
                ModelState.AddModelError(nameof(m.PostalCode), "PSČ je povinné.");
            if (m.ShopId <= 0)
                ModelState.AddModelError(nameof(m.ShopId), "Vyberte prodejnu.");

            if (!ModelState.IsValid)
            {
                await using var reload = (OracleConnection)await _db.CreateOpenAsync();
                await using (var cmd = reload.CreateCommand())
                {
                    cmd.CommandText = "SELECT ID, NAME FROM VW_SHOPS ORDER BY NAME";
                    await using var rd = await cmd.ExecuteReaderAsync();
                    m.Shops = ReadIdName(rd);
                }
                return View("OrderForm", m);
            }

            await using var con = (OracleConnection)await _db.CreateOpenAsync();
            await using var tx = await con.BeginTransactionAsync();

            try
            {
                int addressId;
                await using (var cmdAddr = con.CreateCommand())
                {
                    cmdAddr.Transaction = (OracleTransaction)tx;
                    cmdAddr.BindByName = true;
                    cmdAddr.CommandType = CommandType.StoredProcedure;
                    cmdAddr.CommandText = "ST72861.PRC_CREATE_ADDRESS";
                    cmdAddr.Parameters.Add("p_postalcode", OracleDbType.Int32).Value = m.PostalCode;
                    cmdAddr.Parameters.Add("p_street", OracleDbType.Varchar2, 200).Value = m.Street!.Trim();
                    cmdAddr.Parameters.Add("p_housenumber", OracleDbType.Int32).Value = m.HouseNumber;
                    var o = new OracleParameter("o_address_id", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                    cmdAddr.Parameters.Add(o);
                    await cmdAddr.ExecuteNonQueryAsync();
                    addressId = AsInt(o.Value);
                }

                var deliveryMethodId = await GetOrCreateDeliveryMethodId(con, (OracleTransaction)tx, "On-site Event");

                var productId = await ResolveEventOrganizationProductId(con, m.EventTypeId, (OracleTransaction)tx);

                await using (var cmd = con.CreateCommand())
                {
                    cmd.Transaction = (OracleTransaction)tx;
                    cmd.BindByName = true;
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.CommandText = "ST72861.PRC_EVENT_ORDER_PLACE";

                    cmd.Parameters.Add("p_user_id", OracleDbType.Int32).Value = userId;
                    cmd.Parameters.Add("p_event_type_id", OracleDbType.Int32).Value = m.EventTypeId;
                    cmd.Parameters.Add("p_event_date", OracleDbType.Date).Value = m.EventDate;
                    cmd.Parameters.Add("p_deliverymethodid", OracleDbType.Int32).Value = deliveryMethodId;
                    cmd.Parameters.Add("p_shopid", OracleDbType.Int32).Value = m.ShopId;
                    cmd.Parameters.Add("p_addressid", OracleDbType.Int32).Value = addressId;
                    cmd.Parameters.Add("p_payment_type", OracleDbType.Varchar2, 5).Value = (m.PaymentType ?? "cash").ToLowerInvariant();
                    cmd.Parameters.Add("p_product_id", OracleDbType.Int32).Value = productId;
                    cmd.Parameters.Add("p_quantity", OracleDbType.Int32).Value = 1; 
                    cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100).Value = (User?.Identity?.Name ?? "web");

                    var oOrderNo = new OracleParameter("o_order_no", OracleDbType.Varchar2, 50) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(oOrderNo);

                    await cmd.ExecuteNonQueryAsync();
                    TempData["OrderNo"] = oOrderNo.Value?.ToString();
                }

                await tx.CommitAsync();
                return RedirectToAction("EventOrderSuccess");
            }
            catch (OracleException ex)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "Chyba: " + ex.Message);

                await using var reload = (OracleConnection)await _db.CreateOpenAsync();
                await using (var cmd = reload.CreateCommand())
                {
                    cmd.CommandText = "SELECT ID, NAME FROM VW_SHOPS ORDER BY NAME";
                    await using var rd = await cmd.ExecuteReaderAsync();
                    m.Shops = ReadIdName(rd);
                }
                return View("OrderForm", m);
            }
        }

        [HttpGet]
        public IActionResult EventOrderSuccess()
        {
            ViewBag.OrderNo = TempData["OrderNo"] as string;
            return View();
        }
    }
}
