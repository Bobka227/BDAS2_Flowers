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
            // TODO VIEW
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
                // TODO VIEW
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
                // TODO VIEW
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
                cmd2.CommandText = @"SELECT productid FROM product WHERE UPPER(name) = 'EVENT ORGANIZATION'";// TODO VIEW
                var res2 = await cmd2.ExecuteScalarAsync();
                if (res2 != null && res2 != DBNull.Value) return AsInt(res2);
            }

            await using (var cmd3 = con.CreateCommand())
            {
                cmd3.Transaction = tx;
                cmd3.BindByName = true;
                cmd3.CommandText = @"SELECT MIN(productid) FROM product WHERE UPPER(name) LIKE 'ORGANIZATION%'";// TODO VIEW
                var res3 = await cmd3.ExecuteScalarAsync();
                if (res3 != null && res3 != DBNull.Value) return AsInt(res3);
            }

            await using (var cmd4 = con.CreateCommand())
            {
                cmd4.Transaction = tx;
                cmd4.CommandText = @"SELECT MIN(productid) FROM product";// TODO VIEW
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

            await using (var cmd = con.CreateCommand())
            {
                cmd.BindByName = true;
              
                cmd.CommandText = @"
            SELECT PRODUCTID, TITLE, SUBTITLE, PRICEFROM
              FROM VW_CATALOG_PRODUCTS
             WHERE TITLE NOT LIKE '~~ARCHIVED~~ %'
             ORDER BY PRICEFROM DESC
             FETCH FIRST 12 ROWS ONLY";

                await using var r = await cmd.ExecuteReaderAsync();
                var products = new List<EventProductChoiceVm>();
                int index = 0;
                while (await r.ReadAsync())
                {
                    products.Add(new EventProductChoiceVm
                    {
                        ProductId = AsInt(r.GetValue(0)),
                        Title = Convert.ToString(r.GetValue(1)) ?? "",
                        Subtitle = Convert.ToString(r.GetValue(2)),
                        PriceFrom = r.IsDBNull(3) ? 0 : (decimal)r.GetDecimal(3),
                        Recommended = index < 4,
                        Quantity = 0
                    });
                    index++;
                }

                vm.Products = products;
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
            if (m.Quantity is null || m.Quantity < 1)
                ModelState.AddModelError(nameof(m.Quantity), "Počet balíčků musí být alespoň 1.");

            var payType = (m.PaymentType ?? "card").ToLowerInvariant();

            if (payType == "card")
            {
                if (string.IsNullOrWhiteSpace(m.CardNumber))
                {
                    ModelState.AddModelError(nameof(m.CardNumber), "Zadejte číslo karty.");
                }
                else
                {
                    var digits = new string(m.CardNumber.Where(char.IsDigit).ToArray());
                    if (digits.Length < 12 || digits.Length > 19)
                    {
                        ModelState.AddModelError(nameof(m.CardNumber),
                            "Číslo karty musí mít 12–19 číslic.");
                    }
                }
            }
            else if (payType == "cupon")
            {
                if (string.IsNullOrWhiteSpace(m.CouponCode))
                {
                    ModelState.AddModelError(nameof(m.CouponCode), "Zadejte kód kuponu.");
                }
            }

            if (!ModelState.IsValid)
            {
                await using var reload = (OracleConnection)await _db.CreateOpenAsync();

                await using (var cmd = reload.CreateCommand())
                {
                    cmd.CommandText = "SELECT ID, NAME FROM VW_SHOPS ORDER BY NAME";
                    await using var rd = await cmd.ExecuteReaderAsync();
                    m.Shops = ReadIdName(rd);
                }

                await using (var cmd = reload.CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT PRODUCTID, TITLE, SUBTITLE, PRICEFROM
                  FROM VW_CATALOG_PRODUCTS
                 WHERE TITLE NOT LIKE '~~ARCHIVED~~ %'
                 ORDER BY PRICEFROM DESC
                 FETCH FIRST 12 ROWS ONLY";

                    await using var r = await cmd.ExecuteReaderAsync();
                    var products = new List<EventProductChoiceVm>();
                    int index = 0;
                    while (await r.ReadAsync())
                    {
                        products.Add(new EventProductChoiceVm
                        {
                            ProductId = AsInt(r.GetValue(0)),
                            Title = Convert.ToString(r.GetValue(1)) ?? "",
                            Subtitle = Convert.ToString(r.GetValue(2)),
                            PriceFrom = r.IsDBNull(3) ? 0 : (decimal)r.GetDecimal(3),
                            Recommended = index < 4,
                            Quantity = 0
                        });
                        index++;
                    }
                    m.Products = products;
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
                    var o = new OracleParameter("o_address_id", OracleDbType.Int32)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmdAddr.Parameters.Add(o);
                    await cmdAddr.ExecuteNonQueryAsync();
                    addressId = AsInt(o.Value);
                }

                var deliveryMethodId = await GetOrCreateDeliveryMethodId(
                    con, (OracleTransaction)tx, "On-site Event");

                var baseProductId = await ResolveEventOrganizationProductId(
                    con, m.EventTypeId, (OracleTransaction)tx);

                string? orderNo;
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
                    cmd.Parameters.Add("p_payment_type", OracleDbType.Varchar2, 5).Value = payType;
                    cmd.Parameters.Add("p_product_id", OracleDbType.Int32).Value = baseProductId;
                    cmd.Parameters.Add("p_quantity", OracleDbType.Int32).Value = m.Quantity;
                    cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 100)
                        .Value = (User?.Identity?.Name ?? "web");

                    var oOrderNo = new OracleParameter("o_order_no", OracleDbType.Varchar2, 50)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmd.Parameters.Add(oOrderNo);

                    await cmd.ExecuteNonQueryAsync();
                    orderNo = oOrderNo.Value?.ToString();
                    TempData["OrderNo"] = orderNo;
                    TempData["EventTypeId"] = m.EventTypeId;
                }

                if (string.IsNullOrWhiteSpace(orderNo))
                    throw new InvalidOperationException("Nepodařilo se získat kód objednávky.");

                int orderId;
                await using (var cmdOrderId = con.CreateCommand())
                {
                    cmdOrderId.Transaction = (OracleTransaction)tx;
                    cmdOrderId.BindByName = true;
                    cmdOrderId.CommandText = "SELECT ST72861.PKG_ORDER_CODE.TRY_GET_ID(:p) FROM dual";
                    cmdOrderId.Parameters.Add("p", OracleDbType.Varchar2, 50).Value = orderNo!;
                    var res = await cmdOrderId.ExecuteScalarAsync();
                    orderId = AsInt(res);
                }

                if (orderId <= 0)
                    throw new InvalidOperationException("Neplatné interní ID objednávky.");

                await using (var cmdBase = new OracleCommand("ST72861.PRC_ADD_ITEM", con))
                {
                    cmdBase.Transaction = (OracleTransaction)tx;
                    cmdBase.CommandType = CommandType.StoredProcedure;
                    cmdBase.BindByName = true;

                    cmdBase.Parameters.Add("p_order_id", OracleDbType.Int32).Value = orderId;
                    cmdBase.Parameters.Add("p_productid", OracleDbType.Int32).Value = baseProductId;
                    cmdBase.Parameters.Add("p_quantity", OracleDbType.Int32).Value = m.Quantity;

                    await cmdBase.ExecuteNonQueryAsync();
                }

                var selectedProducts = (m.Products ?? new List<EventProductChoiceVm>())
                    .Where(p => p.Quantity > 0)
                    .ToList();

                if (selectedProducts.Any())
                {
                    foreach (var p in selectedProducts)
                    {
                        await using var cmdAdd = new OracleCommand("ST72861.PRC_ADD_ITEM", con)
                        {
                            Transaction = (OracleTransaction)tx,
                            CommandType = CommandType.StoredProcedure,
                            BindByName = true
                        };
                        cmdAdd.Parameters.Add("p_order_id", OracleDbType.Int32).Value = orderId;
                        cmdAdd.Parameters.Add("p_productid", OracleDbType.Int32).Value = p.ProductId;
                        cmdAdd.Parameters.Add("p_quantity", OracleDbType.Int32).Value = p.Quantity;

                        await cmdAdd.ExecuteNonQueryAsync();
                    }
                }

                await using (var cmdRecalc = new OracleCommand("ST72861.PRC_FINALIZE_ORDER_XCUR", con))
                {
                    cmdRecalc.Transaction = (OracleTransaction)tx;
                    cmdRecalc.CommandType = CommandType.StoredProcedure;
                    cmdRecalc.BindByName = true;
                    cmdRecalc.Parameters.Add("p_order_id", OracleDbType.Int32).Value = orderId;
                    await cmdRecalc.ExecuteNonQueryAsync();
                }

                int? cardLast4 = null;
                if (payType == "card" && !string.IsNullOrWhiteSpace(m.CardNumber))
                {
                    var digits = new string(m.CardNumber.Where(char.IsDigit).ToArray());
                    var last4Str = digits.Length >= 4
                        ? digits.Substring(digits.Length - 4)
                        : digits;
                    if (int.TryParse(last4Str, out var last4Int))
                        cardLast4 = last4Int;
                }

                await using (var cmdPay = con.CreateCommand())
                {
                    cmdPay.Transaction = (OracleTransaction)tx;
                    cmdPay.BindByName = true;
                    cmdPay.CommandType = CommandType.StoredProcedure;
                    cmdPay.CommandText = "ST72861.PRC_EVENT_PAYMENT_EXTEND";

                    cmdPay.Parameters.Add("p_order_no", OracleDbType.Varchar2, 50).Value = orderNo!;
                    cmdPay.Parameters.Add("p_payment_type", OracleDbType.Varchar2, 20).Value = payType;
                    cmdPay.Parameters.Add("p_coupon_code", OracleDbType.Varchar2, 100)
                        .Value = (object?)m.CouponCode ?? DBNull.Value;
                    cmdPay.Parameters.Add("p_card_last4", OracleDbType.Int32)
                        .Value = cardLast4.HasValue ? (object)cardLast4.Value : DBNull.Value;
                    cmdPay.Parameters.Add("p_cash_accepted", OracleDbType.Int32)
                        .Value = DBNull.Value;

                    var oStatus = new OracleParameter("o_status", OracleDbType.Int32)
                    {
                        Direction = ParameterDirection.Output
                    };
                    cmdPay.Parameters.Add(oStatus);

                    await cmdPay.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
                return RedirectToAction(nameof(EventOrderSuccess));
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

                await using (var cmd = reload.CreateCommand())
                {
                    cmd.CommandText = @"
                SELECT PRODUCTID, TITLE, SUBTITLE, PRICEFROM
                  FROM VW_CATALOG_PRODUCTS
                 WHERE TITLE NOT LIKE '~~ARCHIVED~~ %'
                 ORDER BY PRICEFROM DESC
                 FETCH FIRST 12 ROWS ONLY";

                    await using var r = await cmd.ExecuteReaderAsync();
                    var products = new List<EventProductChoiceVm>();
                    int index = 0;
                    while (await r.ReadAsync())
                    {
                        products.Add(new EventProductChoiceVm
                        {
                            ProductId = AsInt(r.GetValue(0)),
                            Title = Convert.ToString(r.GetValue(1)) ?? "",
                            Subtitle = Convert.ToString(r.GetValue(2)),
                            PriceFrom = r.IsDBNull(3) ? 0 : (decimal)r.GetDecimal(3),
                            Recommended = index < 4,
                            Quantity = 0
                        });
                        index++;
                    }
                    m.Products = products;
                }

                return View("OrderForm", m);
            }
        }

        [HttpGet]
        public IActionResult EventOrderSuccess()
        {
            ViewData["OrderNo"] = TempData["OrderNo"];
            ViewData["EventTypeId"] = TempData["EventTypeId"];

            return View(); 
        }
    }
}