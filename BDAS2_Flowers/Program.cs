using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Security;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

var oracleCs = builder.Configuration.GetConnectionString("Oracle");
if (string.IsNullOrWhiteSpace(oracleCs))
    throw new InvalidOperationException("ConnectionStrings:Oracle se nenasel. Uprav User Secrets.");

builder.Services.AddControllersWithViews();

builder.Services.AddSingleton(new OracleConnectionStringBuilder(oracleCs));
builder.Services.AddScoped<IDbFactory, OracleDbFactory>();

builder.Services.AddSingleton<IPasswordHasher, HmacSha256PasswordHasher>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/auth/login";
        opt.LogoutPath = "/auth/logout";
        opt.AccessDeniedPath = "/auth/denied";
        opt.SlidingExpiration = true;
        opt.Cookie.Name = "bdas2.auth";
    });

builder.Services.AddAuthorization(opt =>
{
    opt.AddPolicy("AdminOnly", p => p.RequireRole("Admin"));
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
