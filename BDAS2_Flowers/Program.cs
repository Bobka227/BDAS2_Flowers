using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Data;
using BDAS2_Flowers.Security;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

var oracleCs = builder.Configuration.GetConnectionString("Oracle");
if (string.IsNullOrWhiteSpace(oracleCs))
    throw new InvalidOperationException("ConnectionStrings:Oracle se nenasel. Uprav User Secrets.");

builder.Services.AddControllersWithViews();

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton(new OracleConnectionStringBuilder(oracleCs));
builder.Services.AddScoped<IDbFactory, OracleDbFactory>();
builder.Services.AddScoped<BDAS2_Flowers.Controllers.OrderControllers.IPaymentService,
                           BDAS2_Flowers.Controllers.OrderControllers.PaymentService>();

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

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(opt =>
{
    opt.IdleTimeout = TimeSpan.FromHours(2);
    opt.Cookie.HttpOnly = true;
    opt.Cookie.IsEssential = true;
    opt.Cookie.Name = "bdas2.session";
});
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error/Handle");
    app.UseHsts();
}


app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

