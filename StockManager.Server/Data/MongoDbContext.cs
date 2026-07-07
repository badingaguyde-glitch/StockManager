using MongoDB.Driver;
using StockManager.Server.Models;

namespace StockManager.Server.Data;

public class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IConfiguration configuration)
    {
        var settings = configuration.GetSection("MongoDbSettings").Get<MongoDbSettings>() ?? new MongoDbSettings();
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);

        SeedDefaultData();
    }

    public IMongoCollection<Category> Categories => _database.GetCollection<Category>("Categories");
    public IMongoCollection<Product> Products => _database.GetCollection<Product>("Products");
    public IMongoCollection<Supplier> Suppliers => _database.GetCollection<Supplier>("Suppliers");
    public IMongoCollection<Customer> Customers => _database.GetCollection<Customer>("Customers");
    public IMongoCollection<StockMovement> StockMovements => _database.GetCollection<StockMovement>("StockMovements");
    public IMongoCollection<Sale> Sales => _database.GetCollection<Sale>("Sales");

    private void SeedDefaultData()
    {
        if (Categories.CountDocuments(FilterDefinition<Category>.Empty) == 0)
        {
            Categories.InsertMany(new[]
            {
                new Category { Name = "Kablo", Description = "Elektrik kabloları" },
                new Category { Name = "Aydınlatma", Description = "Aydınlatma ürünleri" },
                new Category { Name = "Sigorta", Description = "Sigorta ve koruma elemanları" }
            });
        }
    }
}

public class MongoDbSettings
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";
    public string DatabaseName { get; set; } = "StockManagerDb";
}
