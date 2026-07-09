using Microsoft.Extensions.Options;
using StockManager.Server.Data;
using StockManager.Server.Models;
using StockManager.Server.Services;
using dotenv.net;

var builder = WebApplication.CreateBuilder(args);
DotEnv.Load();

builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

// Load Stripe settings from environment variables.
builder.Services.Configure<StripeSettings>(options =>
{
    options.PublicKey = Environment.GetEnvironmentVariable("STRIPE_PUBLIC_KEY") ?? string.Empty;
    options.SecretKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? string.Empty;
    options.Currency = Environment.GetEnvironmentVariable("STRIPE_CURRENCY") ?? "try";
});

// 🆕 Load Cloudinary settings from configuration
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));

builder.Services.AddSingleton<MongoDBContext>();
builder.Services.AddSingleton<StripePaymentService>(sp =>
{
    var settings = sp.GetRequiredService<IOptions<StripeSettings>>().Value;
    return new StripePaymentService(settings);
});
builder.Services.AddSingleton<ReceiptPdfService>();

// 🆕 Add Cloudinary Image Upload Service
builder.Services.AddScoped<IImageUploadService, CloudinaryImageService>();

// Add services to the container.
builder.Services.AddControllersWithViews();

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
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
