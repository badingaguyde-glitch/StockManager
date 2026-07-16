using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using StockManager.Server.Models;

namespace StockManager.Server.Services;

public class TransferPdfService
{
    public byte[] GenerateTransferPdf(StockTransfer transfer, Warehouse? sourceWarehouse = null, Warehouse? targetWarehouse = null)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(30);
                page.Size(PageSizes.A4);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(11).FontColor(Colors.Black));

                // Header
                page.Header().Column(headerCol =>
                {
                    headerCol.Item().Row(row =>
                    {
                        row.RelativeColumn().Column(col =>
                        {
                            col.Item().Text("STOCK MANAGER PRO").FontSize(18).SemiBold().FontColor("#6366f1");
                            col.Item().Text("DEPOLAR ARASI STOK TRANSFER VE SEVK BELGESİ").FontSize(13).SemiBold();
                            col.Item().Text("Lojistik ve Tedarik Zinciri Yönetimi").FontSize(9).FontColor(Colors.Grey.Darken2);
                        });
                        row.ConstantColumn(160).AlignRight().Column(col =>
                        {
                            col.Item().Text($"Transfer No: {transfer.TransferNumber}").SemiBold().FontSize(11);
                            col.Item().Text($"Tarih: {transfer.TransferDate.ToLocalTime():dd.MM.yyyy HH:mm}").FontSize(9);
                            col.Item().Text($"Durum: {transfer.Status}").FontSize(9).FontColor("#10b981").SemiBold();
                        });
                    });
                    headerCol.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor("#6366f1");
                });

                // Content
                page.Content().PaddingVertical(15).Column(column =>
                {
                    column.Spacing(12);

                    // Depo Bilgileri (Çıkış ve Giriş)
                    column.Item().Row(row =>
                    {
                        row.RelativeColumn().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(srcCol =>
                        {
                            srcCol.Item().Text("ÇIKIŞ DEPOSU (GÖNDEREN)").FontSize(10).SemiBold().FontColor("#ef4444");
                            srcCol.Item().Text($"{transfer.SourceWarehouseName} {(sourceWarehouse != null ? $"[{sourceWarehouse.Code}]" : "")}").SemiBold().FontSize(12);
                            if (sourceWarehouse != null && !string.IsNullOrEmpty(sourceWarehouse.Address))
                                srcCol.Item().Text($"Adres: {sourceWarehouse.Address}").FontSize(9);
                            if (sourceWarehouse != null && !string.IsNullOrEmpty(sourceWarehouse.Phone))
                                srcCol.Item().Text($"Tel: {sourceWarehouse.Phone}").FontSize(9);
                        });

                        row.ConstantColumn(15);

                        row.RelativeColumn().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(10).Column(tgtCol =>
                        {
                            tgtCol.Item().Text("GİRİŞ DEPOSU (ALICI)").FontSize(10).SemiBold().FontColor("#10b981");
                            tgtCol.Item().Text($"{transfer.TargetWarehouseName} {(targetWarehouse != null ? $"[{targetWarehouse.Code}]" : "")}").SemiBold().FontSize(12);
                            if (targetWarehouse != null && !string.IsNullOrEmpty(targetWarehouse.Address))
                                tgtCol.Item().Text($"Adres: {targetWarehouse.Address}").FontSize(9);
                            if (targetWarehouse != null && !string.IsNullOrEmpty(targetWarehouse.Phone))
                                tgtCol.Item().Text($"Tel: {targetWarehouse.Phone}").FontSize(9);
                        });
                    });

                    if (!string.IsNullOrWhiteSpace(transfer.Notes))
                    {
                        column.Item().Background("#f8fafc").Padding(8).BorderLeft(3).BorderColor("#6366f1").Text($"Açıklama / Notlar: {transfer.Notes}").FontSize(10).Italic();
                    }

                    column.Item().PaddingTop(5).Text("TRANSFER EDİLEN ÜRÜN KALEMLERİ").FontSize(11).SemiBold();

                    // Ürün Tablosu
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(35);  // Sıra No
                            columns.ConstantColumn(110); // Barkod
                            columns.RelativeColumn();    // Ürün Adı
                            columns.ConstantColumn(70);  // Miktar
                            columns.ConstantColumn(120); // Not
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderStyle).Text("#");
                            header.Cell().Element(HeaderStyle).Text("Barkod");
                            header.Cell().Element(HeaderStyle).Text("Ürün Adı");
                            header.Cell().Element(HeaderStyle).AlignRight().Text("Miktar");
                            header.Cell().Element(HeaderStyle).Text("Kalem Notu");
                        });

                        int index = 1;
                        int totalQty = 0;
                        foreach (var item in transfer.Items)
                        {
                            totalQty += item.Quantity;
                            table.Cell().Element(CellStyle).Text(index.ToString());
                            table.Cell().Element(CellStyle).Text(item.Barcode ?? "-");
                            table.Cell().Element(CellStyle).Text(item.ProductName);
                            table.Cell().Element(CellStyle).AlignRight().Text($"{item.Quantity} Adet").SemiBold();
                            table.Cell().Element(CellStyle).Text(item.Notes ?? "-").FontSize(9);
                            index++;
                        }

                        // Toplam Satırı
                        table.Cell().ColumnSpan(3).Element(TotalStyle).AlignRight().Text("TOPLAM TRANSFER MİKTARI:");
                        table.Cell().Element(TotalStyle).AlignRight().Text($"{totalQty} Adet").SemiBold().FontColor("#6366f1");
                        table.Cell().Element(TotalStyle).Text("");
                    });

                    // İmza Alanları
                    column.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeColumn().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(sigCol =>
                        {
                            sigCol.Item().Text("TESLİM EDEN (ÇIKIŞ)").FontSize(10).SemiBold();
                            sigCol.Item().PaddingTop(5).Text("Ad Soyad / İmza:").FontSize(9);
                            sigCol.Item().PaddingTop(25).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                            sigCol.Item().Text(transfer.CreatedBy ?? "Depo Sorumlusu").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });

                        row.ConstantColumn(20);

                        row.RelativeColumn().Border(1).BorderColor(Colors.Grey.Lighten2).Padding(12).Column(sigCol =>
                        {
                            sigCol.Item().Text("TESLİM ALAN (GİRİŞ)").FontSize(10).SemiBold();
                            sigCol.Item().PaddingTop(5).Text("Ad Soyad / İmza:").FontSize(9);
                            sigCol.Item().PaddingTop(25).LineHorizontal(0.5f).LineColor(Colors.Grey.Lighten1);
                            sigCol.Item().Text("Yetkili / Mağaza Müdürü").FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                    });
                });

                // Footer
                page.Footer().AlignCenter().Text(x =>
                {
                    x.Span("StockManager PRO | Sevk ve Transfer İrsaliyesi | Sayfa ");
                    x.CurrentPageNumber();
                    x.Span(" / ");
                    x.TotalPages();
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    private static IContainer HeaderStyle(IContainer container)
    {
        return container.Background("#f1f5f9").BorderBottom(1).BorderColor(Colors.Grey.Lighten1).PaddingVertical(6).PaddingHorizontal(6).DefaultTextStyle(x => x.SemiBold().FontSize(10));
    }

    private static IContainer CellStyle(IContainer container)
    {
        return container.BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).PaddingVertical(6).PaddingHorizontal(6);
    }

    private static IContainer TotalStyle(IContainer container)
    {
        return container.Background("#f8fafc").BorderTop(1).BorderColor(Colors.Grey.Darken1).PaddingVertical(8).PaddingHorizontal(6);
    }
}
