using MongoDB.Driver;
using Microsoft.Extensions.Configuration;
using StockManager.Server.Models;

namespace StockManager.Server.Data
{
    public class MongoDBContext
    {
        private readonly IMongoDatabase _database;

        public IMongoClient Client {get;}

        public MongoDBContext(IConfiguration configuration)
        {
            var connectionString = Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING") ?? configuration.GetSection("MongoDbSettings:ConnectionString").Value;
            var databaseName = configuration.GetSection("MongoDbSettings:DatabaseName").Value;

            Client = new MongoClient(connectionString);
            _database = Client.GetDatabase(databaseName);

            SeedData();
        }

        public IMongoCollection<Product> Products => _database.GetCollection<Product>("Products");
        public IMongoCollection<Category> Categories => _database.GetCollection<Category>("Categories");
        public IMongoCollection<Supplier> Suppliers => _database.GetCollection<Supplier>("Suppliers");
        public IMongoCollection<Customer> Customers => _database.GetCollection<Customer>("Customers");
        public IMongoCollection<StockMovement> StockMovements => _database.GetCollection<StockMovement>("StockMovements");
        public IMongoCollection<Sale> Sales => _database.GetCollection<Sale>("Sales");
        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public IMongoCollection<Notification> Notifications => _database.GetCollection<Notification>("Notifications");
        public IMongoCollection<AuditLog> AuditLogs => _database.GetCollection<AuditLog>("AuditLogs");
        public IMongoCollection<Warehouse> Warehouses => _database.GetCollection<Warehouse>("Warehouses");
        public IMongoCollection<StockTransfer> StockTransfers => _database.GetCollection<StockTransfer>("StockTransfers");
        public IMongoCollection<PurchaseOrder> PurchaseOrders => _database.GetCollection<PurchaseOrder>("PurchaseOrders");
        public IMongoCollection<ProductBatch> ProductBatches => _database.GetCollection<ProductBatch>("ProductBatches");

        private void SeedData()
        {
            if (Categories.EstimatedDocumentCount() == 0)
            {
                var defaultCategories = new List<Category>
                {
                    new Category { Name = "Electronics" },
                    new Category { Name = "Clothing" },
                    new Category { Name = "Books" }
                };
                Categories.InsertMany(defaultCategories);
            }
            if (Users.EstimatedDocumentCount() == 0)
            {
                var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<User>();
                var defaultAdmin = new User
                {
                    Username = "admin",
                    Email = "admin@stockmanager.com",
                    Role = UserRole.Admin
                };
                defaultAdmin.PasswordHash = hasher.HashPassword(defaultAdmin, "admin123");
                Users.InsertOne(defaultAdmin);
            }
            if (Warehouses.EstimatedDocumentCount() == 0)
            {
                var defaultWarehouses = new List<Warehouse>
                {
                    new Warehouse { Name = "Ana Depo", Code = "ANA", Description = "Merkez Ana Depo", IsDefault = true, IsActive = true },
                    new Warehouse { Name = "Mağaza 1", Code = "MGZ1", Description = "Perakende Satış Mağazası", IsDefault = false, IsActive = true },
                    new Warehouse { Name = "Yedek Depo", Code = "YDK", Description = "Yedek ve Lojistik Deposu", IsDefault = false, IsActive = true }
                };
                Warehouses.InsertMany(defaultWarehouses);

                var anaDepo = Warehouses.Find(w => w.IsDefault).FirstOrDefault() ?? defaultWarehouses.First();
                if (anaDepo != null && !string.IsNullOrEmpty(anaDepo.Id))
                {
                    var existingProducts = Products.Find(p => p.WarehouseStocks == null || p.WarehouseStocks.Count == 0).ToList();
                    foreach (var product in existingProducts)
                    {
                        product.WarehouseStocks = new List<WarehouseStock>
                        {
                            new WarehouseStock
                            {
                                WarehouseId = anaDepo.Id,
                                WarehouseName = anaDepo.Name,
                                Quantity = product.Quantity
                            }
                        };
                        Products.ReplaceOne(p => p.Id == product.Id, product);
                    }
                }
            }
        }
    }
}

