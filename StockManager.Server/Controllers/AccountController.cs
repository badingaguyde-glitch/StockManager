using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;
using System.Security.Claims;
using System.Text;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Controllers;

public class AccountController : Controller
{
    private readonly MongoDBContext _context;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly StockManager.Server.Services.IEmailService _emailService;

    public AccountController(MongoDBContext context, StockManager.Server.Services.IEmailService emailService)
    {
        _context = context;
        _emailService = emailService;
        _passwordHasher = new PasswordHasher<User>();
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl)
    {
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string username, string password, string? returnUrl)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ModelState.AddModelError("", "Kullanıcı adı ve şifre gereklidir.");
            return View();
        }

        // Kullanıcı adını büyük/küçük harf duyarsız (case-insensitive) sorgula
        var user = await _context.Users.Find(u => u.Username != null && u.Username.ToLower() == username.ToLower()).FirstOrDefaultAsync();

        if (user == null)
        {
            ModelState.AddModelError("", "Geçersiz kullanıcı adı veya şifre.");
            return View();
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("", "Geçersiz kullanıcı adı veya şifre.");
            return View();
        }

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.Id ?? "")
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
        };

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity), authProperties);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    // ══════════════════════════════════════════════
    // SADECE ADMIN: YENİ PERSONEL HESABI OLUŞTURMA
    // ══════════════════════════════════════════════
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(string email, UserRole role)
    {
        if (string.IsNullOrEmpty(email))
        {
            ModelState.AddModelError("", "E-posta alanı zorunludur.");
            return View();
        }

        var emailExists = await _context.Users.Find(u => u.Email == email).AnyAsync();
        if (emailExists)
        {
            ModelState.AddModelError("", "Bu e-posta adresiyle kayıtlı bir kullanıcı zaten var.");
            return View();
        }

        // Rastgele Kullanıcı Adı ve Şifre Üretimi
        string username = GenerateRandomUsername(email);
        string password = GenerateRandomPassword(8);

        var newUser = new User
        {
            Username = username,
            Email = email,
            Role = role
        };

        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, password);
        await _context.Users.InsertOneAsync(newUser);

        // Oluşturulan bilgileri göstermek için Başarılı sayfasına yönlendir
        ViewBag.GeneratedUsername = username;
        ViewBag.GeneratedPassword = password;
        ViewBag.Role = role.ToString();
        ViewBag.Email = email;

        return View("RegistrationSuccess");
    }

    // ══════════════════════════════════════════════
    // HER KULLANICI: KENDİ PROFİLİNİ GÜNCELLEME
    // ══════════════════════════════════════════════
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> Profile()
    {
        var currentUsername = User.Identity?.Name;
        var user = await _context.Users.Find(u => u.Username == currentUsername).FirstOrDefaultAsync();
        if (user == null) return NotFound();

        return View(user);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(string username, string email, string? newPassword)
    {
        var currentUsername = User.Identity?.Name;
        var user = await _context.Users.Find(u => u.Username == currentUsername).FirstOrDefaultAsync();
        if (user == null) return NotFound();

        // Kullanıcı adı değiştiyse çakışma kontrolü
        if (user.Username != username)
        {
            var exists = await _context.Users.Find(u => u.Username == username && u.Id != user.Id).AnyAsync();
            if (exists)
            {
                ModelState.AddModelError("Username", "Bu kullanıcı adı zaten kullanımda.");
                return View(user);
            }
            user.Username = username;
        }

        user.Email = email;

        bool isPasswordChangeRequested = !string.IsNullOrEmpty(newPassword);
        string? pendingHash = null;
        string? resetCode = null;
        DateTime? resetCodeExpired = null;

        if (isPasswordChangeRequested)
        {
            // Yeni şifre girildiyse, hemen aktif etmiyoruz. Doğrulama kodu üretiyoruz.
            pendingHash = _passwordHasher.HashPassword(user, newPassword!);

            // 6 haneli rastgele kod üretimi
            var random = new Random();
            resetCode = random.Next(100000, 999999).ToString();
            resetCodeExpired = DateTime.UtcNow.AddMinutes(15); // 15 dk geçerlilik

            // E-posta gönderimi
            await _emailService.SendPasswordResetCodeAsync(user.Email, resetCode);
        }

        var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);

        var updateBuilder = Builders<User>.Update
            .Set(u => u.Username, user.Username)
            .Set(u => u.Email, user.Email);

        if (isPasswordChangeRequested)
        {
            updateBuilder = updateBuilder
                .Set(u => u.PasswordResetCode, resetCode)
                .Set(u => u.PasswordResetCodeExpireAt, resetCodeExpired)
                .Set(u => u.PendingPasswordHash, pendingHash);
        }

        await _context.Users.UpdateOneAsync(filter, updateBuilder);

        if (isPasswordChangeRequested)
        {
            TempData["success"] = "Şifre güncelleme doğrulama kodu e-posta adresinize gönderildi.";
            return RedirectToAction(nameof(VerifyPasswordChange));
        }

        // Eğer şifre değişmediyse sadece oturumu yeniliyoruz (kullanıcı adı değişmiş olabilir)
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }
    // ══════════════════════════════════════════════
    // SADECE ADMIN: PERSONEL LİSTESİ & YÖNETİMİ
    // ══════════════════════════════════════════════
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Users()
    {
        var users = await _context.Users.Find(FilterDefinition<User>.Empty).ToListAsync();
        return View(users);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeRole(string id, UserRole role)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, id);
        var update = Builders<User>.Update.Set(u => u.Role, role);
        await _context.Users.UpdateOneAsync(filter, update);
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(string id)
    {
        // Kendisini silmesini engelle
        var currentUsername = User.Identity?.Name;
        var userToDelete = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();

        if (userToDelete != null && userToDelete.Username != currentUsername)
        {
            await _context.Users.DeleteOneAsync(u => u.Id == id);
        }
        return RedirectToAction(nameof(Users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login");
    }

    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }

    // Yardımcı şifre ve kullanıcı adı oluşturma metotları
    private string GenerateRandomUsername(string email)
    {
        var prefix = email.Split('@')[0].Replace(".", "").ToLower();
        var random = new Random();
        return $"{prefix}_{random.Next(1000, 9999)}";
    }

    private string GenerateRandomPassword(int length)
    {
        // Okunabilirliği zorlaştıran benzer karakterler (l, 1, I, o, O, 0) havuzdan çıkarılmıştır.
        const string validChars = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789!@#$";
        StringBuilder res = new StringBuilder();
        Random rnd = new Random();
        while (0 < length--)
        {
            res.Append(validChars[rnd.Next(validChars.Length)]);
        }
        return res.ToString();
    }

        // ══════════════════════════════════════════════
    // ŞİFRE GÜNCELLEME İÇİN DOĞRULAMA SAYFASI VE KONTROLÜ
    // ══════════════════════════════════════════════
    [HttpGet]
    [Authorize]
    public IActionResult VerifyPasswordChange()
    {
        return View();
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPasswordChange(string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            ModelState.AddModelError("", "Doğrulama kodu gereklidir.");
            return View();
        }

        var currentUsername = User.Identity?.Name;
        var user = await _context.Users.Find(u => u.Username == currentUsername).FirstOrDefaultAsync();
        if (user == null) return NotFound();

        // Kod ve Süre Kontrolü
        if (user.PasswordResetCode != code || 
            user.PasswordResetCodeExpireAt == null || 
            user.PasswordResetCodeExpireAt < DateTime.UtcNow)
        {
            ModelState.AddModelError("", "Geçersiz veya süresi dolmuş doğrulama kodu.");
            return View();
        }

        // Doğrulama başarılı: Geçici hash'i kalıcı hale getir
        if (!string.IsNullOrEmpty(user.PendingPasswordHash))
        {
            user.PasswordHash = user.PendingPasswordHash;
        }

        // Doğrulama alanlarını temizle
        user.PasswordResetCode = null;
        user.PasswordResetCodeExpireAt = null;
        user.PendingPasswordHash = null;

        var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, user.PasswordHash)
            .Set(u => u.PasswordResetCode, user.PasswordResetCode)
            .Set(u => u.PasswordResetCodeExpireAt, user.PasswordResetCodeExpireAt)
            .Set(u => u.PendingPasswordHash, user.PendingPasswordHash);

        await _context.Users.UpdateOneAsync(filter, update);

        // Şifre değiştiği için oturumu kapatıp yeniden girişe yönlendir
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        TempData["success"] = "Şifreniz başarıyla doğrulandı ve güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";
        
        return RedirectToAction("Login");
    }

        // ══════════════════════════════════════════════
    // SADECE ADMIN: ÇALIŞAN ŞİFRESİNİ SIFIRLAMA (RESTART)
    // ══════════════════════════════════════════════
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetUserPassword(string id)
    {
        var user = await _context.Users.Find(u => u.Id == id).FirstOrDefaultAsync();
        if (user == null) return NotFound();

        // Kendi şifresini buradan sıfırlamasını engelle (Profilini kullanmalı)
        var currentUsername = User.Identity?.Name;
        if (user.Username == currentUsername)
        {
            TempData["error"] = "Kendi şifrenizi personel yönetiminden sıfırlayamazsınız. Profil ayarlarınızı kullanın.";
            return RedirectToAction(nameof(Users));
        }

        // Yeni geçici şifre üretimi (8 haneli)
        string newTemporaryPassword = GenerateRandomPassword(8);

        // Şifreyi hashle ve kaydet
        user.PasswordHash = _passwordHasher.HashPassword(user, newTemporaryPassword);
        
        // Varsa bekleyen doğrulama verilerini temizle
        user.PasswordResetCode = null;
        user.PasswordResetCodeExpireAt = null;
        user.PendingPasswordHash = null;

        var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, user.PasswordHash)
            .Set(u => u.PasswordResetCode, user.PasswordResetCode)
            .Set(u => u.PasswordResetCodeExpireAt, user.PasswordResetCodeExpireAt)
            .Set(u => u.PendingPasswordHash, user.PendingPasswordHash);

        await _context.Users.UpdateOneAsync(filter, update);

        // Yeni şifreyi e-posta ile gönder
        await _emailService.SendNewTemporaryPasswordAsync(user.Email, newTemporaryPassword);

        // Bilgileri TempData'ya alarak GET yönlendirmesi yapıyoruz (POST-Redirect-GET deseni)
        TempData["ResetUsername"] = user.Username;
        TempData["ResetEmail"] = user.Email;
        TempData["ResetRole"] = user.Role.ToString();
        TempData["ResetPassword"] = newTemporaryPassword;

        return RedirectToAction(nameof(ResetSuccess));
    }

    // ══════════════════════════════════════════════
    // SADECE ADMIN: ŞİFRE SIFIRLAMA BAŞARILI SAYFASI
    // ══════════════════════════════════════════════
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult ResetSuccess()
    {
        if (TempData["ResetUsername"] == null)
        {
            return RedirectToAction(nameof(Users));
        }

        ViewBag.Username = TempData["ResetUsername"];
        ViewBag.Email = TempData["ResetEmail"];
        ViewBag.Role = TempData["ResetRole"];
        ViewBag.GeneratedPassword = TempData["ResetPassword"];

        return View();
    }
}