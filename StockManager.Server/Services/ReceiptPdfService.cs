using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

public class ReceiptPdfService
{
    public byte[] GenerateReceiptPdf(Sale sale, Customer? customer = null)
    {
        QuestPDF.Settings.License = LicenseType.Community;
        var currency = sale.Currency?.ToUpperInvariant() ?? "TRY";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12).FontColor(Colors.Black));

                page.Header().Text("STOCK MANAGER - E-ARŞİV FATURA / FİŞ").FontSize(16).SemiBold().FontColor(Colors.Black);

                page.Content().Column(column =>
                {
                    column.Spacing(6);

                    column.Item().Text("STOCK MANAGER TEKNOLOJİ VE TİCARET A.Ş.").SemiBold();
                    column.Item().Text("Maslak VD - 1234567890 | Bilişim Plaza No: 42, İstanbul").FontSize(10);
                    column.Item().PaddingVertical(4).LineHorizontal(1);

                    column.Item().Text($"Fatura / Fiş No: {sale.InvoiceNumber}");
                    column.Item().Text($"Tarih: {sale.SaleDate.ToLocalTime():dd.MM.yyyy HH:mm}");
                    column.Item().Text($"Ödeme Tipi: {(sale.PaymentType == PaymentType.Cash ? "Nakit" : sale.PaymentType == PaymentType.Card ? "Kart" : "Veresiye (Cari Hesap)")}");

                    if (customer != null)
                    {
                        column.Item().PaddingTop(6).Text($"Sayın / Şirket: {customer.FullName} {(string.IsNullOrEmpty(customer.CompanyName) ? "" : $"({customer.CompanyName})")}").SemiBold();
                        if (!string.IsNullOrEmpty(customer.TaxOffice))
                            column.Item().Text($"Vergi Dairesi / No: {customer.TaxOffice}").FontSize(10);
                        if (!string.IsNullOrEmpty(customer.Address))
                            column.Item().Text($"Adres: {customer.Address}").FontSize(10);
                    }
                    else
                    {
                        column.Item().PaddingTop(6).Text("Müşteri: Muhtelif / Perakende (Anonim Satış)");
                    }

                    if (!string.IsNullOrWhiteSpace(sale.StripePaymentIntentId))
                    {
                        column.Item().Text($"Stripe İşlem ID: {sale.StripePaymentIntentId}").FontSize(9);
                        column.Item().Text($"Ödeme Durumu: {sale.StripeStatus}").FontSize(9);
                    }

                    column.Item().PaddingVertical(8).LineHorizontal(1);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.ConstantColumn(50);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Ürün / Hizmet");
                            header.Cell().Element(CellStyle).AlignRight().Text("Adet");
                            header.Cell().Element(CellStyle).AlignRight().Text("Birim");
                            header.Cell().Element(CellStyle).AlignRight().Text("Tutar");
                        });

                        foreach (var item in sale.Items)
                        {
                            table.Cell().Element(CellStyle).Text(item.ProductName);
                            table.Cell().Element(CellStyle).AlignRight().Text(item.Quantity.ToString());
                            table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.ToString("N2"));
                            table.Cell().Element(CellStyle).AlignRight().Text(item.TotalLinePrice.ToString("N2"));
                        }

                        var kdvHaric = sale.TotalAmount / 1.20m;
                        var kdvTutari = sale.TotalAmount - kdvHaric;

                        table.Cell().ColumnSpan(3).Element(CellStyle).AlignRight().Text("Ara Toplam (KDV Hariç):");
                        table.Cell().Element(CellStyle).AlignRight().Text($"{kdvHaric:N2} {currency}");

                        table.Cell().ColumnSpan(3).Element(CellStyle).AlignRight().Text("KDV (%20):");
                        table.Cell().Element(CellStyle).AlignRight().Text($"{kdvTutari:N2} {currency}");

                        table.Cell().ColumnSpan(3).Element(CellStyle).AlignRight().Text("Genel Toplam:");
                        table.Cell().Element(CellStyle).AlignRight().Text($"{sale.TotalAmount:N2} {currency}");
                    });

                    column.Item().PaddingTop(15).AlignRight().Text("Bizi tercih ettiğiniz için teşekkür ederiz!").FontSize(11).SemiBold();
                });

                page.Footer().AlignCenter().Text("StockManager | 213 Sayılı VUK Hükümlerine Göre Düzenlenmiştir").FontSize(9);
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.DefaultTextStyle(x => x.SemiBold()).PaddingVertical(6).PaddingHorizontal(3);
    }
}
