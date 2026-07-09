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

    public AccountController(MongoDBContext context)
    {
        _context = context;
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

        var user = await _context.Users.Find(u => u.Username == username).FirstOrDefaultAsync();

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

        // Yeni şifre girildiyse güncelle
        if (!string.IsNullOrEmpty(newPassword))
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);
        }

        var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
        var update = Builders<User>.Update
            .Set(u => u.Username, user.Username)
            .Set(u => u.Email, user.Email)
            .Set(u => u.PasswordHash, user.PasswordHash);

        await _context.Users.UpdateOneAsync(filter, update);

        // Kullanıcı adı değiştiyse veya şifre güncellendiyse oturumu yenile
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
        const string validChars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$";
        StringBuilder res = new StringBuilder();
        Random rnd = new Random();
        while (0 < length--)
        {
            res.Append(validChars[rnd.Next(validChars.Length)]);
        }
        return res.ToString();
    }
}