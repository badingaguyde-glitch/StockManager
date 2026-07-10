using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using dotenv.net;
using StockManager.Server.Data;
using StockManager.Server.Models;
using StockManager.Server.Services;

namespace DbSimulate;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting email alert simulation...");

        // Load .env from Server folder
        DotEnv.Load(new DotEnvOptions(envFilePaths: new[] { "../StockManager.Server/.env" }));

        // Load configuration
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../StockManager.Server"))
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        // Create MongoDBContext
        var context = new MongoDBContext(configuration);

        // Create SmtpEmailService
        var emailService = new SmtpEmailService(configuration);

        // 1. Update Admin Email
        var adminUser = await context.Users.Find(u => u.Username == "admin").FirstOrDefaultAsync();
        if (adminUser != null)
        {
            var filter = Builders<User>.Filter.Eq(u => u.Id, adminUser.Id);
            var update = Builders<User>.Update.Set(u => u.Email, "badingaguyde@gmail.com");
            await context.Users.UpdateOneAsync(filter, update);
            Console.WriteLine("1. Admin email successfully verified/updated to badingaguyde@gmail.com");
        }
        else
        {
            Console.WriteLine("Error: Admin user not found.");
            return;
        }

        // 2. Fetch the Target Product (Kulaklk)
        var productId = "6a4e40519e45f2974623fe7c";
        var product = await context.Products.Find(p => p.Id == productId).FirstOrDefaultAsync();
        if (product == null)
        {
            Console.WriteLine("Error: Target product not found.");
            return;
        }

        Console.WriteLine($"Product: {product.Name}, Current Qty: {product.Quantity}, Threshold: {product.LowStockThreshold}");

        // 3. Create a Stock Out Movement of 2965 items to drop stock below threshold
        int quantityToDecrease = 2965;
        int newQuantity = product.Quantity - quantityToDecrease; // should be 5

        // Perform MongoDB update on product quantity
        var productFilter = Builders<Product>.Filter.Eq(p => p.Id, product.Id);
        var updateQty = Builders<Product>.Update.Set(p => p.Quantity, newQuantity);
        await context.Products.UpdateOneAsync(productFilter, updateQty);
        Console.WriteLine($"2. Decreased product quantity by {quantityToDecrease}. New Qty: {newQuantity}");

        // Create Stock Movement record
        var movement = new StockMovement
        {
            ProductId = product.Id,
            Type = StockMovementType.StockOut,
            Quantity = quantityToDecrease,
            Date = DateTime.UtcNow,
            Notes = "Simulated low stock movement"
        };
        await context.StockMovements.InsertOneAsync(movement);
        Console.WriteLine($"3. Inserted StockMovement record (ID: {movement.Id})");

        // 4. Create Notification
        var notification = new Notification
        {
            Message = $"[KRİTİK STOK - STOK ÇIKIŞI] '{product.Name}' ürünü stok çıkışı sonrası limitin altına düştü! " +
                      $"İşlem Miktarı: {quantityToDecrease} adet, Kalan Stok: {newQuantity} (Limit Eşiği: {product.LowStockThreshold}).",
            IsRead = false,
            CreatedAt = DateTime.UtcNow,
            ProductId = product.Id,
            StockMovementId = movement.Id
        };
        await context.Notifications.InsertOneAsync(notification);
        Console.WriteLine($"4. Saved notification record (ID: {notification.Id})");

        // 5. Send Email Alert to Admin (badingaguyde@gmail.com)
        Console.WriteLine($"5. Sending email alert to badingaguyde@gmail.com via SMTP ({Environment.GetEnvironmentVariable("SMTP_HOST")})...");
        await emailService.SendLowStockAlertAsync("badingaguyde@gmail.com", new Product {
            Name = product.Name,
            Barcode = product.Barcode,
            Quantity = newQuantity,
            LowStockThreshold = product.LowStockThreshold
        }, DateTime.UtcNow, "Simulated Customer");

        Console.WriteLine("Simulation completed successfully! Please check your email inbox.");
    }
}
