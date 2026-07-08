# ❓ Sıkça Sorulan Sorular (FAQ)

---

## 🎯 Genel Sorular

### StockManager nedir?

**Cevap**: StockManager, küçük ve orta ölçekli işletmeler için tasarlanmış modern bir stok yönetim sistemidir. Ürünleri, müşterileri, tedarikçileri ve satışları yönetmek için geliştirilmiştir. Ayrıca kredi kartı ödeme (Stripe), PDF fatura ve detaylı raporlama özellikleri içerir.

### Ne tür işletmeler tarafından kullanılabilir?

**Cevap**:

- Perakende mağazaları (elektronik, giyim, kitap)
- Depo ve dağıtım merkezleri
- Toptan ticaret işletmeleri
- Kargo ve lojistik şirketleri
- Restoran ve kafe zinciri (POS sistemi)

### Kaç kullanıcı destekler?

**Cevap**: Mevcut versiyonda sadece **1 kullanıcı** desteklenmiştir. Gelecek versiyonda **kullanıcı yönetimi ve roller** eklenecektir.

---

## 💾 Kurulum ve Konfigürasyon

### MongoDB kurmadan kullanabilir miyim?

**Cevap**: **Hayır**. MongoDB mutlaka kurulu ve çalışıyor olmalıdır. MongoDB olmadan veri saklayamazsınız.

### Docker'da çalıştırabilir miyim?

**Cevap**: Henüz Dockerfile yok, ancak siz oluşturabilirsiniz:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0
WORKDIR /app
COPY . .
RUN dotnet build
CMD ["dotnet", "run"]
```

### Cloud'a deploy etmek istiyorum, ne yapmalıyım?

**Cevap**:

- **Azure**: Azure App Service + Azure Cosmos DB (MongoDB uyumlu)
- **AWS**: EC2 + MongoDB Atlas
- **Heroku**: Buildpack ile (ücretsiz tier sona erdi)
- **DigitalOcean**: App Platform + Managed MongoDB

### .env dosyasını neden oluştururum?

**Cevap**: Gizli bilgileri (Stripe anahtarları, MongoDB parolası) kaynak kodda tutmamak için. `.env` dosyası `.gitignore`'da olduğundan Git'e yüklenmez.

---

## 🛠️ Teknik Sorular

### ASP.NET Core'u bilmek zorunday mı?

**Cevap**: **Evet**, özellikle kodda değişiklik yapacaksanız. İlköğretim düzeyinde C# bilgisi ve MVC mimarisi bilmek yeterlidir. Başlangıç için videoları izleyebilirsiniz.

### MongoDB yerine SQL Server kullanabilir miyim?

**Cevap**: **Hayır**. Sistem MongoDB'ye özel kurulmuştur (BSON attributes vb.). SQL Server'a geçmek için tüm modelleri ve kontroller'leri yeniden yazmanız gerekir.

### API nasıl erişirim?

**Cevap**: Mevcut versiyonda **API yok**, sadece MVC web sitesi var. API eklemek istiyorsanız:

1. Swagger zaten kurulu (`Swashbuckle.AspNetCore`)
2. Controllers'lara `[ApiController]` attribute ekleyin
3. Action'ları `IActionResult` yerine `async Task<ActionResult<T>>` döndürecek şekilde değiştirin

### Mobil uygulama yapabilir miyim?

**Cevap**: Evet, eğer API eklersek. Şu an sadece web var.

---

## 💰 Ödeme ve Stripe

### Kredi kartı ödeme almayan işletmeler için ne yapmalıyım?

**Cevap**: Satış yapıldığında ödeme türü olarak **Nakit**, **Çek**, **Havale** veya **Taksit** seçebilirsiniz. Stripe entegrasyonu opsiyonel.

### Stripe gümrük resmiyetini kapsıyor mu?

**Cevap**: **Hayır**. Stripe gümrük resmiyeti kaplamamaktadır. Siz muhasebe/finansal işlemlerden siz sorumlusunuz.

### Hangisi daha güvenli: Stripe veya PayPal?

**Cevap**: Ikisi de **güvenli ve PCI-DSS sertifikalı**. Stripe kullanıcıları genellikle **daha az ücret** ödediğini belirtir.

### Para birimi değiştirebilir miyim?

**Cevap**: Evet! `.env` dosyasında:

```env
STRIPE_CURRENCY=usd    # USD için
STRIPE_CURRENCY=eur    # EUR için
```

---

## 📊 Veriler ve Raporlar

### Verilerimi Excel'e dışa aktarabilir miyim?

**Cevap**: **Şu an hayır**. Gelecek versiyonda CSV/Excel export eklenecektir.

### Tarihsel verileri görebilir miyim?

**Cevap**: Evet! **Stok Hareketleri** sayfasında tarih aralığı seçerek geçmiş verilerini görebilirsiniz.

### Raporları otomatik olarak e-posta ile gönderebilesim?

**Cevap**: **Şu an hayır**. E-mail entegrasyonu gelecek versiyonda eklenecektir.

### Satış raporunu indirmek istiyorum

**Cevap**:

1. "Raporlar" → "Satış Raporu"
2. Tarih aralığını seçin
3. Ekranda görünen tablo, sağ tıkla → "Sayfayı Kaydet" vb.
4. Gelecek versiyonda PDF/Excel export olacak

---

## 🐛 Sorun Giderme

### "MongoDB bağlantı hatası" alıyorum

**Cevap**:

```bash
# 1. MongoDB çalışıyor mu kontrol edin
mongod

# 2. Connection string doğru mu (.env dosyasında)
# MONGODB_CONNECTION_STRING=mongodb://localhost:27017

# 3. MongoDB port 27017 açık mı
netstat -an | findstr 27017
```

### "Ürün ekleyemiyorum, form boş gidiyor"

**Cevap**:

```
1. Browser console'u aç (F12)
2. Network sekmesinde POST isteğine tıkla
3. Response kısmını kontrol et
4. Genellikle ModelState hatası olur
5. [Validation Error] görürsen form geçersiz demektir
```

### Sayfaya girişte "404 Not Found" hatası

**Cevap**:

```bash
# 1. Uygulamayı temiz başlatın
dotnet clean
dotnet build
dotnet run

# 2. Komut satırında Route'lar görülüyor mu?
# GET /Products/Index
# POST /Products/Create
# vb.
```

### "Port 5001 zaten kullanılıyor" hatası

**Cevap**:

```bash
# Farklı port kullanın
dotnet run --urls "https://localhost:5002"

# Veya mevcut process'i sonlandırın (Windows)
netstat -ano | findstr 5001
taskkill /PID <PID> /F
```

### Sayfanın CSS'i ve JavaScript'i yüklenmiyor

**Cevap**:

```
1. wwwroot/ klasörünün var mı kontrol edin
2. _Layout.cshtml'de doğru dosya yolu var mı?
3. Browser cache temizle (Ctrl+Shift+Delete)
4. URL bar'da https://localhost:5001 yazılı mı?
```

### Satış kaydedilmiyor

**Cevap**:

```
1. Müşteri seçildi mi?
2. Ürün eklenmiş mi?
3. ModelState hatasını kontrol et (F12)
4. MongoDB bağlantısı açık mı?
5. Ürün stoğu yeterli mi?
```

---

## 💳 Stripe Sorunları

### "Stripe payment failed" hatası alıyorum

**Cevap**:

```
1. .env dosyasında Stripe anahtarları var mı?
STRIPE_PUBLIC_KEY=pk_test_...
STRIPE_SECRET_KEY=sk_test_...

2. Test kartı mı kullanıyorsunuz?
Geçerli test kartı: 4242 4242 4242 4242
Geçersiz test kartı: 4000 0000 0000 0002

3. Kartın son kullanma tarihi ileri mi?
Test ortamında her tarih geçerlidir.
```

### Live kartlarla ödeme al göremem

**Cevap**:

```
1. .env'yi kontrol edin:
STRIPE_PUBLIC_KEY=pk_live_...  (pk_test_ DEĞİL!)
STRIPE_SECRET_KEY=sk_live_...  (sk_test_ DEĞİL!)

2. Stripe Dashboard'da "Live" sekmesi seçili mi?
3. Ödeme işleminin ücretini öde (Stripe komisyonu)
```

---

## 📱 Mobil ve Responsive

### Mobil cihazda kullanabilir miyim?

**Cevap**: **Evet**! Bootstrap 5 responsive tasarım kullandığı için mobil cihazlarda çalışır. Fakat ekran küçük olduğu için **masaüstü önerilir**.

### İOS Safari'de sorun yaşıyorum

**Cevap**:

```
1. JavaScript'i etkinleştir (Ayarlar → Safari → JavaScript)
2. Cookies'i sil
3. Başka tarayıcı dene (Chrome)
```

---

## 🔒 Güvenlik

### Verilerim güvenli mi?

**Cevap**:

- ✅ HTTPS şifreli iletişim
- ✅ MongoDB gizli key ile korumalı
- ✅ CSRF token'lar form'larda
- ⚠️ Şu an **kullanıcı giriş yok** (herkes tüm verileri görebilir)
- ⚠️ Gelecek versiyonda rol ve izin sistemi eklenecektir

### Admin şifresi var mı?

**Cevap**: **Hayır**. Şu an kimlik doğrulaması yok. Herkese açık sistem.

### Local network'te kullanabilir miyim?

**Cevap**: **Evet**!

```bash
# Lokal IP'yi bul (Windows)
ipconfig

# Bul: IPv4 Address (örn: 192.168.1.100)
dotnet run --urls "https://192.168.1.100:5001"

# Ağdaki diğer cihazlardan:
https://192.168.1.100:5001
```

---

## 📚 Öğrenme ve Geliştirme

### C# ve ASP.NET Core'u nereden öğrenebilirim?

**Cevap**:

- Microsoft Learn: https://learn.microsoft.com
- YouTube: "ASP.NET Core MVC Tutorial" ara
- Udemy: Kurslar (ücretli)
- freeCodeCamp: Ücretsiz videolar

### Kod nasıl kontribüte edebilirim?

**Cevap**:

1. GitHub'dan fork edin
2. Branch oluşturun: `git checkout -b feature/YeniOzellik`
3. Değişiklik yapın ve test edin
4. Commit edin: `git commit -m "Açıklama"`
5. Push edin: `git push origin feature/YeniOzellik`
6. Pull Request açın

### Proje lisansı nedir?

**Cevap**: **MIT Lisansı** - Özgürce kullanabilir ve değiştirebilirsiniz.

---

## 🚀 İleri Konular

### Veritabanı backup nasıl yaparım?

**Cevap**:

```bash
# MongoDB backup (Windows)
mongodump --out backup_folder

# MongoDB restore
mongorestore backup_folder
```

### Veriyi başka bir bilgisayara taşıyabilir miyim?

**Cevap**: **Evet**:

1. MongoDB'yi yedekle: `mongodump`
2. Yeni bilgisayardan restore et: `mongorestore`
3. Uygulamayı çalıştır

### Neden MSSQL veya PostgreSQL değil de MongoDB?

**Cevap**:

- MongoDB **esnek schema** sunar (ürün özelliği kolaylığı)
- NoSQL ile **hızlı prototyp** oluşturulur
- Başlangıç projesi için **minimal konfigürasyon** gerekir
- Gelecekte SQL ekleme seçeneği vardır

### Mikro servisler mimarisine geçebilir miyim?

**Cevap**: **Şu an hayır**, ama tasarım bunu destekler. Services katmanı detached tutuldu, API layer eklenebilir.

---

## 📞 Desteğe İhtiyacım Var!

### Desteğe nasıl ulaşabilirim?

**Cevap**:

- 📧 E-mail: emre@example.com
- 💬 GitHub Issues: Hata bildir ve öner
- 🤝 Discussions: Teknik sorular sor
- 👥 Forum: Topluluk desteği

### Ücretli destek var mı?

**Cevap**: **Şu an hayır**. Gelecek versiyonda enterprise desteği olabilir.

---

**Bu sorulardan herhangi birine cevap bulamamanız mı? GitHub'da soru açın! 🎉**
