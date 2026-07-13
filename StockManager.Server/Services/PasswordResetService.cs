using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using StockManager.Server.Data;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

public class PasswordResetService : IPasswordResetService
{
    private readonly MongoDBContext _context;
    private readonly PasswordSecuritySettings _settings;
    private readonly PasswordHasher<User> _hasher = new();

    public PasswordResetService(MongoDBContext context, IOptions<PasswordSecuritySettings> settings)
    {
        _context = context;
        _settings = settings.Value;
    }

    public async Task<string> GenerateAndStoreCodeAsync(User user)
    {
        var code = GenerateNumericCode(_settings.CodeLength);
        var codeHash = _hasher.HashPassword(user, code);

        user.PasswordResetCodeHash = codeHash;
        user.PasswordResetCodeExpiresAt = DateTime.UtcNow.AddMinutes(_settings.CodeValidityMinutes);
        user.PasswordResetCodeAttempts = 0;

        await UpdateResetFieldsAsync(user);
        return code;
    }

    public async Task<PasswordResetResult> VerifyCodeAsync(User user, string code)
    {
        if (string.IsNullOrEmpty(user.PasswordResetCodeHash))
            return new PasswordResetResult(false, "Geçerli bir doğrulama kodu bulunamadı. Lütfen yeni kod talep ediniz.");

        if (IsCodeCancelled(user))
        {
            await ClearResetCodeAsync(user);
            return new PasswordResetResult(false,
                $"Doğrulama kodu {_settings.MaxCodeAttempts} kez yanlış girildiği için iptal edildi. Lütfen yeni kod talep ediniz.");
        }

        if (IsCodeExpired(user))
        {
            await ClearResetCodeAsync(user);
            return new PasswordResetResult(false,
                $"Doğrulama kodunun süresi doldu ({_settings.CodeValidityMinutes} dakika). Lütfen yeni kod talep ediniz.");
        }

        var verifyResult = _hasher.VerifyHashedPassword(user, user.PasswordResetCodeHash, code);
        if (verifyResult == PasswordVerificationResult.Failed)
        {
            user.PasswordResetCodeAttempts++;
            var remaining = _settings.MaxCodeAttempts - user.PasswordResetCodeAttempts;

            if (remaining <= 0)
            {
                await ClearResetCodeAsync(user);
                return new PasswordResetResult(false,
                    $"Doğrulama kodu {_settings.MaxCodeAttempts} kez yanlış girildiği için iptal edildi. Lütfen yeni kod talep ediniz.");
            }

            await UpdateResetFieldsAsync(user);
            return new PasswordResetResult(false,
                $"Geçersiz doğrulama kodu. Kalan deneme hakkı: {remaining}.", remaining);
        }

        return new PasswordResetResult(true, "Doğrulama kodu onaylandı.");
    }

    public async Task ClearResetCodeAsync(User user)
    {
        user.PasswordResetCodeHash = null;
        user.PasswordResetCodeExpiresAt = null;
        user.PasswordResetCodeAttempts = 0;

        var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
        var update = Builders<User>.Update
            .Set(u => u.PasswordResetCodeHash, (string?)null)
            .Set(u => u.PasswordResetCodeExpiresAt, (DateTime?)null)
            .Set(u => u.PasswordResetCodeAttempts, 0);

        await _context.Users.UpdateOneAsync(filter, update);
    }

    public bool IsCodeExpired(User user) =>
        user.PasswordResetCodeExpiresAt == null ||
        user.PasswordResetCodeExpiresAt < DateTime.UtcNow;

    public bool IsCodeCancelled(User user) =>
        user.PasswordResetCodeAttempts >= _settings.MaxCodeAttempts;

    private async Task UpdateResetFieldsAsync(User user)
    {
        var filter = Builders<User>.Filter.Eq(u => u.Id, user.Id);
        var update = Builders<User>.Update
            .Set(u => u.PasswordResetCodeHash, user.PasswordResetCodeHash)
            .Set(u => u.PasswordResetCodeExpiresAt, user.PasswordResetCodeExpiresAt)
            .Set(u => u.PasswordResetCodeAttempts, user.PasswordResetCodeAttempts);

        await _context.Users.UpdateOneAsync(filter, update);
    }

    private static string GenerateNumericCode(int length)
    {
        var random = Random.Shared;
        var code = new char[length];
        for (var i = 0; i < length; i++)
            code[i] = (char)('0' + random.Next(0, 10));
        return new string(code);
    }
}
