using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Security;
using BDAS2_Flowers.Models.ViewModels.UserProfileModels;

namespace BDAS2_Flowers.Controllers.UserControllers
{
    /// <summary>
    /// Řadič zajišťující autentizaci uživatelů – registraci, přihlášení,
    /// odhlášení a ukončení impersonace administrátorem.
    /// </summary>
    public class AuthController : Controller
    {
        private readonly IDbFactory _db;
        private readonly IPasswordHasher _hasher;

        /// <summary>
        /// Vytvoří novou instanci řadiče autentizace.
        /// </summary>
        /// <param name="db">Továrna na databázová připojení.</param>
        /// <param name="hasher">Služba pro hashování a ověřování hesel.</param>
        public AuthController(IDbFactory db, IPasswordHasher hasher)
        {
            _db = db;
            _hasher = hasher;
        }

        /// <summary>
        /// Zobrazí formulář pro registraci nového uživatele.
        /// </summary>
        /// <returns>View s prázdným modelem <see cref="RegisterVm"/>.</returns>
        [HttpGet("/auth/register")]
        public IActionResult Register() => View(new RegisterVm());

        /// <summary>
        /// Zpracuje odeslaný registrační formulář, vytvoří uživatele v databázi
        /// pomocí procedury <c>PRC_USER_REGISTER</c> a po úspěchu uživatele přihlásí.
        /// </summary>
        /// <param name="vm">Model s údaji z registračního formuláře.</param>
        /// <returns>
        /// Při chybné validaci znovu zobrazí formulář, při chybě v proceduře
        /// doplní chybové hlášky, při úspěchu přesměruje na úvodní stránku.
        /// </returns>
        [ValidateAntiForgeryToken]
        [HttpPost("/auth/register")]
        public async Task<IActionResult> Register(RegisterVm vm)
        {
            if (!ModelState.IsValid)
                return View(vm);

            await using var conn = await _db.CreateOpenAsync();

            var hash = _hasher.Hash(vm.Password);
            int newId;

            await using (var cmd = new OracleCommand("ST72861.PRC_USER_REGISTER", (OracleConnection)conn))
            {
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.BindByName = true;

                cmd.Parameters.Add("p_email", OracleDbType.Varchar2, vm.Email.ToLower(), ParameterDirection.Input);
                cmd.Parameters.Add("p_hash", OracleDbType.Varchar2, hash, ParameterDirection.Input);
                cmd.Parameters.Add("p_first", OracleDbType.Varchar2, vm.FirstName.Trim(), ParameterDirection.Input);
                cmd.Parameters.Add("p_last", OracleDbType.Varchar2, vm.LastName.Trim(), ParameterDirection.Input);
                cmd.Parameters.Add("p_phone", OracleDbType.Varchar2, vm.Phone, ParameterDirection.Input);

                var oId = new OracleParameter("o_user_id", OracleDbType.Int32)
                {
                    Direction = ParameterDirection.Output
                };
                cmd.Parameters.Add(oId);

                try
                {
                    await cmd.ExecuteNonQueryAsync();

                    var raw = oId.Value;

                    if (raw == null || raw is DBNull)
                        throw new InvalidOperationException("Procedura nevrátila USERID.");

                    if (raw is Oracle.ManagedDataAccess.Types.OracleDecimal od)
                        newId = od.ToInt32();
                    else
                        newId = Convert.ToInt32(raw);
                }
                catch (OracleException ex)
                {
                    if (ex.Number == 20202)
                    {
                        ModelState.AddModelError(nameof(vm.Email), "Tento e-mail už je registrován.");
                        return View(vm);
                    }
                    if (ex.Number == 20201)
                    {
                        ModelState.AddModelError(nameof(vm.Email), "E-mail je povinný.");
                        return View(vm);
                    }

                    ModelState.AddModelError(string.Empty, "Chyba při registraci: " + ex.Message);
                    return View(vm);
                }
            }

            await SignInAsync(newId, vm.Email.ToLower(), "Customer", vm.FirstName, vm.LastName);
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Zobrazí přihlašovací formulář.
        /// </summary>
        /// <param name="returnUrl">Volitelná návratová URL po úspěšném přihlášení.</param>
        /// <returns>View s modelem <see cref="LoginVm"/>.</returns>
        [HttpGet("/auth/login")]
        public IActionResult Login(string? returnUrl = null) => View(new LoginVm { ReturnUrl = returnUrl });

        /// <summary>
        /// Zpracuje přihlašovací formulář, ověří existenci účtu a platnost hesla
        /// a při úspěchu vytvoří autentizační cookie.
        /// </summary>
        /// <param name="vm">Model s přihlašovacími údaji.</param>
        /// <returns>
        /// Při chybě zobrazí znovu formulář, jinak přesměruje na <c>ReturnUrl</c>
        /// nebo na úvodní stránku.
        /// </returns>
        [ValidateAntiForgeryToken]
        [HttpPost("/auth/login")]
        public async Task<IActionResult> Login(LoginVm vm)
        {
            if (!ModelState.IsValid) return View(vm);

            await using var conn = await _db.CreateOpenAsync();

            int? id = null; string? email = null; string? stored = null;
            string role = "Customer"; string? fn = null, ln = null;

            await using (var c = conn.CreateCommand())
            {
                c.CommandText = @"
          SELECT u.userid, u.email, u.passwordhash, r.rolename, u.firstname, u.lastname
          FROM ""USER"" u
          JOIN role r ON r.roleid = u.roleid
          WHERE LOWER(u.email) = :email";
                c.Parameters.Add(new OracleParameter("email", OracleDbType.Varchar2, vm.Email.ToLower(), ParameterDirection.Input));

                await using var r = await c.ExecuteReaderAsync(CommandBehavior.CloseConnection);
                if (await r.ReadAsync())
                {
                    id = DbRead.GetInt32(r, 0);
                    email = r.GetString(1);
                    stored = r.GetString(2);
                    role = r.GetString(3);
                    fn = r.GetString(4);
                    ln = r.GetString(5);
                }
            }

            if (id is null)
            {
                ModelState.AddModelError(nameof(vm.Email), "Účet s tímto e-mailem neexistuje.");
                ViewBag.ShowRegisterPrompt = true;
                return View(vm);
            }

            if (!_hasher.Verify(vm.Password, stored!))
            {
                ModelState.AddModelError(nameof(vm.Password), "Nesprávný e-mail nebo heslo.");
                return View(vm);
            }

            await SignInAsync(id.Value, email!, role, fn!, ln!);

            if (!string.IsNullOrWhiteSpace(vm.ReturnUrl) && Url.IsLocalUrl(vm.ReturnUrl))
                return Redirect(vm.ReturnUrl);

            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Odhlásí aktuálně přihlášeného uživatele a zruší autentizační cookie.
        /// </summary>
        /// <returns>Přesměrování na úvodní stránku.</returns>
        [HttpPost("/auth/logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        /// <summary>
        /// Ukončí impersonaci uživatele administrátorem a obnoví původní
        /// administrátorský účet podle claimu <c>ImpersonatedBy</c>.
        /// </summary>
        /// <returns>
        /// Při úspěchu přesměruje do admin části, při chybě nebo neexistenci claimu
        /// na úvodní stránku.
        /// </returns>
        [HttpPost("/auth/stop-impersonation")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StopImpersonation()
        {
            var adminEmail = User.Claims.FirstOrDefault(c => c.Type == "ImpersonatedBy")?.Value;

            if (string.IsNullOrWhiteSpace(adminEmail))
            {
                return RedirectToAction("Index", "Home");
            }

            await using var conn = await _db.CreateOpenAsync();

            int? id = null; string? email = null; string role = "Customer"; string? fn = null; string? ln = null;

            await using (var c = conn.CreateCommand())
            {
                c.CommandText = @"
              SELECT u.userid, u.email, r.rolename, u.firstname, u.lastname
              FROM ""USER"" u
              JOIN role r ON r.roleid = u.roleid
              WHERE LOWER(u.email) = :email";
                c.Parameters.Add(new OracleParameter("email", OracleDbType.Varchar2, adminEmail.ToLower(), ParameterDirection.Input));

                await using var r = await c.ExecuteReaderAsync(CommandBehavior.CloseConnection);
                if (await r.ReadAsync())
                {
                    id = DbRead.GetInt32(r, 0);
                    email = r.GetString(1);
                    role = r.GetString(2);
                    fn = r.IsDBNull(3) ? "" : r.GetString(3);
                    ln = r.IsDBNull(4) ? "" : r.GetString(4);
                }
            }

            if (id is null || email is null)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToAction("Index", "Home");
            }

            await SignInAsync(id.Value, email, role, fn ?? "", ln ?? "");
            return RedirectToAction("Index", "AdminHome");
        }

        /// <summary>
        /// Stránka zobrazená při nedostatečných oprávněních (access denied).
        /// </summary>
        /// <returns>Jednoduchý text s informací o odepření přístupu.</returns>
        [HttpGet("/auth/denied")]
        public IActionResult Denied() => Content("Přístup odepřen.");

        /// <summary>
        /// Vytvoří přihlašovací identity a uloží ji do autentizační cookie.
        /// </summary>
        /// <param name="userId">Interní ID uživatele.</param>
        /// <param name="email">E-mailová adresa uživatele.</param>
        /// <param name="role">Název role uživatele.</param>
        /// <param name="firstName">Křestní jméno uživatele.</param>
        /// <param name="lastName">Příjmení uživatele.</param>
        private async Task SignInAsync(int userId, string email, string role, string firstName, string lastName)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, $"{firstName} {lastName}"),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, role)
            };
            var id = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(new ClaimsPrincipal(id));
        }
    }
}
