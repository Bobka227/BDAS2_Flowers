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
    /// <summary>
    /// Controller pro zadání a vytvoření objednávky na akci (event).
    /// Zajišťuje výběr typu akce, služeb, adresy a vytvoření objednávky včetně položek.
    /// </summary>
    [Authorize]
    public class OrderEventController : Controller
    {
        private readonly IDbFactory _db;

        /// <summary>
        /// Inicializuje novou instanci <see cref="OrderEventController"/> s továrnou databázových připojení.
        /// </summary>
        /// <param name="db">Továrna pro vytváření a otevírání databázových připojení.</param>
        public OrderEventController(IDbFactory db) => _db = db;

        /// <summary>
        /// Vrací ID aktuálně přihlášeného uživatele z claimu <see cref="ClaimTypes.NameIdentifier"/>.
        /// Pokud se ID nepodaří převést, vrací 0.
        /// </summary>
        private int CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        /// <summary>
        /// Pomocná metoda pro bezpečnou konverzi objektu na <see cref="int"/>,
        /// podporuje různé typy (OracleDecimal, int, long, decimal, string).
        /// </summary>
        /// <param name="v">Hodnota načtená z databáze.</param>
        /// <returns>Celé číslo reprezentující ID.</returns>
        /// <exception cref="InvalidOperationException">Pokud je hodnota <c>null</c> nebo <see cref="DBNull"/>.</exception>
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

        /// <summary>
        /// Načte dvojice (Id, Název) z předaného <see cref="IDataReader"/> a vrátí je jako kolekci.
        /// Očekává se, že první sloupec je ID a druhý sloupec je název.
        /// </summary>
        /// <param name="rd">Otevřený data reader.</param>
        /// <returns>Seznam dvojic (Id, Název).</returns>
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

        /// <summary>
        /// Získá ID aktuálního uživatele. Pokud není k dispozici v claimu,
        /// dohledá ho podle e-mailu uživatele ve <c>VW_USERS_SECURITY</c>.
        /// </summary>
        /// <returns>Číselné ID uživatele.</returns>
        /// <exception cref="InvalidOperationException">Pokud nelze zjistit e-mail přihlášeného uživatele.</exception>
        private async Task<int> GetCurrentUserIdAsync()
        {
            if (CurrentUserId > 0) return CurrentUserId;

            var email = User.Identity?.Name;
            if (string.IsNullOrWhiteSpace(email))
                throw new InvalidOperationException("Nelze zjistit přihlášeného uživatele.");

            await using var con = await _db.CreateOpenAsync();
            await using var cmd = con.CreateCommand();
            var oc = (OracleCommand)cmd;
            oc.BindByName = true;

            oc.CommandText = @"
                  SELECT USERID
                  FROM VW_USERS_SECURITY
                  WHERE UPPER(EMAIL) = UPPER(:e)";

            oc.Parameters.Add("e", OracleDbType.Varchar2, 100).Value = email;

            var res = await oc.ExecuteScalarAsync();
            return AsInt(res);
        }

        /// <summary>
        /// Získá ID způsobu doručení podle názvu z pohledu <c>VW_DELIVERY_METHODS</c>.
        /// Pokud neexistuje, vytvoří nový záznam pomocí procedury <c>ST72861.PRC_DELIVERY_METHOD_CREATE</c>.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Aktuální databázová transakce.</param>
        /// <param name="name">Název způsobu doručení.</param>
        /// <returns>ID existujícího nebo nově vytvořeného způsobu doručení.</returns>
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
                read.CommandText = @"
                    SELECT MAX(ID)
                      FROM VW_DELIVERY_METHODS
                     WHERE UPPER(NAME) = UPPER(:n)";
                read.Parameters.Add("n", OracleDbType.Varchar2, 200).Value = name;
                var v = await read.ExecuteScalarAsync();
                return AsInt(v);
            }
        }

        /// <summary>
        /// Najde produkt reprezentující organizační poplatek akce
        /// (název <c>Organizace akce (poplatek)</c>) a vrátí jeho ID.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="eventTypeId">ID typu akce (momentálně se v dotazu nepoužívá).</param>
        /// <param name="tx">Aktuální databázová transakce.</param>
        /// <returns>ID produktu pro organizační poplatek.</returns>
        /// <exception cref="InvalidOperationException">
        /// Pokud produkt s názvem <c>Organizace akce (poplatek)</c> neexistuje.
        /// </exception>
        private static async Task<int> ResolveEventOrganizationProductId(
            OracleConnection con, int eventTypeId, OracleTransaction tx)
        {
            await using var cmd = con.CreateCommand();
            cmd.Transaction = tx;
            cmd.BindByName = true;

            cmd.CommandText = @"
                SELECT PRODUCTID
                  FROM VW_PRODUCT_EDIT
                 WHERE UPPER(NAME) = UPPER('Organizace akce (poplatek)')";

            var res = await cmd.ExecuteScalarAsync();
            if (res != null && res != DBNull.Value)
                return AsInt(res);

            throw new InvalidOperationException(
                "Nenalezen produkt 'Organizace akce (poplatek)'. " +
                "Vytvoř ho v katalogu produktů a nastav správně, jinak nelze objednávku akce dokončit.");
        }

        /// <summary>
        /// Zobrazí formulář pro objednání akce daného typu včetně seznamu prodejen a nabízených služeb.
        /// </summary>
        /// <param name="eventTypeId">ID typu akce, pro kterou se objednávka vytváří.</param>
        /// <returns>View <c>OrderForm</c> s naplněným modelem <see cref="EventOrderVm"/>.</returns>
        [HttpGet]
        public async Task<IActionResult> OrderForm(int eventTypeId)
        {
            var userId = await GetCurrentUserIdAsync();

            var vm = new EventOrderVm
            {
                UserId = userId,
                EventTypeId = eventTypeId,
                EventDate = DateTime.Today.AddDays(3),
                Quantity = 1
            };

            await using var con = (OracleConnection)await _db.CreateOpenAsync();

            // Prodejny
            await using (var cmd = con.CreateCommand())
            {
                cmd.CommandText = "SELECT ID, NAME FROM VW_SHOPS ORDER BY NAME";
                await using var rd = await cmd.ExecuteReaderAsync();
                vm.Shops = ReadIdName(rd);
            }

            // Nabídka služeb pro akce
            await using (var cmd = con.CreateCommand())
            {
                cmd.BindByName = true;

                cmd.CommandText = @"
                    SELECT PRODUCTID, TITLE, SUBTITLE, PRICEFROM
                      FROM VW_EVENT_SERVICES
                     ORDER BY PRICEFROM DESC";

                await using var r = await cmd.ExecuteReaderAsync();
                var products = new List<EventProductChoiceVm>();
                int index = 0;
                while (await r.ReadAsync())
                {
                    products.Add(new EventProductChoiceVm
                    {
                        ProductId = AsInt(r.GetValue(0)),
                        Title = Convert.ToString(r.GetValue(1)) ?? "",
                        Subtitle = Convert.ToString(r.GetValue(2)) ?? "",
                        PriceFrom = r.IsDBNull(3) ? 0 : (decimal)r.GetDecimal(3),
                        Recommended = index < 3,
                        Quantity = 0
                    });
                    index++;
                }

                vm.Products = products;
            }

            return View("OrderForm", vm);
        }

        /// <summary>
        /// Zpracuje odeslaný formulář pro objednávku akce.
        /// Provede validaci, založí adresu, vytvoří objednávku, přidá položky
        /// a provede finalizaci objednávky v rámci jedné transakce.
        /// </summary>
        /// <param name="m">Model s údaji z formuláře pro objednávku akce.</param>
        /// <returns>
        /// V případě úspěchu přesměrování na <see cref="EventOrderSuccess"/>,
        /// jinak zobrazení formuláře s chybami.
        /// </returns>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Order(EventOrderVm m)
        {
            var userId = await GetCurrentUserIdAsync();
            m.Quantity = 1;

            // Základní validace adresy a prodejny
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
                // Při chybách je potřeba znovu naplnit lookupy (prodejny, služby)
                await using var reload = (OracleConnection)await _db.CreateOpenAsync();

                await using (var cmd = reload.CreateCommand())
                {
                    cmd.CommandText = "SELECT ID, NAME FROM VW_SHOPS ORDER BY NAME";
                    await using var rd = await cmd.ExecuteReaderAsync();
                    m.Shops = ReadIdName(rd);
                }

                await using (var cmd = reload.CreateCommand())
                {
                    cmd.BindByName = true;
                    cmd.CommandText = @"
                        SELECT PRODUCTID, TITLE, SUBTITLE, PRICEFROM
                          FROM VW_EVENT_SERVICES
                         ORDER BY PRICEFROM DESC";

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
                            Recommended = index < 3,
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
                // 1) Vytvoření adresy
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

                // 2) Způsob doručení a základní produkt
                var deliveryMethodId = await GetOrCreateDeliveryMethodId(
                    con, (OracleTransaction)tx, "On-site Event");

                var baseProductId = await ResolveEventOrganizationProductId(
                    con, m.EventTypeId, (OracleTransaction)tx);

                // 3) Vytvoření objednávky
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

                    cmd.Parameters.Add("p_payment_type", OracleDbType.Varchar2, 5).Value = "none";

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

                // 4) Získání interního ID objednávky
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

                // 5) Přidání základní položky objednávky (organizační poplatek)
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

                // 6) Přidání vybraných doplňkových služeb
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

                // 7) Finální přepočet objednávky
                await using (var cmdRecalc = new OracleCommand("ST72861.PRC_FINALIZE_ORDER_XCUR", con))
                {
                    cmdRecalc.Transaction = (OracleTransaction)tx;
                    cmdRecalc.CommandType = CommandType.StoredProcedure;
                    cmdRecalc.BindByName = true;
                    cmdRecalc.Parameters.Add("p_order_id", OracleDbType.Int32).Value = orderId;
                    await cmdRecalc.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
                return RedirectToAction(nameof(EventOrderSuccess));
            }
            catch (OracleException ex)
            {
                await tx.RollbackAsync();
                ModelState.AddModelError("", "Chyba: " + ex.Message);

                // Při chybě je opět nutné naplnit lookupy, aby se formulář dal znovu zobrazit
                await using var reload = (OracleConnection)await _db.CreateOpenAsync();

                await using (var cmd = reload.CreateCommand())
                {
                    cmd.CommandText = "SELECT ID, NAME FROM VW_SHOPS ORDER BY NAME";
                    await using var rd = await cmd.ExecuteReaderAsync();
                    m.Shops = ReadIdName(rd);
                }

                await using (var cmd = reload.CreateCommand())
                {
                    cmd.BindByName = true;
                    cmd.CommandText = @"
                        SELECT PRODUCTID, TITLE, SUBTITLE, PRICEFROM
                          FROM VW_EVENT_SERVICES
                         ORDER BY PRICEFROM DESC";

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
                            Recommended = index < 3,
                            Quantity = 0
                        });
                        index++;
                    }
                    m.Products = products;
                }

                return View("OrderForm", m);
            }
        }

        /// <summary>
        /// Zobrazí stránku s potvrzením objednávky akce.
        /// Využívá hodnoty uložené v <see cref="TempData"/> (číslo objednávky, ID typu akce).
        /// </summary>
        /// <returns>View s informací o úspěšném vytvoření objednávky.</returns>
        [HttpGet]
        public IActionResult EventOrderSuccess()
        {
            ViewData["OrderNo"] = TempData["OrderNo"];
            ViewData["EventTypeId"] = TempData["EventTypeId"];
            ViewData["OrderId"] = TempData["OrderId"];

            return View();
        }

    }
}
