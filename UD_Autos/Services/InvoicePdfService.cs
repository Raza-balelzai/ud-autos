using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using UD_Autos.Models.Sales;

namespace UD_Autos.Services;

public interface IInvoicePdfService
{
    byte[] Generate(Invoice invoice);
}

public class InvoicePdfService : IInvoicePdfService
{
    static InvoicePdfService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Generate(Invoice invoice)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        var customerName = invoice.Customer?.ShopName ?? invoice.Customer?.Name ?? "Customer";
        var items = invoice.Items?.OrderBy(i => i.Id).ToList() ?? new List<InvoiceItem>();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontSize(10).FontColor(Colors.Grey.Darken3));

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(left =>
                        {
                            left.Item().Text("Auto Parts Business Solution").SemiBold().FontSize(16).FontColor(Colors.Blue.Darken2);
                            left.Item().Text("Invoice").FontSize(11).FontColor(Colors.Grey.Darken1);
                        });
                        row.ConstantItem(160).AlignRight().Column(right =>
                        {
                            right.Item().Text(invoice.InvoiceNumber).SemiBold().FontSize(12);
                            right.Item().Text(invoice.InvoiceDate.ToString("dd MMM yyyy"));
                            if (invoice.IsVoided)
                                right.Item().Text("VOIDED").FontColor(Colors.Red.Medium).SemiBold();
                        });
                    });
                    col.Item().PaddingTop(8).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(16).Column(col =>
                {
                    col.Spacing(12);

                    col.Item().Column(c =>
                    {
                        c.Item().Text(customerName).SemiBold().FontSize(12);
                        if (!string.IsNullOrWhiteSpace(invoice.Customer?.Phone))
                            c.Item().Text(invoice.Customer!.Phone!);
                        if (!string.IsNullOrWhiteSpace(invoice.Customer?.Address))
                            c.Item().Text(invoice.Customer!.Address!);
                        if (!string.IsNullOrWhiteSpace(invoice.Customer?.City))
                            c.Item().Text(invoice.Customer!.City!);
                    });

                    col.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(28);
                            columns.RelativeColumn(3);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1.2f);
                            columns.RelativeColumn(1.2f);
                        });

                        table.Header(header =>
                        {
                            header.Cell().Element(HeaderCell).Text("#");
                            header.Cell().Element(HeaderCell).Text("Product");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Qty");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Rate");
                            header.Cell().Element(HeaderCell).AlignRight().Text("Amount");
                        });

                        var n = 1;
                        foreach (var item in items)
                        {
                            var name = item.Product?.Name ?? $"Product #{item.ProductId}";
                            table.Cell().Element(BodyCell).Text(n++.ToString());
                            table.Cell().Element(BodyCell).Text(name);
                            table.Cell().Element(BodyCell).AlignRight().Text(item.Quantity.ToString("N2"));
                            table.Cell().Element(BodyCell).AlignRight().Text(item.UnitPrice.ToString("N2"));
                            table.Cell().Element(BodyCell).AlignRight().Text(item.LineTotal.ToString("N2"));
                        }
                    });

                    col.Item().AlignRight().Width(260).Column(totals =>
                    {
                        totals.Spacing(4);
                        TotalsRow(totals, "Subtotal", invoice.Subtotal);
                        TotalsRow(totals, "Discount", invoice.DiscountAmount);
                        TotalsRow(totals, "Delivery / Other", invoice.DeliveryCharge + invoice.OtherCharge);
                        TotalsRow(totals, "Bill Total", invoice.BillTotal, strong: true);
                        TotalsRow(totals, "Previous Balance", invoice.PreviousBalance);
                        TotalsRow(totals, "Payment Received", invoice.PaymentReceived);
                        TotalsRow(totals, "New Balance", invoice.NewBalance, strong: true);
                    });
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("Generated by UD Autos · Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void TotalsRow(ColumnDescriptor column, string label, decimal amount, bool strong = false)
    {
        column.Item().Row(row =>
        {
            var labelCell = row.RelativeItem().Text(label);
            var valueCell = row.ConstantItem(100).AlignRight().Text($"Rs. {amount:N2}");
            if (strong)
            {
                labelCell.SemiBold();
                valueCell.SemiBold();
            }
        });
    }

    private static IContainer HeaderCell(IContainer container)
        => container.DefaultTextStyle(x => x.SemiBold().FontSize(9))
            .PaddingVertical(6)
            .BorderBottom(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten4)
            .PaddingHorizontal(4);

    private static IContainer BodyCell(IContainer container)
        => container.PaddingVertical(5).PaddingHorizontal(4).BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten3);
}
