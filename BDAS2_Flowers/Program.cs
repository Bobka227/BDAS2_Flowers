using Oracle.ManagedDataAccess.Client;   // <� ������� Oracle
using BDAS2_Flowers.Data;               // <� IDbFactory / OracleDbFactory

var builder = WebApplication.CreateBuilder(args);

// 1) ������ ������ ����������� � ����������
var oracleCs = builder.Configuration.GetConnectionString("Oracle");
if (string.IsNullOrWhiteSpace(oracleCs))
    throw new InvalidOperationException("ConnectionStrings:Oracle se nenasel. Uprav User Secrets.");

// 2) MVC
builder.Services.AddControllersWithViews();

// 3) DI: ����� ����������� ������ ����������� (������ ��� �����/Health � ������)
builder.Services.AddSingleton(new OracleConnectionStringBuilder(oracleCs));

// 4) ���� ������� �����������
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
