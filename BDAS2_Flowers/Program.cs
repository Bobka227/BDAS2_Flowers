using Oracle.ManagedDataAccess.Client;   // <— драйвер Oracle
using BDAS2_Flowers.Data;               // <— IDbFactory / OracleDbFactory

var builder = WebApplication.CreateBuilder(args);

// 1) Читаем строку подключения и валидируем
var oracleCs = builder.Configuration.GetConnectionString("Oracle");
if (string.IsNullOrWhiteSpace(oracleCs))
    throw new InvalidOperationException("ConnectionStrings:Oracle se nenasel. Uprav User Secrets.");

// 2) MVC
builder.Services.AddControllersWithViews();

// 3) DI: кладём построитель строки подключения (удобно для логов/Health и фабрик)
builder.Services.AddSingleton(new OracleConnectionStringBuilder(oracleCs));

// 4) Твоя фабрика подключения
builder.Services.AddScoped<IDbFactory, OracleDbFactory>();

var app = builder.Build();

// ------ Pipeline ------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
