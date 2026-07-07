using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using StockManager.Server.Models;

namespace StockManager.Server.Data
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(IConfiguration configuration)
        {
            var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? configuration.GetSection("MongoDbSettings:ConnectionString").Value;
            var databaseName = configuration.GetSection("MongoDbSettings:DatabaseName").Value;

            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(databaseName);

            SeedData();
        }

        public IMongoCollection<Product> Products => _database.GetCollection<Product>("Products");
        public IMongoCollection<Category> Categories => _database.GetCollection<Category>("Categories");
        public IMongoCollection<Supplier> Suppliers => _database.GetCollection<Supplier>("Suppliers");
        public IMongoCollection<Customer> Customers => _database.GetCollection<Customer>("Customers");
        public IMongoCollection<StockMovement> StockMovements => _database.GetCollection<StockMovement>("StockMovements");
        public IMongoCollection<Sale> Sales => _database.GetCollection<Sale>("Sales");

        private void SeedData()
        {
            if (Categories.EstimatedDocumentCount() == 0)
            {
                var defaultCategories = new List<Category>
                {
                    new Category { Name = "Kablo", Description = "Elektrik kabloları" },
                    new Category { Name = "Aydınlatma", Description = "Aydınlatma ürünleri" },
                    new Category { Name = "Sigorta", Description = "Sigorta ve koruma elemanları" }
                };
                Categories.InsertMany(defaultCategories);
            }
        }
    }
}
