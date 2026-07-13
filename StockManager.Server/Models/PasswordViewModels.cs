using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

public class ForgotPasswordViewModel
{
    [Required(ErrorMessage = "E-posta adresi gereklidir.")]
    [EmailAddress(ErrorMessage = "Geçerli bir e-posta adresi giriniz.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;
}

public class VerifyResetCodeViewModel
{
    [Required(ErrorMessage = "E-posta adresi gereklidir.")]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Doğrulama kodu gereklidir.")]
    [StringLength(6, MinimumLength = 6, ErrorMessage = "Doğrulama kodu 6 haneli olmalıdır.")]
    [Display(Name = "Doğrulama Kodu")]
    public string Code { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifre gereklidir.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni Şifre")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre tekrarı gereklidir.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Şifreler eşleşmiyor.")]
    [Display(Name = "Yeni Şifre (Tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
