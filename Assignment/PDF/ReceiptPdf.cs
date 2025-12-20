namespace Assignment.PDF;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

public class ReceiptPdf : IDocument
{
    private readonly Payment _payment;

    public ReceiptPdf(Payment payment)
    {
        _payment = payment;
    }

    public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(x => x.FontSize(12));

            page.Header().AlignCenter().Text("XYZ Car Rental").FontSize(24).Bold();
            page.Content().Column(col =>
            {
                // Receipt title
                col.Item().PaddingVertical(10)
                    .AlignCenter()
                    .Text("PAYMENT RECEIPT")
                    .FontSize(18)
                    .Bold()
                    .Underline();

                // Customer info
                col.Item().PaddingVertical(5).Row(row =>
                {
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Customer: {_payment.Rental.Customer.Name}");
                        c.Item().Text($"Email: {_payment.Rental.Customer.Email}");
                    });
                    row.RelativeItem().Column(c =>
                    {
                        c.Item().Text($"Payment ID: {_payment.PaymentId}");
                        c.Item().Text($"Rental ID: {_payment.RentalId}");
                        c.Item().Text($"Date: {_payment.Date:dd MMM yyyy HH:mm}");
                    });
                });

                // Divider
                col.Item().PaddingVertical(5).LineHorizontal(1);

                // Payment details table
                col.Item().PaddingVertical(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2);
                    });

                    // Header row
                    table.Header(header =>
                    {
                        header.Cell().Text("Description").Bold();
                        header.Cell().Text("Amount (RM)").Bold();
                    });

                    // Payment row
                    table.Cell().Text(_payment.PaymentType);
                    table.Cell().Text($"{_payment.Amount:F2}");
                });

                // Divider
                col.Item().PaddingVertical(5).LineHorizontal(1);

                // Total
                col.Item().PaddingVertical(5).Row(row =>
                {
                    row.RelativeItem();
                    row.ConstantItem(200).AlignRight().Text($"Total: RM {_payment.Amount:F2}").Bold();
                });

                // Footer / Note
                col.Item().PaddingTop(20)
                   .AlignCenter()
                   .Text("Thank you for choosing XYZ Car Rental!")
                   .Italic();
            });

            // Optional page footer
            page.Footer().AlignCenter().Text("www.xyzcarrental.com | Contact: +6012-3456789").FontSize(10);
        });
    }
}
