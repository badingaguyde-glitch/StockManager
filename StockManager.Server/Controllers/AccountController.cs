using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using System.Security.Claims;
using System.Text;
using StockManager.Server.Data;
using StockManager.Server.Models;
using StockManager.Server.Services;

namespace StockManager.Server.Controllers;

public class AccountController : Controller
{
    private const string PasswordResetVerifiedKey = "PasswordResetVerifiedEmail";

    private readonly MongoDBContext _context;
    private readonly PasswordHasher<User> _passwordHasher;
    private readonly IEmailService _emailService;
    private readonly IPasswordValidator _passwordValidator;
    private readonly IPasswordResetService _passwordResetService;
    private readonly PasswordSecuritySettings _securitySettings;

    public AccountController(
        MongoDBContext context,
        IPasswordValidator passwordValidator,
        IPasswordResetService passwordResetService,
        IEmailService emailService,
        IOptions<PasswordSecuritySettings> securitySettings)
    {
        _context = context;
        _emailService = emailService;
        _passwordHasher = new PasswordHasher<User>();
        _passwordValidator = passwordValidator;
        _passwordResetService = passwordResetService;
        _securitySettings = securitySettings.Value;
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
            new Claim(ClaimTypes.Name, user.Username ?? ""),
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
    // ŞİFRE SIFIRLAMA: KOD TALEP ET
    // ══════════════════════════════════════════════
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _context.Users.Find(u => u.Email == model.Email).FirstOrDefaultAsync();

        // Güvenlik: kullanıcı bulunamasa bile aynı mesajı göster
        if (user != null)
        {
            var code = await _passwordResetService.GenerateAndStoreCodeAsync(user);
            await _emailService.SendPasswordResetCodeAsync(
                user.Email, code, _securitySettings.CodeValidityMinutes);
        }

        TempData["InfoMessage"] =
            "E-posta adresiniz kayıtlıysa, doğrulama kodu gönderildi. Kod 5 dakika geçerlidir.";
        return RedirectToAction(nameof(VerifyResetCode), new { email = model.Email });
    }

    // ══════════════════════════════════════════════
    // ŞİFRE SIFIRLAMA: KOD DOĞRULAMA
    // ══════════════════════════════════════════════
    [HttpGet]
    public IActionResult VerifyResetCode(string? email)
    {
        ViewBag.CodeValidityMinutes = _securitySettings.CodeValidityMinutes;
        ViewBag.MaxAttempts = _securitySettings.MaxCodeAttempts;
        return View(new VerifyResetCodeViewModel { Email = email ?? "" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyResetCode(VerifyResetCodeViewModel model)
    {
        ViewBag.CodeValidityMinutes = _securitySettings.CodeValidityMinutes;
        ViewBag.MaxAttempts = _securitySettings.MaxCodeAttempts;

        if (!ModelState.IsValid)
            return View(model);

        var user = await _context.Users.Find(u => u.Email == model.Email).FirstOrDefaultAsync();
        if (user == null)
        {
            ModelState.AddModelError("", "Geçersiz doğrulama kodu.");
            return View(model);
        }

        var verifyResult = await _passwordResetService.VerifyCodeAsync(user, model.Code);
        if (!verifyResult.Success)
        {
            ModelState.AddModelError("", verifyResult.Message);
            return View(model);
        }

        HttpContext.Session.SetString(PasswordResetVerifiedKey, model.Email);
        return RedirectToAction(nameof(ResetPassword), new { email = model.Email });
    }

    // ══════════════════════════════════════════════
    // ŞİFRE SIFIRLAMA: YENİ ŞİFRE BELİRLEME
    // ══════════════════════════════════════════════
    [HttpGet]
    public IActionResult ResetPassword(string? email)
    {
        var verifiedEmail = HttpContext.Session.GetString(PasswordResetVerifiedKey);
        if (string.IsNullOrEmpty(verifiedEmail) || verifiedEmail != email)
        {
            TempData["ErrorMessage"] = "Önce doğrulama kodunu onaylamanız gerekmektedir.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        ViewBag.PasswordRequirements = _passwordValidator.GetRequirementsDescription();
        return View(new ResetPasswordViewModel { Email = email ?? "" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        ViewBag.PasswordRequirements = _passwordValidator.GetRequirementsDescription();

        var verifiedEmail = HttpContext.Session.GetString(PasswordResetVerifiedKey);
        if (string.IsNullOrEmpty(verifiedEmail) || verifiedEmail != model.Email)
        {
            TempData["ErrorMessage"] = "Oturum süresi doldu. Lütfen işlemi baştan başlatınız.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        if (!ModelState.IsValid)
            return View(model);

        var passwordError = _passwordValidator.Validate(model.NewPassword);
        if (passwordError != null)
        {
            ModelState.AddModelError(nameof(model.NewPassword), passwordError);
            return View(model);
        }

        var user = await _context.Users.Find(u => u.Email == model.Email).FirstOrDefaultAsync();
        if (user == null)
        {
            TempData["ErrorMessage"] = "Kullanıcı bulunamadı.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        user.PasswordHash = _passwordHasher.HashPassword(user, model.NewPassword);
        await _passwordResetService.ClearResetCodeAsync(user);

        var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
        var update = Builders<User>.Update.Set(u => u.PasswordHash, user.PasswordHash);
        await _context.Users.UpdateOneAsync(filter, update);

        HttpContext.Session.Remove(PasswordResetVerifiedKey);
        TempData["SuccessMessage"] = "Şifreniz başarıyla güncellendi. Yeni şifrenizle giriş yapabilirsiniz.";
        return RedirectToAction(nameof(Login));
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

        string username = GenerateRandomUsername(email);
        string password = GenerateStrongRandomPassword();

        var newUser = new User
        {
            Username = username,
            Email = email,
            Role = role
        };

        newUser.PasswordHash = _passwordHasher.HashPassword(newUser, password);
        await _context.Users.InsertOneAsync(newUser);

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

        ViewBag.PasswordRequirements = _passwordValidator.GetRequirementsDescription();
        return View(user);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(
        string username,
        string email,
        string? currentPassword,
        string? newPassword,
        string? confirmPassword)
    {
        var currentUsername = User.Identity?.Name;
        var user = await _context.Users.Find(u => u.Username == currentUsername).FirstOrDefaultAsync();
        if (user == null) return NotFound();

        ViewBag.PasswordRequirements = _passwordValidator.GetRequirementsDescription();

        var wantsPasswordChange = !string.IsNullOrEmpty(newPassword) ||
                                  !string.IsNullOrEmpty(currentPassword) ||
                                  !string.IsNullOrEmpty(confirmPassword);

        if (wantsPasswordChange)
        {
            if (string.IsNullOrEmpty(currentPassword))
            {
                ModelState.AddModelError("", "Şifre değiştirmek için mevcut şifrenizi girmelisiniz.");
                return View(user);
            }

            if (string.IsNullOrEmpty(newPassword))
            {
                ModelState.AddModelError("", "Yeni şifre gereklidir.");
                return View(user);
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError("", "Yeni şifreler eşleşmiyor.");
                return View(user);
            }

            var currentVerify = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, currentPassword);
            if (currentVerify == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError("", "Mevcut şifre hatalı.");
                return View(user);
            }

            var passwordError = _passwordValidator.Validate(newPassword, username);
            if (passwordError != null)
            {
                ModelState.AddModelError("", passwordError);
                return View(user);
            }
        }

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

        // E-posta ve şifre değişikliği isteklerini belirle
        bool isEmailChangeRequested = user.Email != email;
        bool isPasswordChangeRequested = wantsPasswordChange;
        bool requiresVerification = isEmailChangeRequested || isPasswordChangeRequested;

        string? pendingHash = null;
        string? pendingEmail = null;
        string? resetCode = null;
        DateTime? resetCodeExpired = null;

        var originalEmail = user.Email; // Doğrulama kodunu alacak olan aktif güvenli e-posta

        if (requiresVerification)
        {
            // 6 haneli rastgele kod üretimi
            var random = new Random();
            resetCode = random.Next(100000, 999999).ToString();
            resetCodeExpired = DateTime.UtcNow.AddMinutes(_securitySettings.CodeValidityMinutes);

            if (isPasswordChangeRequested)
            {
                // Şifreyi hemen aktif etmiyoruz, onay bekleyen hash olarak tutuyoruz
                pendingHash = _passwordHasher.HashPassword(user, newPassword!);
            }

            if (isEmailChangeRequested)
            {
                // E-postanın başka hesapta kullanılıp kullanılmadığını kontrol et
                var emailExists = await _context.Users.Find(u => u.Email == email && u.Id != user.Id).AnyAsync();
                if (emailExists)
                {
                    ModelState.AddModelError("Email", "Bu e-posta adresi başka bir hesap tarafından kullanılıyor.");
                    return View(user);
                }
                // E-postayı onaylanana kadar geçici alana yazıyoruz
                pendingEmail = email;
            }

            // E-posta doğrulama kodunu MEVCUT doğrulanmış e-posta adresine gönder (Güvenlik gereği)
            await _emailService.SendPasswordResetCodeAsync(originalEmail, resetCode, _securitySettings.CodeValidityMinutes);
        }

        var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
        
        var updateBuilder = Builders<User>.Update
            .Set(u => u.Username, user.Username);

        if (requiresVerification)
        {
            updateBuilder = updateBuilder
                .Set(u => u.PasswordResetCode, resetCode)
                .Set(u => u.PasswordResetCodeExpireAt, resetCodeExpired)
                .Set(u => u.PendingPasswordHash, pendingHash)
                .Set(u => u.PendingEmail, pendingEmail);
        }
        else
        {
            // Eğer doğrulama gerekmiyorsa e-postayı doğrudan güncelle
            updateBuilder = updateBuilder
                .Set(u => u.Email, user.Email);
        }

        await _context.Users.UpdateOneAsync(filter, updateBuilder);

        if (requiresVerification)
        {
            TempData["success"] = "Profil değişikliklerini onaylamak için doğrulama kodu mevcut e-posta adresinize gönderildi.";
            return RedirectToAction(nameof(VerifyPasswordChange));
        }

        // Oturumu yenile (Kullanıcı adı değişmiş olabilir)
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

    private string GenerateRandomUsername(string email)
    {
        var prefix = email.Split('@')[0].Replace(".", "").ToLower();
        return $"{prefix}_{Random.Shared.Next(1000, 9999)}";
    }

    private string GenerateStrongRandomPassword()
    {
        const string upper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lower = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string special = "!@#$%^&*";
        const string all = upper + lower + digits + special;

        var length = Math.Max(_securitySettings.MinPasswordLength, 12);
        var password = new StringBuilder();
        password.Append(upper[Random.Shared.Next(upper.Length)]);
        password.Append(lower[Random.Shared.Next(lower.Length)]);
        password.Append(digits[Random.Shared.Next(digits.Length)]);
        password.Append(special[Random.Shared.Next(special.Length)]);

        for (var i = password.Length; i < length; i++)
            password.Append(all[Random.Shared.Next(all.Length)]);

        var chars = password.ToString().ToCharArray();
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = Random.Shared.Next(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);
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

        // Doğrulama başarılı: Geçici hash'i ve geçici e-postayı kalıcı hale getir
        if (!string.IsNullOrEmpty(user.PendingPasswordHash))
        {
            user.PasswordHash = user.PendingPasswordHash;
        }
        if (!string.IsNullOrEmpty(user.PendingEmail))
        {
            user.Email = user.PendingEmail;
        }

        // Doğrulama alanlarını temizle
        user.PasswordResetCode = null;
        user.PasswordResetCodeExpireAt = null;
        user.PendingPasswordHash = null;
        user.PendingEmail = null;

        var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
        var update = Builders<User>.Update
            .Set(u => u.PasswordHash, user.PasswordHash)
            .Set(u => u.Email, user.Email)
            .Set(u => u.PasswordResetCode, user.PasswordResetCode)
            .Set(u => u.PasswordResetCodeExpireAt, user.PasswordResetCodeExpireAt)
            .Set(u => u.PendingPasswordHash, user.PendingPasswordHash)
            .Set(u => u.PendingEmail, user.PendingEmail);

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
