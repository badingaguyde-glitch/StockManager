using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

public enum PurchaseOrderStatus
{
    [Display(Name = "Taslak")]
    Draft = 0,

    [Display(Name = "Tedarikçiye İletildi (E-Posta Gönderildi)")]
    Sent = 1,

    [Display(Name = "Tedarikçi Onayladı")]
    Approved = 2,

    [Display(Name = "Teslim Alındı / Stok Girişi Yapıldı")]
    Completed = 3,

    [Display(Name = "İptal Edildi")]
    Cancelled = 4
}
