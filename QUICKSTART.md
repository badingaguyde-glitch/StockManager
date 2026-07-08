# ⚡ Hızlı Başlangıç Kılavuzu

Sadece **5 dakika** içinde StockManager'ı çalıştırın!

## 1️⃣ MongoDB Başlatın (2 dakika)

### Windows:

```bash
# PowerShell'i yönetici olarak açın
mongod
```

### Mac:

```bash
brew services start mongodb-community
```

### Linux (Ubuntu):

```bash
sudo systemctl start mongod
```

✅ Başarılı: Terminalde "waiting for connections on port 27017" görmeniz gerekir.

---

## 2️⃣ Projeyi Hazırlayın (2 dakika)

```bash
# Proje klasörüne gidin
cd StockManager/StockManager.Server

# Bağımlılıkları yükleyin
dotnet restore

# Uygulamayı çalıştırın
dotnet run
```

📍 **Sonuç**: Tarayıcı otomatik açılır veya şu adrese gidin:

```
https://localhost:5001
```

---

## 3️⃣ İlk Verilerinizi Oluşturun (1 dakika)

### A. Kategori Ekleyin:

1. Dashboard'dan "Kategoriler" → "Yeni Kategori"
2. Adı girin: `Elektronik`
3. "Kaydet" tıklayın

### B. Tedarikçi Ekleyin:

1. "Tedarikçiler" → "Yeni Tedarikçi"
2. Şirket adı: `ABC Ticaret`
3. Telefon: `0212-123-4567`
4. "Kaydet" tıklayın

### C. Ürün Ekleyin:

1. "Ürünler" → "Yeni Ürün"
2. Ürün adı: `Dizüstü Bilgisayar`
3. Fiyatlar:
   - Alış: `2000`
   - Satış: `2500`
4. Stok: `10`
5. Kategori: `Elektronik` seçin
6. Tedarikçi: `ABC Ticaret` seçin veya "ABC Tech" adıyla yeni tedarikçi oluşturun
7. "Kaydet" tıklayın

### D. Müşteri Ekleyin:

1. "Müşteriler" → "Yeni Müşteri"
2. Ad: `Ahmet Demir`
3. Telefon: `0533-555-1234`
4. "Kaydet" tıklayın

### E. Satış Yapın:

1. "Satışlar" → "Yeni Satış"
2. Müşteri: `Ahmet Demir` seçin
3. Ürün: `Dizüstü Bilgisayar` ekleyin (adet: 1)
4. Ödeme: "Nakit" seçin
5. "Satışı Tamamla" tıklayın

✅ **Hazır!** İlk satışınızı yaptınız! 🎉

---

## 🎮 Sonraki Adımlar

### Dashboard'da göz atın:

- Toplam ürün, müşteri, tedarikçi sayıları
- Stok değeri
- Satış istatistikleri

### Diğer Özellikler:

- **📋 Stok Hareketleri**: Ürün girişi/çıkışı kaydedin
- **📈 Raporlar**: Satış ve stok raporlarını görüntüleyin
- **🧾 Fatura**: Satıştan PDF fatura oluşturun
- **⚠️ Stok Uyarıları**: Düşük stok ürünleri takip edin

---

## ⚙️ Stripe Ödeme Entegrasyonu (Opsiyonel)

Kredi kartı ödeme almak istiyorsanız:

1. **Stripe hesabı oluşturun**: https://stripe.com
2. **Test anahtarlarını alın**: https://dashboard.stripe.com/test/apikeys
3. **.env dosyası oluşturun**:
   ```env
   STRIPE_PUBLIC_KEY=pk_test_XXXXXXX
   STRIPE_SECRET_KEY=sk_test_XXXXXXX
   STRIPE_CURRENCY=try
   ```
4. **Uygulamayı yeniden başlatın**: `dotnet run`
5. Satışta ödeme yöntemi olarak "Kart (Stripe)" seçebilirsiniz

---

## 🆘 Sorunlar?

| Problem                            | Çözüm                                                  |
| ---------------------------------- | ------------------------------------------------------ |
| **"MongoDB bağlantı hatası"**      | MongoDB çalışıyor mu? `mongod` komutu ile kontrol edin |
| **"Port 5001 zaten kullanılıyor"** | `dotnet run --urls "https://localhost:5002"`           |
| **"Ürün formu açılmıyor"**         | `dotnet clean && dotnet build`                         |
| **Stripe ödeme başarısız**         | `.env` dosyasında anahtarları kontrol edin             |

---

## 📚 Daha Fazla Bilgi

Detaylı rehber için: [README.md](README.md)

---

**Başlamaya hazır mısınız? 🚀**

`dotnet run` yazıp ENTER tuşuna basın!
