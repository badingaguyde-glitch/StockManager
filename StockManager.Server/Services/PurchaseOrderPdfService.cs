using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

public class PurchaseOrderPdfService
{
    public byte[] GeneratePurchaseOrderPdf(PurchaseOrder po, Supplier? supplier = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Black));

                // Header
                page.Header().Column(headerCol =>
                {
                    headerCol.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("STOCK MANAGER PRO").FontSize(18).SemiBold().FontColor("#2563eb");
                            col.Item().Text("SATIN ALMA SİPARİŞ BELGESİ (PURCHASE ORDER)").FontSize(13).SemiBold();
                            col.Item().Text("Tedarik ve Satın Alma Departmanı").FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantItem(180).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Sipariş No: {po.OrderNumber}").SemiBold().FontSize(11).FontColor("#1e40af");
                            col.Item().Text($"Tarih: {po.OrderDate.ToLocalTime():dd.MM.yyyy HH:mm}").FontSize(9);
                            col.Item().Text($"Teslimat: {(po.ExpectedDeliveryDate.HasValue ? po.ExpectedDeliveryDate.Value.ToLocalTime().ToString("dd.MM.yyyy") : "Asap / Acil")}").FontSize(9);
                            col.Item().Text($"Durum: {po.Status}").FontSize(9).FontColor("#10b981").SemiBold();
                        });
                    });
                    headerCol.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor("#2563eb");
                });

                // Content
                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(12);

                    // Tedarikçi ve Alıcı Bilgileri
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                        {
                            col.Item().Text("TEDARİKÇİ FİRMA BİLGİLERİ").FontSize(10).SemiBold().FontColor("#2563eb");
                            col.Item().PaddingTop(4).Text($"Firma: {po.SupplierName}").SemiBold();
                            col.Item().Text($"E-Posta: {po.SupplierEmail ?? (supplier?.Email ?? "-")}").FontSize(9);
                            col.Item().Text($"Yetkili: {supplier?.ContactName ?? "-"}").FontSize(9);
                            col.Item().Text($"Telefon: {supplier?.Phone ?? "-"}").FontSize(9);
                        });

                        row.ConstantItem(15);

                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(col =>
                        {
                            col.Item().Text("ALICI & TESLİMAT BİLGİLERİ").FontSize(10).SemiBold().FontColor("#2563eb");
                            col.Item().PaddingTop(4).Text("Firma: StockManager Pro / Kurumsal").SemiBold();
                            col.Item().Text($"Teslim Deposu: {po.TargetWarehouseName ?? "Ana Depo"}").FontSize(9);
                            col.Item().Text($"Oluşturan: {po.CreatedBy ?? "Sistem Yetkilisi"}").FontSize(9);
                            col.Item().Text("Ödeme Şartları: Banka Havalesi / EFT / Kredi").FontSize(9);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(po.Notes))
                    {
                        column.Item().Background("#eff6ff").Padding(8).BorderLeft(3).BorderColor("#2563eb").Text($"Sipariş Notu / Özel Talimatlar: {po.Notes}").FontSize(9).Italic();
                    }

                    column.Item().PaddingTop(5).Text("SİPARİŞ EDİLEN ÜRÜN KALEMLERİ").FontSize(11).SemiBold();

                    // Ürün Tablosu
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(30);  // Sıra
                            columns.ConstantColumn(95);  // Barkod
                            columns.RelativeColumn();    // Ürün Adı
                            columns.ConstantColumn(65);  // Mevcut/Kritik
                            columns.ConstantColumn(60);  // Sipariş Adet
                            columns.ConstantColumn(75);  // Birim Fiyat
                            columns.ConstantColumn(85);  // Toplam Tutar
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderStyle).Text("#");
                            header.Cell().Element(HeaderStyle).Text("Barkod");
                            header.Cell().Element(HeaderStyle).Text("Ürün Adı");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Mevcut/Eşik");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Sipariş");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Birim Fiyat");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Toplam");
                        });

                        int index = 1;
                        int totalItems = 0;
                        foreach (var item in po.Items)
                        {
                            totalItems += item.OrderedQuantity;
                            table.Cell().Element(CellStyle).Text(index.ToString());
                            table.Cell().Element(CellStyle).Text(item.Barcode ?? "-");
                            table.Cell().Element(CellStyle).Text(item.ProductName);
                            table.Cell().Element(CellStyle).AlignRight().Text($"{item.CurrentStock} / {item.LowStockThreshold}").FontSize(8).FontColor(Colors.Red.Medium);
                            table.Cell().Element(CellStyle).AlignRight().Text($"{item.OrderedQuantity} Adet").SemiBold();
                            table.Cell().Element(CellStyle).AlignRight().Text($"{item.UnitPrice:N2} ₺");
                            table.Cell().Element(CellStyle).AlignRight().Text($"{item.TotalPrice:N2} ₺").SemiBold();
                            index++;
                        }

                        // Toplam Satırı
                        table.Cell().ColumnSpan(4).Element(TotalStyle).AlignRight().Text("GENEL TOPLAM:");
                        table.Cell().Element(TotalStyle).AlignRight().Text($"{totalItems} Adet").SemiBold();
                        table.Cell().Element(TotalStyle).Text("");
                        table.Cell().Element(TotalStyle).AlignRight().Text($"{po.TotalAmount:N2} ₺").SemiBold().FontColor("#2563eb").FontSize(11);
                    });

                    // Koşullar ve İmza
                    column.Item().PaddingTop(20).Row(row =>
                    {
                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(sigCol =>
                        {
                            sigCol.Item().Text("SATIN ALMA / ONAYLAYAN").FontSize(10).SemiBold();
                            sigCol.Item().PaddingTop(4).Text("Ad Soyad / İmza:").FontSize(9);
                            sigCol.Item().PaddingTop(25).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                            sigCol.Item().Text(po.CreatedBy ?? "Satın Alma Müdürü").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });

                        row.ConstantItem(20);

                        row.RelativeItem().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(sigCol =>
                        {
                            sigCol.Item().Text("TEDARİKÇİ KABUL / ONAY").FontSize(10).SemiBold();
                            sigCol.Item().PaddingTop(4).Text("Kaşe / İmza / Tarih:").FontSize(9);
                            sigCol.Item().PaddingTop(25).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                            sigCol.Item().Text(po.SupplierName).FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                    });

                    column.Item().PaddingTop(15).Text("* Lütfen bu sipariş belgesini onayladıktan sonra irsaliye/fatura ile birlikte sevk ediniz.").FontSize(8).Italic().FontColor(Colors.Grey.Darken1);
                });

                // Footer
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("StockManager Satın Alma Yönetim Sistemi - Sayfa ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }

    static IContainer HeaderStyle(IContainer container)
    {
        return container
            .Background("#f1f5f9")
            .BorderBottom(1)
            .BorderColor("#cbd5e1")
            .Padding(6)
            .DefaultTextStyle(x => x.SemiBold().FontSize(9).FontColor("#1e293b"));
    }

    static IContainer CellStyle(IContainer container)
    {
        return container
            .BorderBottom(0.5f)
            .BorderColor("#e2e8f0")
            .Padding(6)
            .DefaultTextStyle(x => x.FontSize(9));
    }

    static IContainer TotalStyle(IContainer container)
    {
        return container
            .Background("#f8fafc")
            .BorderTop(1)
            .BorderColor("#64748b")
            .Padding(6)
            .DefaultTextStyle(x => x.SemiBold().FontSize(10));
    }
}
