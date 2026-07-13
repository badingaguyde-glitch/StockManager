using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using dotenv.net;
using Microsoft.AspNetCore.Identity;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace DbSimulate;

class TestReset
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Starting Hash Verification Test...");

        // Load .env with absolute path
        DotEnv.Load(new DotEnvOptions(envFilePaths: new[] { @"c:\Users\HP-PC\source\repos\c#Apprentissage\StockManager\StockManager.Server\.env" }));

        // Load configuration with absolute path
        var configuration = new ConfigurationBuilder()
            .SetBasePath(@"c:\Users\HP-PC\source\repos\c#Apprentissage\StockManager\StockManager.Server")
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
            .Build();

        var context = new MongoDBContext(configuration);
        var hasher = new PasswordHasher<User>();

        // 1. Get the admin user
        var adminUser = await context.Users.Find(u => u.Username == "admin").FirstOrDefaultAsync();
        if (adminUser == null)
        {
            Console.WriteLine("Admin user not found.");
            return;
        }

        // Test credentials
        string testPassword = "testPassword123!";
        
        // Hash it
        string newHash = hasher.HashPassword(adminUser, testPassword);
        Console.WriteLine($"Generated Hash: {newHash}");

        // Update database
        var filter = Builders<User>.Filter.Eq(u => u.Id, adminUser.Id);
        var update = Builders<User>.Update.Set(u => u.PasswordHash, newHash);
        var updateResult = await context.Users.UpdateOneAsync(filter, update);

        Console.WriteLine($"Database update result - Matched: {updateResult.MatchedCount}, Modified: {updateResult.ModifiedCount}");

        // 2. Fetch again from DB to verify it was written correctly
        var updatedAdmin = await context.Users.Find(u => u.Username == "admin").FirstOrDefaultAsync();
        Console.WriteLine($"DB Password Hash: {updatedAdmin.PasswordHash}");

        // Verify hash
        var verifyResult = hasher.VerifyHashedPassword(updatedAdmin, updatedAdmin.PasswordHash, testPassword);
        Console.WriteLine($"Verification result: {verifyResult}");

        if (verifyResult == PasswordVerificationResult.Success)
        {
            Console.WriteLine("SUCCESS: Password verified successfully!");
        }
        else
        {
            Console.WriteLine("FAILURE: Password verification failed!");
        }
    }
}
