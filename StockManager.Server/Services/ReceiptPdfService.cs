using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

public class ReceiptPdfService
{
    public byte[] GenerateReceiptPdf(Sale sale, Customer? customer = null)
    {
        var currency = sale.Currency?.ToUpperInvariant() ?? "TRY";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(12).FontColor(Colors.Black));

                page.Header().Text("Satış Fişi").FontSize(20).SemiBold().FontColor(Colors.Black);

                page.Content().Column(column =>
                {
                    column.Spacing(6);

                    column.Item().Text($"Fiş No: {sale.InvoiceNumber}");
                    column.Item().Text($"Tarih: {sale.SaleDate:yyyy-MM-dd HH:mm}");

                    if (customer != null)
                    {
                        column.Item().Text($"Müşteri: {customer.FullName}");
                    }

                    if (!string.IsNullOrWhiteSpace(sale.StripePaymentIntentId))
                    {
                        column.Item().Text($"Stripe İşlem ID: {sale.StripePaymentIntentId}");
                        column.Item().Text($"Ödeme Durumu: {sale.StripeStatus}");
                    }

                    column.Item().PaddingVertical(10).LineHorizontal(1);

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(4);
                            columns.ConstantColumn(60);
                            columns.ConstantColumn(80);
                            columns.ConstantColumn(80);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Ürün");
                            header.Cell().Element(CellStyle).AlignRight().Text("Adet");
                            header.Cell().Element(CellStyle).AlignRight().Text("Birim");
                            header.Cell().Element(CellStyle).AlignRight().Text("Tutar");
                        });

                        foreach (var item in sale.Items)
                        {
                            table.Cell().Element(CellStyle).Text(item.ProductName);
                            table.Cell().Element(CellStyle).AlignRight().Text(item.Quantity.ToString());
                            table.Cell().Element(CellStyle).AlignRight().Text(item.UnitPrice.ToString("0.00"));
                            table.Cell().Element(CellStyle).AlignRight().Text(item.TotalLinePrice.ToString("0.00"));
                        }

                        table.Cell().ColumnSpan(3).Element(CellStyle).AlignRight().Text("Toplam");
                        table.Cell().Element(CellStyle).AlignRight().Text($"{sale.TotalAmount:0.00} {currency}");
                    });

                    column.Item().PaddingTop(20).AlignRight().Text("Teşekkür ederiz!").FontSize(12).SemiBold();
                });

                page.Footer().AlignCenter().Text("StockManager | Stripe ile ödeme ve fiş desteği").FontSize(10);
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
