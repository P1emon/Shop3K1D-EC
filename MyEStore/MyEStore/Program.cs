using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using MyEStore.Entities;
using MyEStore.Models;
using MyEStore.Servicess;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<MyeStoreContext>(options => {
	options.UseSqlServer(builder.Configuration.GetConnectionString("MyDb"));
});

builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        options.SlidingExpiration = true;
        options.LoginPath = "/Customer/Login";
        options.AccessDeniedPath = "/Forbidden/";
    });

//đăng ký PaymentClient dạng Singleton
builder.Services.AddSingleton(x =>
	new PaypalClient(
		builder.Configuration["PayPalOptions:ClientId"],
		builder.Configuration["PayPalOptions:ClientSecret"],
		builder.Configuration["PayPalOptions:Mode"]
	)
);

builder.Services.AddSingleton<IVnpayService, VnPayService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
	app.UseExceptionHandler("/Home/Error");
	// The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
	app.UseHsts();
}



app.UseStaticFiles();

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();



app.MapControllerRoute(
	name: "default",
	pattern: "{controller=Products}/{action=Index}/{id?}");

app.UseEndpoints(endpoints =>
{
    endpoints.MapControllerRoute(
        name: "productDetail",
        pattern: "san-pham/{categorySlug}/{productSlug}",
        defaults: new { controller = "Products", action = "Detail" });

    endpoints.MapDefaultControllerRoute();
});
app.MapControllerRoute(
    name: "search",
    pattern: "tim-kiem",
    defaults: new { controller = "Products", action = "Search" });


app.Run();
