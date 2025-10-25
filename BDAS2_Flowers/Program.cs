using Oracle.ManagedDataAccess.Client;
using BDAS2_Flowers.Data;

var builder = WebApplication.CreateBuilder(args);

var oracleCs = builder.Configuration.GetConnectionString("Oracle");
if (string.IsNullOrWhiteSpace(oracleCs))
    throw new InvalidOperationException("ConnectionStrings:Oracle se nenasel. Uprav User Secrets.");

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton(new OracleConnectionStringBuilder(oracleCs));
builder.Services.AddScoped<IDbFactory, OracleDbFactory>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthorization();

app.MapControllers();

app.MapControllerRoute(
    name: "mvc",
    pattern: "{controller=Home}/{action=Index}/{id?}");


app.Run();
