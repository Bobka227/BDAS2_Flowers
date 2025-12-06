using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.Security.Claims;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Models.ViewModels.OrderModels;
using Microsoft.Extensions.Logging;

namespace BDAS2_Flowers.Controllers.OrderControllers
{
    /// <summary>
    /// Controller pro vytváření a zobrazení objednávek zákazníků.
    /// Zajišťuje validaci vstupu, volání PL/SQL procedur a integraci s platební službou.
    /// </summary>
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly IPaymentService _payments;
        private readonly IDbFactory _db;

        /// <summary>
        /// Inicializuje novou instanci <see cref="OrdersController"/> se službou plateb a továrnou DB připojení.
        /// </summary>
        /// <param name="db">Továrna databázových připojení pro práci s Oracle DB.</param>
        /// <param name="payments">Služba pro vytváření a aktualizaci plateb (hotově / karta / kupón).</param>
        public OrdersController(IDbFactory db, IPaymentService payments)
        { _db = db; _payments = payments; }

        /// <summary>
        /// Klíč pro uložení košíku v session pro aktuálního uživatele (nebo anonymního).
        /// </summary>
        private string CartKey
        {
            get
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!string.IsNullOrEmpty(userId))
                    return $"CART_USER_{userId}";

                return "CART_ANON";
            }
        }

        /// <summary>
        /// Vrací číselné ID aktuálně přihlášeného uživatele.
        /// Pokud jej nelze z claimů načíst, vrací 0.
        /// </summary>
        private int CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        /// <summary>
        /// Zobrazí formulář pro vytvoření nové objednávky.
        /// Načte číselníky (doprava, prodejny, adresy, produkty) a předvyplní položky z košíku.
        /// </summary>
        /// <returns>View s modelem <see cref="OrderCreateVm"/>.</returns>
        [HttpGet("/orders/create")]
        public async Task<IActionResult> Create()
        {
            var vm = new OrderCreateVm
            {
                UserId = CurrentUserId,
                Items = new List<OrderItemVm>()
            };

            vm.DeliveryMethods = await LoadIdNameAsync(
                @"SELECT ID, NAME FROM VW_DELIVERY_METHODS WHERE UPPER(NAME) <> 'ON-SITE EVENT'");
            vm.Shops = await LoadIdNameAsync(@"SELECT ID, NAME FROM VW_SHOPS");
            vm.Addresses = await LoadIdNameAsync(@"
                SELECT ADDRESSID AS ID,
                       STREET || ' ' || HOUSENUMBER || ', ' || POSTALCODE AS NAME
                FROM VW_ADMIN_ADDRESSES");
            vm.Products = await LoadIdNameAsync(@"
                SELECT PRODUCTID AS ID,
                       TITLE     AS NAME
                  FROM VW_CATALOG_PRODUCTS");

            var cart = HttpContext.Session.GetJson<CartVm>(CartKey);
            if (cart != null && cart.Items.Any())
            {
                vm.Items = cart.Items
                    .Select(i => new OrderItemVm
                    {
                        ProductId = i.ProductId,
                        Quantity = i.Quantity,
                        UnitPrice = i.UnitPrice
                    })
                    .ToList();

                vm.CartTotal = cart.Items.Sum(i => i.UnitPrice * i.Quantity);
            }
            else
            {
                vm.Items.Add(new OrderItemVm { Quantity = 1, UnitPrice = 0m });
                vm.CartTotal = 0m;
            }

            return View(vm);
        }

        /// <summary>
        /// Zpracuje odeslaný formulář pro vytvoření objednávky.
        /// Provede validaci, vytvoří adresu, platbu, objednávku a položky a následně objednávku finalizuje.
        /// </summary>
        /// <param name="vm">ViewModel s údaji o objednávce a platebních údajích.</param>
        /// <returns>
        /// Při chybě vrací view s validací a aktuálními číselníky,
        /// při úspěchu přesměruje na detail objednávky.
        /// </returns>
        [ValidateAntiForgeryToken]
        [HttpPost("/orders/create")]
        public async Task<IActionResult> Create(OrderCreateVm vm)
        {
            var errors = new List<string>();

            // Validace adresy
            if (vm.UseNewAddress)
            {
                if (string.IsNullOrWhiteSpace(vm.Street) || vm.HouseNumber <= 0 || vm.PostalCode <= 0)
                    errors.Add("Vyplňte prosím ulici, číslo domu a PSČ.");
            }
            else
            {
                if (vm.AddressId is null || vm.AddressId <= 0)
                    errors.Add("Vyberte prosím existující adresu.");
            }

            var pt = (vm.PaymentType ?? "").ToLowerInvariant();

            // Validace platebních údajů
            if (pt == "card")
            {
                if (string.IsNullOrWhiteSpace(vm.CardNumber) || vm.CardNumber.Count(char.IsDigit) < 12)
                    errors.Add("Zadejte platné číslo karty (min. 12 číslic).");
            }
            else if (pt == "cash")
            {
                if (!vm.CashAccepted.HasValue || vm.CashAccepted <= 0)
                    errors.Add("Zadejte přijatou hotovost.");
            }
            else if (pt == "cupon")
            {
                if (string.IsNullOrWhiteSpace(vm.CuponCode))
                    errors.Add("Zadejte kód kupónu.");
            }

            // Validace položek objednávky
            if (vm.Items == null || vm.Items.Count == 0 ||
                vm.Items.Any(i => i.ProductId <= 0 || i.Quantity <= 0))
            {
                errors.Add("Přidejte alespoň jednu platnou položku.");
            }

            if (errors.Any())
            {
                vm.DeliveryMethods = await LoadIdNameAsync(@"SELECT ID, NAME FROM VW_DELIVERY_METHODS WHERE UPPER(NAME) <> 'ON-SITE EVENT'");
                vm.Shops = await LoadIdNameAsync(@"SELECT ID, NAME FROM VW_SHOPS");
                vm.Addresses = await LoadIdNameAsync(@"
                    SELECT ADDRESSID AS ID,
                           STREET || ' ' || HOUSENUMBER || ', ' || POSTALCODE AS NAME
                      FROM VW_ADMIN_ADDRESSES");
                vm.Products = await LoadIdNameAsync(@"
                    SELECT PRODUCTID AS ID,
                           TITLE     AS NAME
                      FROM VW_CATALOG_PRODUCTS");

                vm.CartTotal = vm.Items?
                    .Where(i => i.ProductId > 0 && i.Quantity > 0)
                    .Sum(i => i.UnitPrice * i.Quantity) ?? 0m;

                TempData["OrderError"] = errors.First();
                return View(vm);
            }

            await using var con = await _db.CreateOpenAsync();
            await using var tx = con.BeginTransaction();

            try
            {
                var pendingId = await GetPendingStatusIdAsync(con);
                var rawType = pt;

                // Vytvoření záznamu platby
                var paymentId = await _payments.CreatePaymentAsync(con, tx, CurrentUserId, rawType);

                if (rawType == "card" && !string.IsNullOrWhiteSpace(vm.CardNumber))
                {
                    await _payments.AttachCardAsync(con, tx, paymentId, vm.CardNumber!);
                }
                else if (rawType == "cash")
                {
                    await _payments.AttachCashAsync(con, tx, paymentId, vm.CashAccepted ?? 0m);
                }

                // Rozhodnutí, zda použít novou nebo existující adresu
                int addressId;
                var wantsNewAddress =
                    vm.UseNewAddress ||
                    (!string.IsNullOrWhiteSpace(vm.Street) && vm.HouseNumber > 0 && vm.PostalCode > 0);

                if (wantsNewAddress)
                {
                    await using (var cmd = new OracleCommand("PRC_CREATE_ADDRESS", con)
                    { CommandType = CommandType.StoredProcedure, Transaction = tx })
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_postalcode", OracleDbType.Int32).Value = vm.PostalCode;
                        cmd.Parameters.Add("p_street", OracleDbType.Varchar2, 200).Value = vm.Street.Trim();
                        cmd.Parameters.Add("p_housenumber", OracleDbType.Int32).Value = vm.HouseNumber;
                        var oAddr = new OracleParameter("o_address_id", OracleDbType.Int32)
                        { Direction = ParameterDirection.Output };
                        cmd.Parameters.Add(oAddr);
                        await cmd.ExecuteNonQueryAsync();
                        addressId = Convert.ToInt32(oAddr.Value.ToString());
                    }
                }
                else
                {
                    addressId = vm.AddressId!.Value;
                }

                // Vytvoření hlavičky objednávky
                int orderId;
                await using (var cmd = new OracleCommand("PRC_CREATE_ORDER", con)
                { CommandType = CommandType.StoredProcedure, Transaction = tx })
                {
                    cmd.Parameters.Add("p_user_id", OracleDbType.Int32).Value = CurrentUserId;
                    cmd.Parameters.Add("p_deliverymethodid", OracleDbType.Int32).Value = vm.DeliveryMethodId;
                    cmd.Parameters.Add("p_statusid", OracleDbType.Int32).Value = pendingId;
                    cmd.Parameters.Add("p_shopid", OracleDbType.Int32).Value = vm.ShopId;
                    cmd.Parameters.Add("p_addressid", OracleDbType.Int32).Value = addressId;
                    cmd.Parameters.Add("p_paymentid", OracleDbType.Int32).Value = paymentId;
                    var o = new OracleParameter("o_order_id", OracleDbType.Int32)
                    { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(o);
                    await cmd.ExecuteNonQueryAsync();
                    orderId = Convert.ToInt32(o.Value.ToString());
                }

                // Vložení položek objednávky
                foreach (var it in vm.Items)
                {
                    await using var cmd = new OracleCommand("PRC_ADD_ITEM", con)
                    { CommandType = CommandType.StoredProcedure, Transaction = tx };
                    cmd.Parameters.Add("p_order_id", OracleDbType.Int32).Value = orderId;
                    cmd.Parameters.Add("p_productid", OracleDbType.Int32).Value = it.ProductId;
                    cmd.Parameters.Add("p_quantity", OracleDbType.Int32).Value = it.Quantity;
                    await cmd.ExecuteNonQueryAsync();
                }

                // Přepočet a finalizace objednávky
                await using (var cmd = new OracleCommand("PRC_FINALIZE_ORDER_XCUR", con)
                { CommandType = CommandType.StoredProcedure, Transaction = tx })
                {
                    cmd.Parameters.Add("p_order_id", OracleDbType.Int32).Value = orderId;
                    await cmd.ExecuteNonQueryAsync();
                }

                // Zpracování kupónu (pokud je použit)
                if (rawType == "cupon" && !string.IsNullOrWhiteSpace(vm.CuponCode))
                {
                    var status = await ApplyCouponAsync(con, tx, paymentId, vm.CuponCode!);

                    if (status != 0)
                    {
                        await tx.RollbackAsync();

                        vm.DeliveryMethods = await LoadIdNameAsync(@"SELECT ID, NAME FROM VW_DELIVERY_METHODS");
                        vm.Shops = await LoadIdNameAsync(@"SELECT ID, NAME FROM VW_SHOPS");
                        vm.Addresses = await LoadIdNameAsync(@"
                            SELECT ADDRESSID AS ID,
                                   STREET || ' ' || HOUSENUMBER || ', ' || POSTALCODE AS NAME
                              FROM VW_ADMIN_ADDRESSES");
                        vm.Products = await LoadIdNameAsync(@"
                            SELECT PRODUCTID AS ID,
                                   TITLE     AS NAME
                              FROM VW_CATALOG_PRODUCTS");

                        TempData["OrderError"] = status switch
                        {
                            1 => "Zadaný kupón neexistuje.",
                            2 => "Platnost kupónu již vypršela.",
                            3 => "Celková cena objednávky nesmí být vyšší než hodnota kupónu.",
                            _ => "Chyba při použití kupónu."
                        };

                        return View(vm);
                    }
                }

                // Kontrola a výpočet vrácené hotovosti
                if (rawType == "cash")
                {
                    var amount = await _payments.GetAmountAsync(con, tx, paymentId);
                    var accepted = vm.CashAccepted ?? 0m;

                    if (accepted < amount)
                    {
                        await tx.RollbackAsync();

                        vm.DeliveryMethods = await LoadIdNameAsync(@"SELECT ID, NAME FROM VW_DELIVERY_METHODS");
                        vm.Shops = await LoadIdNameAsync(@"SELECT ID, NAME FROM VW_SHOPS");
                        vm.Addresses = await LoadIdNameAsync(@"
                            SELECT ADDRESSID AS ID,
                                   STREET || ' ' || HOUSENUMBER || ', ' || POSTALCODE AS NAME
                              FROM VW_ADMIN_ADDRESSES");
                        vm.Products = await LoadIdNameAsync(@"
                            SELECT PRODUCTID AS ID,
                                   TITLE     AS NAME
                              FROM VW_CATALOG_PRODUCTS");

                        TempData["OrderError"] =
                            "Přijatá hotovost nesmí být menší než cena objednávky.";
                        return View(vm);
                    }

                    var change = accepted - amount;
                    await _payments.SetCashChangeAsync(con, tx, paymentId, change);
                }

                await tx.CommitAsync();
                HttpContext.Session.Remove(CartKey);
                TempData["OrderOk"] = "Objednávka byla vytvořena.";
                return Redirect($"/orders/{orderId}");
            }
            catch (OracleException ex)
            {
                await tx.RollbackAsync();

                string userMsg;

                switch (ex.Number)
                {
                    case 20502:
                        userMsg = "Tento uživatel již daný kupón použil. Každý kupón lze použít jen jednou na uživatele.";
                        break;

                    case 20501:
                        userMsg = "Platnost tohoto kupónu již vypršela.";
                        break;

                    default:
                        userMsg = "Chyba při vytváření objednávky. Zkuste to prosím znovu.";
                        break;
                }

                TempData["OrderError"] = userMsg;
                return Redirect("/orders/create");
            }

        }

        /// <summary>
        /// Zobrazí detail konkrétní objednávky včetně platebních údajů a seznamu položek.
        /// </summary>
        /// <param name="id">Interní identifikátor objednávky (ORDERID).</param>
        /// <returns>View s modelem <see cref="OrderDetailsVm"/> nebo 404, pokud objednávka neexistuje.</returns>
        [HttpGet("/orders/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            await using var con = await _db.CreateOpenAsync();

            var model = new OrderDetailsVm();

            // Hlavička objednávky
            await using (var cmd = new OracleCommand(@"
                SELECT ORDER_NO, ORDERDATE, CUSTOMER, STATUS, DELIVERY, SHOP, TOTAL
                FROM VW_ORDER_DETAILS
                WHERE ORDERID = :id", con))
            {
                cmd.Parameters.Add(new OracleParameter("id", id));
                await using var rd = await cmd.ExecuteReaderAsync();
                if (!await rd.ReadAsync()) return NotFound();

                model.PublicNo = rd.GetString(0);
                model.OrderDate = rd.GetDateTime(1);
                model.Customer = rd.GetString(2);
                model.Status = rd.GetString(3);
                model.Delivery = rd.GetString(4);
                model.Shop = rd.GetString(5);
                model.Total = rd.GetDecimal(6);
            }

            // Platební informace
            await using (var cmd = new OracleCommand(@"
            SELECT PAYMENTTYPE, AMOUNT, CARD_LAST4, CASH_ACCEPTED, CASH_RETURNED, CUPON_BONUS, CUPON_EXPIRY
            FROM VW_ORDER_PAYMENT
            WHERE ORDERID = :id", con))
            {
                cmd.Parameters.Add(new OracleParameter("id", id));
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    model.PaymentType = rd.GetString(0);
                    model.Amount = rd.IsDBNull(1) ? 0m : rd.GetDecimal(1);
                    model.CardLast4 = rd.IsDBNull(2) ? (int?)null : rd.GetInt32(2);
                    model.CashAccepted = rd.IsDBNull(3) ? (decimal?)null : rd.GetDecimal(3);
                    model.CashReturned = rd.IsDBNull(4) ? (decimal?)null : rd.GetDecimal(4);
                    model.CuponBonus = rd.IsDBNull(5) ? (decimal?)null : rd.GetDecimal(5);
                    model.CuponExpiry = rd.IsDBNull(6) ? (DateTime?)null : rd.GetDateTime(6);
                }
            }

            // Položky objednávky
            model.Items = new List<OrderItemDetailsVm>();
            await using (var cmd = new OracleCommand(@"
                SELECT productid, product_name, quantity, unitprice, line_total
                FROM VW_ORDER_ITEMS
                WHERE orderid = :id
                ORDER BY product_name", con))
            {
                cmd.Parameters.Add(new OracleParameter("id", id));
                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    model.Items.Add(new OrderItemDetailsVm
                    {
                        ProductId = rd.GetInt32(0),
                        ProductName = rd.GetString(1),
                        Quantity = rd.GetInt32(2),
                        UnitPrice = rd.GetDecimal(3),
                        LineTotal = rd.GetDecimal(4)
                    });
                }
            }

            return View(model);
        }

        /// <summary>
        /// Načte ID statusu s názvem <c>PENDING</c> z pohledu <c>VW_STATUSES</c>.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi Oracle.</param>
        /// <returns>ID statusu PENDING.</returns>
        /// <exception cref="InvalidOperationException">Vyhozeno, pokud není PENDING status v číselníku nalezen.</exception>
        private async Task<int> GetPendingStatusIdAsync(OracleConnection con)
        {
            await using var cmd = new OracleCommand(
                @"SELECT ID FROM VW_STATUSES WHERE UPPER(NAME) = 'PENDING'", con);

            var v = await cmd.ExecuteScalarAsync();
            if (v != null) return Convert.ToInt32(v);
            throw new InvalidOperationException("Tabulka STATUS je prázdná – doplňte číselník stavů.");
        }

        /// <summary>
        /// Pomocná metoda pro načtení dvojic ID–Name z databáze pomocí zadaného SQL dotazu.
        /// Používá se pro naplnění dropdownů (doprava, prodejny, adresy, produkty).
        /// </summary>
        /// <param name="sql">SQL dotaz, který vrací minimálně dva sloupce (ID, Name).</param>
        /// <returns>Seznam dvojic <see cref="IdNameVm"/>.</returns>
        private async Task<List<IdNameVm>> LoadIdNameAsync(string sql)
        {
            await using var con = await _db.CreateOpenAsync();
            var list = new List<IdNameVm>();
            await using var cmd = new OracleCommand(sql, con);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                list.Add(new IdNameVm { Id = Convert.ToInt32(rd.GetValue(0)), Name = rd.GetString(1) });
            return list;
        }

        /// <summary>
        /// Aplikuje slevový kupón k dané platbě pomocí uložené procedury <c>PRC_COUPON_APPLY</c>.
        /// </summary>
        /// <param name="con">Otevřené připojení k databázi.</param>
        /// <param name="tx">Probíhající databázová transakce.</param>
        /// <param name="paymentId">Identifikátor platby, ke které se kupón vztahuje.</param>
        /// <param name="code">Textový kód kupónu zadaný uživatelem.</param>
        /// <returns>
        /// Číselný status operace (0 = úspěch, jiné hodnoty značí chybu – neexistující/propadlý kupón apod.).
        /// </returns>
        private async Task<int> ApplyCouponAsync(OracleConnection con, OracleTransaction tx, int paymentId, string code)
        {
            await using var cmd = new OracleCommand("PRC_COUPON_APPLY", con)
            {
                CommandType = CommandType.StoredProcedure,
                Transaction = tx,
                BindByName = true
            };

            cmd.Parameters.Add("p_payment_id", OracleDbType.Int32).Value = paymentId;
            cmd.Parameters.Add("p_code", OracleDbType.Varchar2, 50).Value = code.Trim();

            var oStatus = new OracleParameter("o_status", OracleDbType.Int32)
            {
                Direction = ParameterDirection.Output
            };
            cmd.Parameters.Add(oStatus);

            await cmd.ExecuteNonQueryAsync();

            return Convert.ToInt32(oStatus.Value.ToString());
        }
    }
}
