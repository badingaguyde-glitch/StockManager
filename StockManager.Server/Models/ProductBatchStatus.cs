using System.ComponentModel.DataAnnotations;

namespace StockManager.Server.Models;

public enum ProductBatchStatus
{
    [Display(Name = "Aktif (Stokta)")]
    Active = 0,

    [Display(Name = "Tükendi / Satıldı")]
    Sold = 1,

    [Display(Name = "Süresi Doldu (SKT Geçti)")]
    Expired = 2,

    [Display(Name = "Hasarlı / Fire")]
    Damaged = 3,

    [Display(Name = "Tedarikçiye İade")]
    Returned = 4
}
