# 🏗️ Mimari Yapısı ve Teknik Detaylar

StockManager'ın teknik altyapısını, veri akışını ve sistem bileşenlerini anlamak için bu dokümanı okuyun.

---

## 📐 Genel Sistem Mimarisi

```
┌─────────────────────────────────────────────────────────────┐
│                        WEB BROWSER                          │
│                    (Bootstrap 5 UI)                         │
└────────────────────────┬──────────────────────────────────┘
                         │ HTTP/HTTPS
                         ▼
┌─────────────────────────────────────────────────────────────┐
│           ASP.NET CORE 8.0 MVC APPLICATION                  │
│  ┌────────────────────────────────────────────────────────┐ │
│  │                   Controllers                          │ │
│  │  (HomeController, ProductsController, vb.)            │ │
│  └────────────────────────────────────────────────────────┘ │
│           │ ▲                                                │
│           │ │ (Model ↔ View)                               │
│           ▼ │                                                │
│  ┌────────────────────────────────────────────────────────┐ │
│  │           Services & Business Logic                    │ │
│  │  (StripePaymentService, ReceiptPdfService)            │ │
│  └────────────────────────────────────────────────────────┘ │
│           │                                                  │
│           ▼                                                  │
│  ┌────────────────────────────────────────────────────────┐ │
│  │              Data Layer (MongoDBContext)              │ │
│  └────────────────────────────────────────────────────────┘ │
└────────────────────────┬──────────────────────────────────┘
                         │ MongoDB Wire Protocol
                         ▼
┌─────────────────────────────────────────────────────────────┐
│                      MONGODB DATABASE                       │
│  (Collections: Products, Categories, Suppliers, etc.)      │
└─────────────────────────────────────────────────────────────┘
```

---

## 📁 Proje Dosya Yapısı

```
StockManager.Server/
│
├── 📂 Controllers/                  # HTTP istekileri işleyen sınıflar
│   ├── HomeController              # Dashboard (GET /, GET /home/index)
│   ├── ProductsController          # CRUD: GET, POST, PUT, DELETE
│   ├── CategoriesController        # Kategori yönetimi
│   ├── SuppliersController         # Tedarikçi yönetimi
│   ├── CustomersController         # Müşteri yönetimi
│   ├── SalesController             # Satış işlemleri
│   ├── StockMovementsController    # Stok hareketleri
│   └── ReportsController           # Raporlama
│
├── 📂 Models/                       # Veri modelleri (POCO sınıfları)
│   ├── Product                      # Ürün veri yapısı
│   ├── Category                     # Kategori veri yapısı
│   ├── Supplier                     # Tedarikçi veri yapısı
│   ├── Customer                     # Müşteri veri yapısı
│   ├── Sale                         # Satış veri yapısı
│   ├── SaleItem                     # Satış detayı veri yapısı
│   ├── StockMovement               # Stok hareketi veri yapısı
│   ├── ProductInputModel           # Ürün formu input modeli
│   ├── Enums                        # Sabitler (PaymentType, MovementType)
│   ├── StripeSettings              # Stripe yapılandırması
│   ├── StripePaymentIntentRequest  # Stripe ödeme isteği
│   └── ErrorViewModel              # Hata sayfası modeli
│
├── 📂 Views/                        # Razor HTML şablonları
│   ├── Home/
│   │   └── Index.cshtml            # Dashboard sayfası
│   ├── Products/
│   │   ├── Index.cshtml            # Ürün listesi
│   │   ├── Create.cshtml           # Yeni ürün formu
│   │   └── Edit.cshtml             # Ürün düzenleme formu
│   ├── Categories/                 # Kategori sayfaları
│   ├── Suppliers/                  # Tedarikçi sayfaları
│   ├── Customers/                  # Müşteri sayfaları
│   ├── Sales/                      # Satış sayfaları
│   ├── StockMovements/             # Stok hareketi sayfaları
│   ├── Reports/                    # Rapor sayfaları
│   └── Shared/
│       ├── _Layout.cshtml          # Ana şablon (navbar, footer)
│       ├── _ViewStart.cshtml       # View başlangıcı
│       └── _ViewImports.cshtml     # Global using direktifleri
│
├── 📂 Data/                         # Veri erişim katmanı
│   └── MongoDBContext              # MongoDB bağlantısı ve koleksiyonlar
│
├── 📂 Services/                     # İş mantığı servisleri
│   ├── StripePaymentService        # Stripe ödeme işlemleri
│   └── ReceiptPdfService           # PDF fatura oluşturma
│
├── 📂 wwwroot/                      # Statik web dosyaları
│   ├── css/
│   │   └── site.css               # Özel CSS
│   └── js/
│       └── site.js                # Özel JavaScript
│
├── 📂 Properties/
│   └── launchSettings.json         # Çalışma profilleri
│
├── Program.cs                       # Uygulamanın başlangıç noktası
├── appsettings.json                # Yapılandırma dosyası
├── appsettings.Development.json    # Geliştirme yapılandırması
├── StockManager.Server.csproj      # Proje dosyası (NuGet paketleri)
└── .env                            # Ortam değişkenleri (git'te takip edilmez)
```

---

## 🔄 İstek-Yanıt Akışı (Request-Response Flow)

### Örnek: Yeni Ürün Ekleme

```
1. Kullanıcı "Yeni Ürün" butonuna tıklar
   ↓
2. GET /Products/Create isteği
   → ProductsController.Create() çağrılır
   → PopulateDropdowns() tedarikçi ve kategori listesi yükler
   → ProductInputModel ile Create.cshtml render edilir
   ↓
3. Kullanıcı formu doldurur ve "Kaydet" tıklar
   ↓
4. POST /Products/Create isteği (ProductInputModel)
   → ModelState.IsValid() kontrolü
   → Barkod benzersizliği kontrolü
   → Eğer SupplierName varsa, yeni Supplier oluştur
   → Product nesnesini oluştur
   → MongoDB'ye InsertOneAsync() ile kaydet
   → RedirectToAction("Index")
   ↓
5. GET /Products/Index isteği
   → Tüm ürünler listelenir
```

---

## 🗄️ Veritabanı Şeması

### MongoDB Koleksiyonları

#### **Products Koleksiyonu**

```javascript
{
  "_id": ObjectId,
  "barcode": "1234567890",
  "name": "Dizüstü Bilgisayar",
  "description": "Yüksek performanslı...",
  "purchasePrice": 2000,
  "salePrice": 2500,
  "quantity": 10,
  "lowStockThreshold": 5,
  "categoryId": ObjectId,      // Category referansı
  "supplierId": ObjectId       // Supplier referansı
}
```

#### **Categories Koleksiyonu**

```javascript
{
  "_id": ObjectId,
  "name": "Elektronik",
  "description": "Bilgisayar ve aksesuarları"
}
```

#### **Suppliers Koleksiyonu**

```javascript
{
  "_id": ObjectId,
  "companyName": "ABC Ticaret",
  "contactName": "İbrahim Yılmaz",
  "phone": "0212-123-4567",
  "email": "info@abc.com",
  "balance": 50000        // Borç/Alacak
}
```

#### **Customers Koleksiyonu**

```javascript
{
  "_id": ObjectId,
  "fullName": "Ahmet Demir",
  "phone": "0533-555-1234",
  "email": "ahmet@example.com",
  "balance": -25000       // Müşteri borcu
}
```

#### **Sales Koleksiyonu**

```javascript
{
  "_id": ObjectId,
  "saleDate": ISODate("2026-07-08T10:30:00Z"),
  "invoiceNumber": "INV-2026-0001",
  "totalAmount": 2500,
  "customerId": ObjectId,
  "paymentType": "Card",
  "currency": "TRY",
  "stripePaymentIntentId": "pi_XXXXXXX",
  "stripeStatus": "succeeded",
  "items": [
    {
      "productId": ObjectId,
      "productName": "Dizüstü",
      "quantity": 1,
      "unitPrice": 2500,
      "subtotal": 2500
    }
  ]
}
```

#### **StockMovements Koleksiyonu**

```javascript
{
  "_id": ObjectId,
  "productId": ObjectId,
  "movementType": "Inbound",      // Inbound, Outbound, Adjustment
  "quantity": 10,
  "date": ISODate("2026-07-08T10:30:00Z"),
  "supplierId": ObjectId,          // Tedarikçi (giriş için)
  "notes": "Sipariş #12345"
}
```

---

## 🔌 Bağımlılık Enjeksiyonu (Dependency Injection)

`Program.cs` içinde yapılandırılır:

```csharp
// Services kayıtları
builder.Services.AddSingleton<MongoDBContext>();
builder.Services.AddSingleton<StripePaymentService>();
builder.Services.AddSingleton<ReceiptPdfService>();

// MVC desteği
builder.Services.AddControllersWithViews();

// API Belgelendirmesi
builder.Services.AddSwaggerGen();
```

Kontrolcülerde kullanım:

```csharp
public class ProductsController : Controller
{
    private readonly MongoDBContext _context;

    public ProductsController(MongoDBContext context)
    {
        _context = context;  // Otomatik enjeksiyonu
    }
}
```

---

## 🔐 Veri Güvenliği

### Koruma Mekanizmaları

1. **MongoDB ObjectId Validation**: Tüm ID'ler MongoDB ObjectId olarak doğrulanır
2. **Input Validation**: ModelState kontrolü ile form verileri doğrulanır
3. **Barkod Benzersizliği**: Duplicate barkod preventif kontrolü
4. **HTTPS Only**: Tüm iletişim şifreli
5. **CORS Ready**: Gelecekteki API güvenliği için hazır
6. **Anti-CSRF**: ValidateAntiForgeryToken ile form koruma

---

## 📊 MongoDB Bağlantı Düzeni

```csharp
// MongoDBContext.cs
public class MongoDBContext
{
    private readonly IMongoDatabase _database;

    public MongoDBContext(IConfiguration configuration)
    {
        // Bağlantı string'i okuma (environment variable veya appsettings.json)
        var connectionString =
            Environment.GetEnvironmentVariable("MONGODB_CONNECTION_STRING")
            ?? configuration.GetSection("MongoDbSettings:ConnectionString").Value;

        var databaseName =
            configuration.GetSection("MongoDbSettings:DatabaseName").Value;

        var client = new MongoClient(connectionString);
        _database = client.GetDatabase(databaseName);

        SeedData();  // İlk verileri yükle
    }

    // Koleksiyonlar
    public IMongoCollection<Product> Products
        => _database.GetCollection<Product>("Products");

    // ... diğer koleksiyonlar
}
```

---

## 💳 Stripe Entegrasyonu

### Ödeme Akışı

```
1. Satış formu doldurulur
2. Ödeme türü "Card (Stripe)" seçilir
3. Stripe.js ile Client-side token oluşturulur
4. Backend'e POST isteği: StripePaymentIntentRequest
5. StripePaymentService.ProcessPaymentAsync() çağrılır
6. PaymentIntent oluşturulur
7. PaymentIntent.ClientSecret döndürülür
8. Frontend'de Stripe Checkout gösterilir
9. Müşteri kredi kartı bilgilerini girer
10. Stripe ödemeyi işler
11. Başarılı ise Sale kaydedilir
12. PDF fatura oluşturulur
```

### Stripe Settings

```csharp
public class StripeSettings
{
    public string PublicKey { get; set; }    // pk_test_...
    public string SecretKey { get; set; }    // sk_test_...
    public string Currency { get; set; }     // "try"
}
```

---

## 📄 PDF Fatura Oluşturma

QuestPDF kütüphanesi kullanılır:

```csharp
public class ReceiptPdfService
{
    public Stream GenerateReceipt(Sale sale)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Content()
                    .PaddingVertical(20)
                    .Column(col =>
                    {
                        col.Item().Text("FATURA").FontSize(24).Bold();
                        col.Item().Text($"Invoice: {sale.InvoiceNumber}");
                        col.Item().Table(table =>
                        {
                            table.Cell(cell => cell.Text("Ürün"));
                            table.Cell(cell => cell.Text("Miktar"));
                            table.Cell(cell => cell.Text("Fiyat"));

                            foreach (var item in sale.Items)
                            {
                                table.Cell(cell =>
                                    cell.Text(item.ProductName));
                                table.Cell(cell =>
                                    cell.Text(item.Quantity.ToString()));
                                table.Cell(cell =>
                                    cell.Text(item.Subtotal.ToString()));
                            }
                        });

                        col.Item().Text($"Toplam: {sale.TotalAmount}");
                    });
            });
        });

        var stream = new MemoryStream();
        document.GeneratePdf(stream);
        stream.Position = 0;
        return stream;
    }
}
```

---

## 🔄 CRUD İşlemleri Şablonu

Tüm kontrolcüler aynı şablonu izler:

```
GET    /Entity/Index         → Listele
GET    /Entity/Details/{id}  → Detay
GET    /Entity/Create        → Yeni form
POST   /Entity/Create        → Kaydet
GET    /Entity/Edit/{id}     → Düzenleme form
POST   /Entity/Edit          → Güncelle
POST   /Entity/Delete        → Sil
```

---

## 🧪 Entity İlişkileri

### One-to-Many (Bir-Çok):

- **Category** → **Products** (1 kategori, çok ürün)
- **Supplier** → **Products** (1 tedarikçi, çok ürün)
- **Supplier** → **StockMovements** (1 tedarikçi, çok hareket)
- **Customer** → **Sales** (1 müşteri, çok satış)
- **Sale** → **SaleItems** (1 satış, çok satış detayı)

### Referans Şekli:

```csharp
// Product.cs
public string? CategoryId { get; set; }    // Category ObjectId referansı
public string? SupplierId { get; set; }    // Supplier ObjectId referansı

// Sale.cs
public string? CustomerId { get; set; }    // Customer ObjectId referansı
public List<SaleItem> Items { get; set; }  // Embedded collection
```

---

## 📈 Performans Optimizasyonları

1. **SingleOrDefaultAsync()**: Veritabanı sorgusu sonuçlarını single entity olarak alır
2. **SortBy()**: Koleksiyonda indexing için sıralama
3. **FindAsync()**: Asenkron sorgu (UI bloklama yok)
4. **EstimatedDocumentCount()**: Hızlı sayısal kontrol
5. **Caching**: ViewBag ile dropdown cache'i

---

## 🚀 Genişletilebilirlik

### Yeni Entite Ekleme Adımları

1. **Model oluştur** (`Models/NewEntity.cs`)

   ```csharp
   public class NewEntity
   {
       [BsonId]
       public string? Id { get; set; }
       [BsonElement("field")]
       public string Field { get; set; }
   }
   ```

2. **MongoDBContext'e ekle**

   ```csharp
   public IMongoCollection<NewEntity> NewEntities
       => _database.GetCollection<NewEntity>("NewEntities");
   ```

3. **Kontrolcü oluştur**

   ```csharp
   public class NewEntitiesController : Controller
   {
       private readonly MongoDBContext _context;
   }
   ```

4. **CRUD eylemlerini uygula** (Index, Create, Edit, Delete)

5. **View'ler oluştur** (`Views/NewEntity/`)

---

## 📞 Hata Yönetimi

```csharp
try
{
    await _context.Products.InsertOneAsync(product);
}
catch (MongoWriteException ex)
{
    // Duplicate key, validation errors
    ModelState.AddModelError("", ex.Message);
    return View(product);
}
catch (Exception ex)
{
    // Genel hata
    return StatusCode(500, $"Sunucu hatası: {ex.Message}");
}
```

---

## 📌 Önemli Notlar

1. **ObjectId Dönüşümü**: MongoDB'de string olarak tutulan ObjectId'ler C#'ta `[BsonRepresentation(BsonType.ObjectId)]` ile belirtilir
2. **Async/Await**: Tüm veritabanı işlemleri asenkron (`ToListAsync()`, `FirstOrDefaultAsync()`)
3. **Environment Variables**: Gizli bilgiler `.env` dosyasında tutulur
4. **SeedData**: İlk çalıştırmada varsayılan kategoriler yüklenir
5. **Bootstrap 5**: Tüm UI Bootstrap 5 ile yapılandırılmıştır

---

**Daha fazla soru? GitHub Issues'de soru açın!**
