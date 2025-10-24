var builder = WebApplication.CreateBuilder(args);

var testCs = builder.Configuration.GetConnectionString("Oracle");
if (string.IsNullOrWhiteSpace(testCs))
    throw new InvalidOperationException("ConnectionStrings:Oracle se nenasel. Uprav User Secrets.");

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddScoped<BDAS2_Flowers.Data.IDbFactory, BDAS2_Flowers.Data.OracleDbFactory>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
