# 📦 StockManager - Profesyonel Stok Yönetim Sistemi

## 🎯 Proje Hakkında

**StockManager**, küçük ve orta ölçekli işletmeler için tasarlanmış modern bir **stok yönetim, satış ve finans takibi sistemi**dir. Ürünlerinizi, müşterilerinizi, tedarikçilerinizi ve satışlarınızı tek bir yerden yönetebilirsiniz.

Sistem, işinizin büyümesine uyum sağlayacak şekilde inşa edilmiştir ve gelecekteki genişletmeler için hazırdır.

---

## ✨ Temel Özellikler

### 📊 Dashboard (Ana Sayfa)

- Anlık iş özeti görüntüleme
- Kategori, ürün, müşteri ve tedarikçi sayıları
- Toplam stok değeri hesaplaması
- Hızlı erişim butonları

### 📦 Ürün Yönetimi

- Ürün ekleme, düzenleme ve silme
- Barkod desteği (otomatik ve manuel)
- Fiyat yönetimi (alış ve satış fiyatı)
- Stok miktarı takibi
- Düşük stok uyarıları
- Ürün resimleri ve açıklamaları
- **Tedarikçi seçimi**: Mevcut tedarikçiyi seçin veya yeni tedarikçi adı girin

### 🏷️ Kategori Yönetimi

- Ürün kategorilerini oluşturma ve düzenleme
- Kategori bazlı ürün filtreleme
- Varsayılan kategoriler: Electronics, Clothing, Books

### 👥 Müşteri Yönetimi

- Müşteri bilgileri (ad, telefon, e-posta)
- Müşteri bakiyesi takibi
- Satış geçmişi
- İletişim bilgileri arşivlemesi

### 🤝 Tedarikçi Yönetimi

- Tedarikçi profili oluşturma
- İletişim bilgileri saklama
- Tedarikçi bakiyesi
- Borç ve alacak takibi

### 💰 Satış Yönetimi

- Satış kaydı oluşturma
- Fatura numarası otomatik oluşturma
- Satış öğeleri yönetimi
- Ödeme yöntemi seçimi:
  - Nakit
  - Çek
  - Kart (Stripe entegrasyonu)
  - Havale/EFT
  - Taksit

### 💳 Ödeme İşlemleri

- **Stripe Entegrasyonu**: Kredi kartı ödeme alımı
- Güvenli ödeme işlemleri
- Ödeme durumu takibi
- Para birimi desteği

### 📋 Stok Hareketleri

- Giriş (satın alma, dış ticaret)
- Çıkış (satış, iadeler)
- Düzeltme (envanter sayımı sonrası)
- Tarih bazlı stok raporları
- Tedarikçi bazlı stok takibi

### 📈 Raporlama

- Ürün stok durumu raporları
- Satış raporları
- Müşteri satın alma geçmişi
- Tedarikçi bazlı alış raporları
- Dönemsel analiz

### 🧾 PDF Fatura

- Profesyonel fatura oluşturma
- QuestPDF kütüphanesi ile yüksek kaliteli görünüm
- Muhasebe uyumu
- Baskı hazır format

---

## 🔧 Teknik Detaylar

### Teknoloji Stack

| Bileşen               | Teknoloji        | Sürüm     |
| --------------------- | ---------------- | --------- |
| **Sunucu Ortamı**     | ASP.NET Core     | 8.0       |
| **Web Framework**     | ASP.NET Core MVC | 8.0       |
| **Veritabanı**        | MongoDB          | -         |
| **Ödeme Sistemi**     | Stripe           | 52.1.0    |
| **PDF Oluşturma**     | QuestPDF         | 2024.12.0 |
| **Ortam Yönetimi**    | dotenv.net       | 4.0.2     |
| **API Dokumentasyon** | Swagger/OpenAPI  | 6.6.2     |

### Mimari Yapısı

```
StockManager/
├── Controllers/          # HTTP isteklerini işleyen kontrolcüler
│   ├── HomeController    # Dashboard ve ana sayfa
│   ├── ProductsController # Ürün CRUD işlemleri
│   ├── CategoriesController # Kategori yönetimi
│   ├── SuppliersController # Tedarikçi yönetimi
│   ├── CustomersController # Müşteri yönetimi
│   ├── SalesController   # Satış işlemleri
│   ├── StockMovementsController # Stok hareketi takibi
│   └── ReportsController # Raporlama işlemleri
│
├── Models/               # Veri modelleri
│   ├── Product          # Ürün sınıfı
│   ├── Category         # Kategori sınıfı
│   ├── Supplier         # Tedarikçi sınıfı
│   ├── Customer         # Müşteri sınıfı
│   ├── Sale             # Satış sınıfı
│   ├── SaleItem         # Satış öğesi
│   ├── StockMovement    # Stok hareketi
│   └── Enums            # Sabitler (PaymentType, MovementType vb.)
│
├── Views/                # Razor HTML şablonları
│   ├── Home             # Dashboard sayfası
│   ├── Products         # Ürün listeleme, ekleme, düzenleme
│   ├── Categories       # Kategori sayfaları
│   ├── Suppliers        # Tedarikçi sayfaları
│   ├── Customers        # Müşteri sayfaları
│   ├── Sales            # Satış sayfaları
│   ├── StockMovements   # Stok hareketi sayfaları
│   └── Shared           # Ortak şablonlar (_Layout.cshtml)
│
├── Data/
│   └── MongoDBContext   # MongoDB bağlantısı ve koleksiyonlar
│
├── Services/            # İş mantığı servisleri
│   ├── StripePaymentService # Stripe ödeme işlemleri
│   └── ReceiptPdfService # PDF fatura oluşturma
│
├── wwwroot/             # Statik dosyalar
│   ├── css/             # Stil dosyaları (Bootstrap)
│   └── js/              # JavaScript dosyaları
│
├── Program.cs           # Uygulamanın başlangıç noktası
├── appsettings.json     # Yapılandırma dosyası
└── .env                 # Gizli yapılandırma (Stripe anahtarları vb.)
```

### Veri Modelleri

#### 📦 Product (Ürün)

```csharp
- Id: Benzersiz kimlik (MongoDB ObjectId)
- Barcode: Ürün barkodu
- Name: Ürün adı
- Description: Açıklama
- PurchasePrice: Alış fiyatı
- SalePrice: Satış fiyatı
- Quantity: Mevcut stok miktarı
- LowStockThreshold: Düşük stok uyarı seviyesi
- CategoryId: İlişkili kategori
- SupplierId: İlişkili tedarikçi
```

#### 💰 Sale (Satış)

```csharp
- Id: Benzersiz kimlik
- SaleDate: Satış tarihi
- InvoiceNumber: Fatura numarası
- TotalAmount: Toplam tutar
- CustomerId: Alıcı müşteri
- PaymentType: Ödeme yöntemi (Cash, Check, Card, Transfer, Installment)
- StripePaymentIntentId: Stripe ödeme kimliği
- Items: Satışta bulunan ürünler listesi
```

#### 🔄 StockMovement (Stok Hareketi)

```csharp
- Id: Benzersiz kimlik
- ProductId: Ürün kimliği
- MovementType: Giriş/Çıkış/Düzeltme
- Quantity: Hareket miktarı
- Date: Hareket tarihi
- SupplierId: Tedarikçi (giriş için)
- Notes: Açıklamalar
```

---

## 🚀 Kurulum ve Çalıştırma

### Ön Gereksinimler

1. **.NET 8.0 SDK** - [İndir](https://dotnet.microsoft.com/download)
2. **MongoDB** - [İndir](https://www.mongodb.com/try/download/community)
3. **Visual Studio Code veya Visual Studio** - [İndir](https://code.visualstudio.com/)
4. **Stripe Hesabı** (opsiyonel, sadece kartlı ödeme için) - [Kayıt](https://stripe.com)

### Adım 1: MongoDB'yi Başlatın

Windows'ta:

```bash
# MongoDB kurulu ise, Services'ten başlatın
# veya terminal ile:
mongod
```

Linux/Mac:

```bash
brew services start mongodb-community
# veya
mongod
```

### Adım 2: Projeyi Klonlayın

```bash
git clone <repository-url>
cd StockManager
```

### Adım 3: Ortam Değişkenlerini Ayarlayın

`.env` dosyasını proje kökünde oluşturun:

```env
# MongoDB Bağlantısı
MONGODB_CONNECTION_STRING=mongodb://localhost:27017

# Stripe Anahtarları (opsiyonel)
STRIPE_PUBLIC_KEY=pk_test_...
STRIPE_SECRET_KEY=sk_test_...
STRIPE_CURRENCY=try
```

### Adım 4: Bağımlılıkları Yükleyin

```bash
cd StockManager.Server
dotnet restore
```

### Adım 5: Uygulamayı Çalıştırın

```bash
dotnet run
```

Tarayıcınızda açın: **https://localhost:5001**

---

## 📱 Kullanım Kılavuzu

### İlk Adımlar

1. **Giriş**: Uygulamayı açtığınızda Dashboard'a hoş geldiniz
2. **Kategoriler**: Ürün kategorilerini oluşturun (Elektronik, Giyim vb.)
3. **Tedarikçiler**: Satın aldığınız tedarikçileri kaydedin
4. **Ürünler**: Ürünleri ekleyin ve her ürüne kategori ve tedarikçi atayın
5. **Müşteriler**: Müşteri bilgilerini kaydedin
6. **Satış**: Ürün satışı gerçekleştirin ve ödeme yapın

### 💡 İpuçları

- **Düşük Stok Uyarısı**: Ürünlerin `LowStockThreshold` değerini ayarlayın, sistem otomatik uyarı verecek
- **Barkod**: Barkod okuyucudan veya manuel olarak girebilirsiniz
- **Stok Kontrolü**: Satış sırasında otomatik stok azalması
- **Fatura**: Her satıştan PDF fatura oluşturabilirsiniz

---

## 🔐 Güvenlik

- **HTTPS**: Tüm iletişim şifreli
- **Environment Variables**: Gizli anahtarlar `.env` dosyasında depolanır
- **CORS**: Gelecekteki API güvenliği için hazır
- **Input Validation**: Tüm girişler doğrulanır

---

## 🐛 Sorun Giderme

### MongoDB Bağlantı Hatası

```
Çözüm: MongoDB servisinin çalıştığından emin olun (mongod)
```

### "Port 5001 zaten kullanılıyor" Hatası

```bash
# Farklı port kullanın:
dotnet run --urls "https://localhost:5002"
```

### Stripe Ödeme Hatası

```
Çözüm: .env dosyasında Stripe anahtarlarını kontrol edin
Stripe'dan test anahtarlarını alın: https://dashboard.stripe.com/test/apikeys
```

---

## 📊 Raporlar

Sistem şu raporları destekler:

1. **Stok Raporu**: Ürün başına mevcut stok
2. **Satış Raporu**: Tarih aralığına göre satışlar
3. **Müşteri Raporu**: Müşteri satın alma geçmişi
4. **Tedarikçi Raporu**: Tedarikçi bazlı alışlar
5. **Kar-Zarar Raporu**: Giriş-Çıkış fiyat farklılığı

---

## 🔮 Gelecek Özellikler

- [ ] Çok dilli arayüz (İngilizce, Türkçe, Arapça)
- [ ] Gelişmiş analitik paneli
- [ ] Mobil uygulama
- [ ] E-ticaret entegrasyonu
- [ ] Lojistik takibi
- [ ] Vergi ve muhasebe entegrasyonu
- [ ] Kullanıcı rolleri ve izinleri
- [ ] İş akışı otomasyonu

---

## 👨‍💻 Geliştirme

### Proje Yapısı

- **C#**: Backend kodlama dili
- **Razor**: HTML şablonları
- **Bootstrap 5**: Kullanıcı arayüzü
- **MongoDB**: NoSQL veritabanı
- **Stripe API**: Ödeme işlemleri

### Yeni Özellik Ekleme

1. Model oluşturun (`Models/` klasöründe)
2. MongoDB koleksiyonunu kaydedin (`MongoDBContext.cs`)
3. Controller oluşturun (`Controllers/` klasöründe)
4. View dosyaları oluşturun (`Views/` klasöründe)
5. Rotaları güncelleyin (`Program.cs`)

---

## 📞 Destek

Sorularınız veya önerilerin için:

- **GitHub Issues**: Problemleri raporla
- **Discussions**: Yeni fikirler tartış
- **Email**: emre@example.com

---

## 📄 Lisans

Bu proje MIT Lisansı altında sunulmaktadır.

---

## 🙏 Katkıda Bulunma

Katkılarınız hoş geldiniz! Lütfen şunları yapın:

1. Projeyi fork edin
2. Yeni bir branch oluşturun (`git checkout -b feature/AmazingFeature`)
3. Değişiklikleri commit edin (`git commit -m 'Add some AmazingFeature'`)
4. Branch'ı push edin (`git push origin feature/AmazingFeature`)
5. Pull Request açın

---

## 📈 İstatistikler

- **Kontrolcüler**: 8 adet
- **Veri Modelleri**: 13 adet
- **Görünümler**: 25+ adet
- **Kod Satırı**: 5000+
- **Veritabanı Koleksiyonları**: 6 adet

---

**Son Güncelleme**: 8 Temmuz 2026

Mutlu Yönetimi! 🎉
