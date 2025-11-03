using BDAS2_Flowers.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using System.Data;
using System.Linq;
using System.Security.Claims;

namespace BDAS2_Flowers.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly IConfiguration _cfg;
        public OrdersController(IConfiguration cfg) => _cfg = cfg;

        private const string CartKey = "CART";

        private int CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        // GET: /orders/create
        [HttpGet("/orders/create")]
        public async Task<IActionResult> Create()
        {
            var vm = new OrderCreateVm
            {
                UserId = CurrentUserId,
                Items = new List<OrderItemVm>()
            };

            vm.DeliveryMethods = await LoadIdNameAsync(@"SELECT Id, Name FROM VW_DELIVERY_METHODS");
            vm.Shops = await LoadIdNameAsync(@"SELECT ShopId AS Id, Name AS Name FROM FLOWER_SHOP");
            vm.Addresses = await LoadIdNameAsync(@"SELECT AddressId AS Id, Street || ' ' || HouseNumber || ', ' || PostalCode AS Name FROM ADDRESS");
            vm.Products = await LoadIdNameAsync(@"SELECT ProductId AS Id, Name AS Name FROM PRODUCT");

            var cart = HttpContext.Session.GetJson<CartVm>(CartKey);
            if (cart != null && cart.Items.Any())
                vm.Items = cart.Items.Select(i => new OrderItemVm { ProductId = i.ProductId, Quantity = i.Quantity }).ToList();
            else
                vm.Items.Add(new OrderItemVm());

            return View(vm);
        }

        // POST: /orders/create
        [ValidateAntiForgeryToken]
        [HttpPost("/orders/create")]
        public async Task<IActionResult> Create(OrderCreateVm vm)
        {
            if (!ModelState.IsValid || vm.Items == null || !vm.Items.Any() || vm.Items.Any(i => i.ProductId <= 0 || i.Quantity <= 0))
            {
                TempData["OrderError"] = "Zkontrolujte prosím položky a povinná pole.";
                return Redirect("/orders/create");
            }

            var cs = _cfg.GetConnectionString("Oracle");
            await using var con = new OracleConnection(cs);
            await con.OpenAsync();
            await using var tx = con.BeginTransaction();

            try
            {
                // 0) статус Pending берём из БД
                var pendingId = await GetPendingStatusIdAsync();

                // 1) платёж
                var type = (vm.PaymentType ?? "cash").ToLowerInvariant();
                int paymentId;
                await using (var cmd = new OracleCommand("PRC_CREATE_PAYMENT", con)
                { CommandType = CommandType.StoredProcedure, Transaction = tx })
                {
                    cmd.Parameters.Add("p_user_id", OracleDbType.Int32).Value = CurrentUserId;
                    cmd.Parameters.Add("p_type", OracleDbType.Varchar2, 10).Value = type;
                    var o = new OracleParameter("o_payment_id", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(o);
                    await cmd.ExecuteNonQueryAsync();
                    paymentId = Convert.ToInt32(o.Value.ToString());
                }

                // 1.5) адрес
                // ... внутри try, перед шагом "2) заказ"
                int addressId;
                var wantsNewAddress =
                    vm.UseNewAddress ||
                    (!string.IsNullOrWhiteSpace(vm.Street) && vm.HouseNumber > 0 && vm.PostalCode > 0);

                if (wantsNewAddress)
                {
                    if (string.IsNullOrWhiteSpace(vm.Street) || vm.HouseNumber <= 0 || vm.PostalCode <= 0)
                    {
                        TempData["OrderError"] = "Vyplňte prosím ulici, číslo domu a PSČ, nebo vyberte existující adresu.";
                        return Redirect("/orders/create");
                    }

                    await using (var cmd = new OracleCommand("PRC_CREATE_ADDRESS", con)
                    { CommandType = CommandType.StoredProcedure, Transaction = tx })
                    {
                        cmd.BindByName = true;
                        cmd.Parameters.Add("p_postalcode", OracleDbType.Int32).Value = vm.PostalCode;
                        cmd.Parameters.Add("p_street", OracleDbType.Varchar2, 200).Value = vm.Street.Trim();
                        cmd.Parameters.Add("p_housenumber", OracleDbType.Int32).Value = vm.HouseNumber;
                        var oAddr = new OracleParameter("o_address_id", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                        cmd.Parameters.Add(oAddr);
                        await cmd.ExecuteNonQueryAsync();
                        addressId = Convert.ToInt32(oAddr.Value.ToString());
                    }
                }
                else
                {
                    if (vm.AddressId is null || vm.AddressId <= 0)
                    {
                        TempData["OrderError"] = "Vyberte prosím adresu nebo zadejte novou.";
                        return Redirect("/orders/create");
                    }
                    addressId = vm.AddressId.Value;
                }


                // 2) заказ
                int orderId;
                await using (var cmd = new OracleCommand("PRC_CREATE_ORDER", con)
                { CommandType = CommandType.StoredProcedure, Transaction = tx })
                {
                    cmd.Parameters.Add("p_user_id", OracleDbType.Int32).Value = CurrentUserId;
                    cmd.Parameters.Add("p_deliverymethodid", OracleDbType.Int32).Value = vm.DeliveryMethodId;
                    cmd.Parameters.Add("p_statusid", OracleDbType.Int32).Value = pendingId;      // <-- фикс
                    cmd.Parameters.Add("p_shopid", OracleDbType.Int32).Value = vm.ShopId;
                    cmd.Parameters.Add("p_addressid", OracleDbType.Int32).Value = addressId;    // <-- фикс
                    cmd.Parameters.Add("p_paymentid", OracleDbType.Int32).Value = paymentId;
                    var o = new OracleParameter("o_order_id", OracleDbType.Int32) { Direction = ParameterDirection.Output };
                    cmd.Parameters.Add(o);
                    await cmd.ExecuteNonQueryAsync();
                    orderId = Convert.ToInt32(o.Value.ToString());
                }

                // 3) позиции
                foreach (var it in vm.Items)
                {
                    await using var cmd = new OracleCommand("PRC_ADD_ITEM", con)
                    { CommandType = CommandType.StoredProcedure, Transaction = tx };
                    cmd.Parameters.Add("p_order_id", OracleDbType.Int32).Value = orderId;
                    cmd.Parameters.Add("p_productid", OracleDbType.Int32).Value = it.ProductId;
                    cmd.Parameters.Add("p_quantity", OracleDbType.Int32).Value = it.Quantity;
                    await cmd.ExecuteNonQueryAsync();
                }

                // 4) финализация (явный курсор)
                await using (var cmd = new OracleCommand("PRC_FINALIZE_ORDER_XCUR", con)
                { CommandType = CommandType.StoredProcedure, Transaction = tx })
                {
                    cmd.Parameters.Add("p_order_id", OracleDbType.Int32).Value = orderId;
                    await cmd.ExecuteNonQueryAsync();
                }

                await tx.CommitAsync();
                HttpContext.Session.Remove(CartKey);
                TempData["OrderOk"] = "Objednávka byla vytvořena.";
                return Redirect($"/orders/{orderId}");
            }
            catch (OracleException ex)
            {
                await tx.RollbackAsync();
                TempData["OrderError"] = "Chyba při vytváření objednávky: " + ex.Message;
                return Redirect("/orders/create");
            }
        }

        // GET: /orders/{id}
        [HttpGet("/orders/{id:int}")]
        public async Task<IActionResult> Details(int id)
        {
            var cs = _cfg.GetConnectionString("Oracle");
            await using var con = new OracleConnection(cs);
            await con.OpenAsync();

            var model = new OrderDetailsVm();

            await using (var cmd = new OracleCommand(@"
                SELECT o.ORDERID, o.ORDERDATE, o.CUSTOMER, o.STATUS, o.DELIVERY, o.SHOP
                FROM VW_ORDERS o
                WHERE o.ORDERID = :id", con))
            {
                cmd.Parameters.Add(new OracleParameter("id", id));
                await using var rd = await cmd.ExecuteReaderAsync();
                if (await rd.ReadAsync())
                {
                    model.OrderId = rd.GetInt32(0);
                    model.OrderDate = rd.GetDateTime(1);
                    model.Customer = rd.GetString(2);
                    model.Status = rd.GetString(3);
                    model.Delivery = rd.GetString(4);
                    model.Shop = rd.GetString(5);
                }
                else return NotFound();
            }


            await using (var cmd = new OracleCommand(@"BEGIN :x := FN_ORDER_TOTAL(:oid); END;", con))
            {
                cmd.CommandType = CommandType.Text;
                var sum = new OracleParameter("x", OracleDbType.Decimal) { Direction = ParameterDirection.Output };
                cmd.Parameters.Add(sum);
                cmd.Parameters.Add("oid", OracleDbType.Int32).Value = id;

                await cmd.ExecuteNonQueryAsync();

                model.Total = sum.Value == null || sum.Value == DBNull.Value
                    ? 0m
                    : ((OracleDecimal)sum.Value).Value;
            }


            model.Items = new List<OrderItemDetailsVm>();
            await using (var cmd = new OracleCommand(@"
                SELECT i.PRODUCTID, p.NAME, i.QUANTITY, i.UNITPRICE, (i.QUANTITY * i.UNITPRICE) AS LINE_TOTAL
                FROM ITEM i
                JOIN PRODUCT p ON p.PRODUCTID = i.PRODUCTID
                WHERE i.ORDERID = :id
                ORDER BY p.NAME", con))
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
                        UnitPrice = (decimal)rd.GetDecimal(3),
                        LineTotal = (decimal)rd.GetDecimal(4)
                    });
                }
            }

            return View(model);
        }

        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        [HttpPost("/orders/{id:int}/status")]
        public async Task<IActionResult> ChangeStatus(int id, int statusId)
        {
            var cs = _cfg.GetConnectionString("Oracle");
            await using var con = new OracleConnection(cs);
            await con.OpenAsync();

            try
            {
                await using var cmd = new OracleCommand("PRC_CHANGE_ORDER_STATUS", con)
                { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.Add("p_order_id", OracleDbType.Int32).Value = id;
                cmd.Parameters.Add("p_status_id", OracleDbType.Int32).Value = statusId;
                cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 50).Value = User.Identity?.Name ?? "system";
                await cmd.ExecuteNonQueryAsync();

                TempData["OrderOk"] = "Status byl změněn.";
            }
            catch (Exception ex)
            {
                TempData["OrderError"] = "Nelze změnit status: " + ex.Message;
            }

            return Redirect($"/orders/{id}");
        }

        private async Task<int> GetPendingStatusIdAsync()
        {
            var cs = _cfg.GetConnectionString("Oracle");
            await using var con = new OracleConnection(cs);
            await con.OpenAsync();
            await using var cmd = new OracleCommand(@"SELECT StatusId FROM STATUS WHERE UPPER(StatusName) = 'PENDING'", con);
            var v = await cmd.ExecuteScalarAsync();
            if (v != null) return Convert.ToInt32(v);

            throw new InvalidOperationException("Tabulka STATUS je prázdná – doplňte číselník stavů.");
        }

        private async Task<List<IdNameVm>> LoadIdNameAsync(string sql)
        {
            var cs = _cfg.GetConnectionString("Oracle");
            await using var con = new OracleConnection(cs);
            await con.OpenAsync();
            var list = new List<IdNameVm>();
            await using var cmd = new OracleCommand(sql, con);
            await using var rd = await cmd.ExecuteReaderAsync();
            while (await rd.ReadAsync())
                list.Add(new IdNameVm { Id = Convert.ToInt32(rd.GetValue(0)), Name = rd.GetString(1) });
            return list;
        }
    }
}
