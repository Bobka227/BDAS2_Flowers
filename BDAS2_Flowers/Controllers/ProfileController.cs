using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Models.ViewModels;
using BDAS2_Flowers.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using BDAS2_Flowers.Data;

namespace BDAS2_Flowers.Controllers
{
    [Authorize]
    public class ProfileController : Controller
    {
        private readonly IDbFactory _db;
        private readonly IPasswordHasher _hasher;

        public ProfileController(IDbFactory db, IPasswordHasher hasher)
        {
            _db = db;
            _hasher = hasher;
        }

        private int CurrentUserId =>
            int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

        [HttpGet("/profile")]
        public async Task<IActionResult> Index()
        {
            var vm = new ProfileVm
            {
                UserId = CurrentUserId,
                FullName = User.FindFirstValue(ClaimTypes.Name) ?? "",
                Email = User.FindFirstValue(ClaimTypes.Email) ?? "",
                Role = User.FindFirstValue(ClaimTypes.Role) ?? ""
            };

            await using var con = await _db.CreateOpenAsync();

            const string sqlOrders = @"
                SELECT ORDERID, ORDER_NO, ORDERDATE, STATUS, DELIVERY, SHOP, TOTAL
                FROM VW_USER_ORDERS_EX
                WHERE UPPER(EMAIL)=UPPER(:e)
                ORDER BY ORDERDATE DESC";

            await using (var cmd = new OracleCommand(sqlOrders, con))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add("e", OracleDbType.Varchar2).Value = vm.Email;

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    vm.Orders.Add(new ProfileOrderRowVm
                    {
                        OrderId = rd.GetInt32(0),
                        OrderNo = rd.GetString(1),
                        OrderDate = rd.GetDateTime(2),
                        Status = rd.GetString(3),
                        Delivery = rd.GetString(4),
                        Shop = rd.GetString(5),
                        Total = (decimal)rd.GetDecimal(6)
                    });
                }
            }

            const string sqlAddr = @"
                SELECT ADDRESSID, STREET, HOUSENUMBER, POSTALCODE, LAST_USED, USED_COUNT
                FROM VW_USER_ADDRESSES
                WHERE UPPER(EMAIL)=UPPER(:e)
                ORDER BY LAST_USED DESC";

            await using (var cmd = new OracleCommand(sqlAddr, con))
            {
                cmd.BindByName = true;
                cmd.Parameters.Add("e", OracleDbType.Varchar2).Value = vm.Email;

                await using var rd = await cmd.ExecuteReaderAsync();
                while (await rd.ReadAsync())
                {
                    vm.Addresses.Add(new ProfileAddressVm
                    {
                        AddressId = rd.GetInt32(0),
                        Line = $"{rd.GetString(1)} {rd.GetInt32(2)}, {rd.GetInt32(3)}",
                        LastUsed = rd.GetDateTime(4),
                        UsedCount = rd.GetInt32(5)
                    });
                }
            }

            return View(vm);
        }

        [ValidateAntiForgeryToken]
        [HttpPost("/profile/change-email")]
        public async Task<IActionResult> ChangeEmail(ChangeEmailVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.NewEmail) || string.IsNullOrWhiteSpace(vm.Password))
            {
                TempData["ProfileError"] = "Vyplňte prosím všechna pole.";
                return Redirect("/profile?tab=email");
            }

            await using var con = await _db.CreateOpenAsync();

            string? dbHash;
            await using (var cmd = new OracleCommand(@"SELECT ""PASSWORDHASH"" FROM ""ST72861"".""USER"" WHERE ""USERID"" = :id", con))
            {
                cmd.Parameters.Add(new OracleParameter("id", CurrentUserId));
                dbHash = (string?)await cmd.ExecuteScalarAsync();
            }
            if (dbHash == null || !_hasher.Verify(vm.Password, dbHash))
            {
                TempData["ProfileError"] = "Nesprávné heslo.";
                return Redirect("/profile?tab=email");
            }

            try
            {
                await using (var cmd = new OracleCommand(@"UPDATE ""ST72861"".""USER"" SET ""EMAIL"" = :em WHERE ""USERID"" = :id", con))
                {
                    cmd.Parameters.Add(new OracleParameter("em", vm.NewEmail.Trim()));
                    cmd.Parameters.Add(new OracleParameter("id", CurrentUserId));
                    await cmd.ExecuteNonQueryAsync();
                }

                var claims = User.Claims.ToList();
                var old = claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
                if (old != null) claims.Remove(old);
                claims.Add(new Claim(ClaimTypes.Email, vm.NewEmail.Trim()));
                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

                TempData["ProfileOk"] = "E-mail byl změněn.";
                return Redirect("/profile?tab=overview");
            }
            catch (OracleException ex) when (ex.Number == 1)
            {
                TempData["ProfileError"] = "Tento e-mail už existuje.";
                return Redirect("/profile?tab=email");
            }
            catch
            {
                TempData["ProfileError"] = "Došlo k chybě. Zkuste to prosím znovu.";
                return Redirect("/profile?tab=email");
            }
        }

        [ValidateAntiForgeryToken]
        [HttpPost("/profile/change-password")]
        public async Task<IActionResult> ChangePassword(ChangePasswordVm vm)
        {
            if (string.IsNullOrWhiteSpace(vm.CurrentPassword) ||
                string.IsNullOrWhiteSpace(vm.NewPassword) ||
                vm.NewPassword != vm.ConfirmNewPassword)
            {
                TempData["ProfileError"] = "Zkontrolujte nová hesla a vyplněná pole.";
                return Redirect("/profile?tab=password");
            }

            await using var con = await _db.CreateOpenAsync();

            string? dbHash;
            await using (var cmd = new OracleCommand(@"SELECT ""PASSWORDHASH"" FROM ""ST72861"".""USER"" WHERE ""USERID"" = :id", con))
            {
                cmd.Parameters.Add(new OracleParameter("id", CurrentUserId));
                dbHash = (string?)await cmd.ExecuteScalarAsync();
            }
            if (dbHash == null || !_hasher.Verify(vm.CurrentPassword, dbHash))
            {
                TempData["ProfileError"] = "Aktuální heslo není správné.";
                return Redirect("/profile?tab=password");
            }

            var newHash = _hasher.Hash(vm.NewPassword);
            await using (var cmd = new OracleCommand(@"UPDATE ""ST72861"".""USER"" SET ""PASSWORDHASH"" = :ph WHERE ""USERID"" = :id", con))
            {
                cmd.Parameters.Add(new OracleParameter("ph", newHash));
                cmd.Parameters.Add(new OracleParameter("id", CurrentUserId));
                await cmd.ExecuteNonQueryAsync();
            }

            TempData["ProfileOk"] = "Heslo bylo změněno.";
            return Redirect("/profile?tab=overview");
        }

        [ValidateAntiForgeryToken]
        [HttpPost("/profile/avatar")]
        [RequestSizeLimit(10 * 1024 * 1024)]
        [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            const long MaxBytes = 10 * 1024 * 1024;
            if (file is null || file.Length == 0)
            {
                TempData["ProfileError"] = "Vyberte prosím soubor.";
                return Redirect("/profile?tab=overview");
            }
            if (file.Length > MaxBytes)
            {
                TempData["ProfileError"] = "Maximální velikost avataru je 10 MB.";
                return Redirect("/profile?tab=overview");
            }
            var allowed = new[] { "image/png", "image/jpeg", "image/gif", "image/webp" };
            if (!allowed.Contains(file.ContentType))
            {
                TempData["ProfileError"] = "Podporované formáty: PNG, JPG, GIF, WEBP.";
                return Redirect("/profile?tab=overview");
            }

            var ext = Path.GetExtension(file.FileName).Trim('.').ToLower();

            byte[] bytes;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                bytes = ms.ToArray();
            }

            await using var con = await _db.CreateOpenAsync();

            await using var cmd = new OracleCommand("ST72861.PR_SET_AVATAR", con)
            { CommandType = System.Data.CommandType.StoredProcedure };
            cmd.Parameters.Add("p_userid", OracleDbType.Int32).Value = CurrentUserId;
            cmd.Parameters.Add("p_name", OracleDbType.Varchar2, 200).Value = file.FileName;
            cmd.Parameters.Add("p_ext", OracleDbType.Varchar2, 20).Value = ext;
            cmd.Parameters.Add("p_blob", OracleDbType.Blob).Value = bytes;
            cmd.Parameters.Add("p_actor", OracleDbType.Varchar2, 50).Value = User.Identity?.Name ?? "system";
            cmd.Parameters.Add("o_avatarid", OracleDbType.Int32).Direction = System.Data.ParameterDirection.Output;

            await cmd.ExecuteNonQueryAsync();

            TempData["ProfileOk"] = "Avatar byl aktualizován.";
            return Redirect("/profile?tab=overview");
        }
    }
}
