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

// 🆕 Load Cloudinary settings from environment variables or configuration fallback
builder.Services.Configure<CloudinarySettings>(options =>
{
    options.CloudName = Environment.GetEnvironmentVariable("CLOUD_NAME") ?? builder.Configuration["CloudinarySettings:CloudName"] ?? string.Empty;
    options.ApiKey = Environment.GetEnvironmentVariable("CLOUD_API_KEY") ?? builder.Configuration["CloudinarySettings:ApiKey"] ?? string.Empty;
    options.ApiSecret = Environment.GetEnvironmentVariable("CLOUD_API_SECRET") ?? builder.Configuration["CloudinarySettings:ApiSecret"] ?? string.Empty;
    options.MaxFileSize = int.TryParse(builder.Configuration["CloudinarySettings:MaxFileSize"], out var size) ? size : 5242880;
});

builder.Services.AddSingleton<MongoDBContext>();
builder.Services.AddSingleton<StockManager.Server.Services.IEmailService, StockManager.Server.Services.SmtpEmailService>();
builder.Services.AddSingleton<StockManager.Server.Services.IAuditLogService, StockManager.Server.Services.AuditLogService>();
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
