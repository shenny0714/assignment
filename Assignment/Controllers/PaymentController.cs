using Assignment.Models;
using Assignment.ViewModels;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using Stripe;
using Stripe.Checkout;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;

namespace Assignment.Controllers;


[Authorize(Roles = "Customer")]
public class PaymentController : Controller
{
    private readonly DB _db;
    private readonly IConfiguration _configuration;

    public PaymentController(DB db, IConfiguration configuration)
    {
        _db = db;
        _configuration = configuration;
        StripeConfiguration.ApiKey = _configuration["Stripe:SecretKey"];
    }

    // =========================
    // MAKE PAYMENT (GET)
    // =========================
    [HttpGet]
    public IActionResult MakePayment(
        int modelId,
        DateTime rentalDate,
        DateTime returnDate,
        decimal totalPrice,
        decimal deposit)
    {
        var model = _db.CarModels
            .Include(m => m.Brand)
            .FirstOrDefault(m => m.ModelId == modelId);

        if (model == null)
            return RedirectToAction("Index", "Home");

        var vm = new PaymentVM
        {
            CarModel = $"{model.Brand?.BrandName} {model.ModelName}",
            TotalRentalPrice = totalPrice,
            DepositRequired = deposit,
            Amount = totalPrice + deposit
        };

        ViewBag.ModelId = modelId;
        ViewBag.RentalDate = rentalDate;
        ViewBag.ReturnDate = returnDate;

        return View(vm);
    }

    // =========================
    // MAKE PAYMENT (POST)
    // =========================
    [HttpPost]
    public IActionResult MakePayment(
        int modelId,
        DateTime rentalDate,
        DateTime returnDate,
        decimal totalPrice,
        decimal deposit,
        string paymentMethod)
    {
        var model = _db.CarModels
            .Include(m => m.Brand)
            .FirstOrDefault(m => m.ModelId == modelId);

        if (model == null)
            return RedirectToAction("Index", "Home");

        decimal grandTotal = totalPrice + deposit;
        long amountInCents = (long)(grandTotal * 100);
        string domain = "https://localhost:7102";

        List<string> paymentTypes =
            paymentMethod == "StripeFPX"
                ? new() { "fpx" }
                : new() { "card" };

        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = paymentTypes,
            LineItems = new()
            {
                new SessionLineItemOptions
                {
                    PriceData = new SessionLineItemPriceDataOptions
                    {
                        UnitAmount = amountInCents,
                        Currency = "myr",
                        ProductData = new SessionLineItemPriceDataProductDataOptions
                        {
                            Name = $"Car Rental: {model.Brand?.BrandName} {model.ModelName}",
                            Description = "Rental + Deposit"
                        }
                    },
                    Quantity = 1
                }
            },
            Mode = "payment",
            SuccessUrl = domain + $"/Payment/Success?modelId={modelId}&rentalDate={rentalDate:yyyy-MM-dd}&returnDate={returnDate:yyyy-MM-dd}&amount={grandTotal}&method={paymentMethod}",
            CancelUrl = domain + $"/Payment/MakePayment?modelId={modelId}&rentalDate={rentalDate:yyyy-MM-dd}&returnDate={returnDate:yyyy-MM-dd}&totalPrice={totalPrice}&deposit={deposit}"
        };

        var service = new SessionService();
        Session session = service.Create(options);

        return Redirect(session.Url);
    }

    // =========================
    // SUCCESS
    // =========================
    public IActionResult Success(
        int modelId,
        DateTime rentalDate,
        DateTime returnDate,
        decimal amount,
        string method)
    {
        string userEmail = User.Identity!.Name!;
        var customer = _db.Customers
            .FirstOrDefault(c => c.Email == userEmail || c.Name == userEmail);

        if (customer == null)
            return RedirectToAction("Login", "Account");

        string rentalId = GenerateRentalId();
        string paymentId = GeneratePaymentId();

        decimal rentalFee = Math.Round(amount / 1.2m, 2);
        decimal deposit = amount - rentalFee;

        var rental = new Rental
        {
            RentalId = rentalId,
            CustomerId = customer.CustomerId,
            ModelId = modelId,
            RentalDate = DateTime.Now,
            PickupDate = rentalDate,
            ReturnDate = returnDate,
            TotalPrice = rentalFee,
            DepositAmount = deposit,
            Status = "Booked",
            Customer = customer,
            Payment = new List<Payment>()
        };

        var payment = new Payment
        {
            PaymentId = paymentId,
            RentalId = rentalId,
            Amount = amount,
            PaymentType = "Full Payment",
            PaymentMethod = method == "StripeFPX" ? "Online Banking" : "Credit Card",
            Status = "Successful",
            Date = DateTime.Now
        };

        // 🔥 CRITICAL FIX
        rental.Payment.Add(payment);

        _db.Rentals.Add(rental);
        _db.SaveChanges();

        // EMAIL + PDF
        var model = _db.CarModels
            .Include(m => m.Brand)
            .FirstOrDefault(m => m.ModelId == modelId);

        rental.Model = model;

        string domain = $"{Request.Scheme}://{Request.Host}";
        string pickupUrl = $"{domain}/PickUpReturn/Pickup?RentalId={rentalId}";
        byte[] qr = GenerateQRCode(pickupUrl);
        byte[] pdf = GeneratePdfReceipt(rental, qr);

        SendEmail(customer.Email, rental, qr, pdf);

        TempData["Info"] = "Payment successful. Receipt sent to email.";
        return RedirectToAction("Detail", "Rental", new { id = rentalId });
    }

    // =========================
    // PAYMENT HISTORY
    // =========================
    public IActionResult History()
    {
        var email = User.Identity!.Name!;
        var customer = _db.Customers
            .FirstOrDefault(c => c.Email == email || c.Name == email);

        var payments = _db.Payments
            .Include(p => p.Rental)
                .ThenInclude(r => r.Model)
                    .ThenInclude(m => m.Brand)
            .Where(p => p.Rental.CustomerId == customer!.CustomerId)
            .OrderByDescending(p => p.Date)
            .ToList();

        return View(payments);
    }

    // =========================
    // HELPERS
    // =========================
    private string GenerateRentalId()
    {
        string? max = _db.Rentals.Max(r => r.RentalId);
        return max == null
            ? "RN0001"
            : $"RN{(int.Parse(max[2..]) + 1):D4}";
    }

    private string GeneratePaymentId()
    {
        string? max = _db.Payments.Max(p => p.PaymentId);
        return max == null
            ? "PM0001"
            : $"PM{(int.Parse(max[2..]) + 1):D4}";
    }

    private byte[] GenerateQRCode(string url)
    {
        QRCodeGenerator gen = new();
        var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        return new PngByteQRCode(data).GetGraphic(20);
    }

    private byte[] GeneratePdfReceipt(Assignment.Models.Rental rental, byte[] qrBytes)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            PdfWriter writer = new PdfWriter(stream);
            PdfDocument pdf = new PdfDocument(writer);
            Document document = new Document(pdf);

            PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            PdfFont normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            Color primaryColor = new DeviceRgb(13, 110, 253);
            Color successColor = new DeviceRgb(25, 135, 84);
            Color mutedColor = DeviceGray.GRAY;
            Color lightBg = new DeviceRgb(248, 249, 250);

            // HEADER
            Table headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 })).UseAllAvailableWidth();

            Cell leftHeader = new Cell().SetBorder(Border.NO_BORDER);
            leftHeader.Add(new Paragraph("RECEIPT").SetFont(boldFont).SetFontSize(18));
            leftHeader.Add(new Paragraph($"Ref: #{rental.RentalId}").SetFont(normalFont).SetFontColor(mutedColor).SetFontSize(10));

            Cell rightHeader = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);
            rightHeader.Add(new Paragraph(rental.Status.ToUpper())
                .SetFont(boldFont).SetFontColor(ColorConstants.WHITE)
                .SetBackgroundColor(successColor)
                .SetPadding(3).SetFontSize(10));
            rightHeader.Add(new Paragraph(rental.RentalDate.ToString("dd MMM yyyy, hh:mm tt"))
                .SetFont(normalFont).SetFontColor(mutedColor).SetFontSize(9).SetMarginTop(2));

            headerTable.AddCell(leftHeader);
            headerTable.AddCell(rightHeader);
            document.Add(headerTable);

            document.Add(new Paragraph("\n"));

            // CUSTOMER
            document.Add(new Paragraph("BILLED TO").SetFont(boldFont).SetFontSize(8).SetFontColor(mutedColor));
            document.Add(new Paragraph(rental.Customer?.Name ?? "Valued Customer").SetFont(boldFont).SetFontSize(12));
            document.Add(new Paragraph("Valued Customer").SetFont(normalFont).SetFontSize(10).SetFontColor(mutedColor));

            document.Add(new Paragraph("\n"));

            // CAR SUMMARY BOX
            Table carBox = new Table(1).UseAllAvailableWidth();
            Cell carCell = new Cell();
            carCell.SetBackgroundColor(lightBg);
            carCell.SetBorder(new SolidBorder(DeviceGray.GRAY, 0.5f));
            carCell.SetPadding(15);

            int days = (rental.ReturnDate - rental.PickupDate).Days + 1;
            if (days < 1) days = 1;

            // UPDATED: Added Brand Name here
            string carTitle = $"Car: {rental.Model?.Brand?.BrandName ?? ""} {rental.Model?.ModelName ?? "Unknown"}";

            carCell.Add(new Paragraph(carTitle).SetFont(boldFont).SetFontSize(12).SetMarginBottom(5));
            carCell.Add(new Paragraph("RENTAL PERIOD").SetFont(boldFont).SetFontSize(8).SetFontColor(mutedColor));

            Table dateTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 3 })).UseAllAvailableWidth();

            dateTable.AddCell(new Cell().Add(new Paragraph("Pick-up:").SetFont(boldFont).SetFontSize(10)).SetBorder(Border.NO_BORDER));
            dateTable.AddCell(new Cell().Add(new Paragraph(rental.PickupDate.ToString("dd MMM yyyy")).SetFont(normalFont).SetFontSize(10)).SetBorder(Border.NO_BORDER));

            dateTable.AddCell(new Cell().Add(new Paragraph("Return:").SetFont(boldFont).SetFontSize(10)).SetBorder(Border.NO_BORDER));
            dateTable.AddCell(new Cell().Add(new Paragraph(rental.ReturnDate.ToString("dd MMM yyyy")).SetFont(normalFont).SetFontSize(10)).SetBorder(Border.NO_BORDER));

            dateTable.AddCell(new Cell().Add(new Paragraph("Duration:").SetFont(boldFont).SetFontSize(10).SetFontColor(primaryColor)).SetBorder(Border.NO_BORDER));
            dateTable.AddCell(new Cell().Add(new Paragraph($"{days} Day(s)").SetFont(boldFont).SetFontSize(10).SetFontColor(primaryColor)).SetBorder(Border.NO_BORDER));

            carCell.Add(dateTable);
            carBox.AddCell(carCell);
            document.Add(carBox);

            document.Add(new Paragraph("\n"));

            // PRICE TABLE
            Table priceTable = new Table(UnitValue.CreatePercentArray(new float[] { 3, 1 })).UseAllAvailableWidth();

            priceTable.AddHeaderCell(new Cell().Add(new Paragraph("DESCRIPTION").SetFont(boldFont).SetFontSize(9).SetFontColor(mutedColor)).SetBorder(Border.NO_BORDER));
            priceTable.AddHeaderCell(new Cell().Add(new Paragraph("AMOUNT (RM)").SetFont(boldFont).SetFontSize(9).SetFontColor(mutedColor)).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));

            priceTable.AddCell(new Cell(1, 2).SetBorderBottom(new SolidBorder(DeviceGray.GRAY, 0.5f)).SetBorderLeft(Border.NO_BORDER).SetBorderRight(Border.NO_BORDER).SetBorderTop(Border.NO_BORDER));

            priceTable.AddCell(new Cell().Add(new Paragraph($"Rental Charges ({days} Days)").SetFont(normalFont).SetFontSize(10)).SetBorder(Border.NO_BORDER).SetPaddingTop(10));
            priceTable.AddCell(new Cell().Add(new Paragraph(rental.TotalPrice.ToString("N2")).SetFont(boldFont).SetFontSize(10)).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER).SetPaddingTop(10));

            priceTable.AddCell(new Cell().Add(new Paragraph("Security Deposit (Refundable)").SetFont(normalFont).SetFontSize(10).SetFontColor(mutedColor)).SetBorder(Border.NO_BORDER));
            priceTable.AddCell(new Cell().Add(new Paragraph(rental.DepositAmount.ToString("N2")).SetFont(normalFont).SetFontSize(10).SetFontColor(mutedColor)).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));

            decimal totalPaid = rental.TotalPrice + rental.DepositAmount;

            Cell lineCell = new Cell(1, 2).SetBorder(Border.NO_BORDER);
            lineCell.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(1f)).SetMarginTop(10));
            priceTable.AddCell(lineCell);

            priceTable.AddCell(new Cell().Add(new Paragraph("Total Paid").SetFont(boldFont).SetFontSize(12)).SetBorder(Border.NO_BORDER).SetPaddingTop(5));
            priceTable.AddCell(new Cell().Add(new Paragraph($"RM {totalPaid:N2}").SetFont(boldFont).SetFontSize(14).SetFontColor(successColor)).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER).SetPaddingTop(5));

            document.Add(priceTable);
            document.Add(new Paragraph("\n\n"));

            if (qrBytes != null && qrBytes.Length > 0)
            {
                try
                {
                    var qrImage = new iText.Layout.Element.Image(iText.IO.Image.ImageDataFactory.Create(qrBytes));
                    qrImage.SetWidth(100);
                    qrImage.SetHorizontalAlignment(HorizontalAlignment.CENTER);
                    document.Add(qrImage);
                }
                catch { }
            }

            document.Add(new Paragraph("© 2025 Title. All Rights Reserved.")
                .SetFont(normalFont).SetFontSize(8).SetFontColor(mutedColor)
                .SetTextAlignment(TextAlignment.CENTER).SetMarginTop(10));

            document.Close();
            return stream.ToArray();
        }
    }

    private void SendEmail(string userEmail, Assignment.Models.Rental rental, byte[] qrCodeBytes, byte[] pdfBytes)
    {
        string host = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
        int port = int.Parse(_configuration["Smtp:Port"] ?? "587");
        string senderEmail = _configuration["Smtp:User"] ?? "waixianho@gmail.com";
        string senderPass = _configuration["Smtp:Pass"] ?? "hmor krvp syey vewp";
        string senderName = _configuration["Smtp:Name"] ?? "Car Rental Admin";

        int days = (rental.ReturnDate - rental.PickupDate).Days + 1;
        if (days < 1) days = 1;
        decimal totalPaid = rental.TotalPrice + rental.DepositAmount;

        // UPDATED: Combined String for Email
        string carFullTitle = $"{rental.Model?.Brand?.BrandName ?? ""} {rental.Model?.ModelName ?? "Unknown"}";
        string customerName = rental.Customer?.Name ?? "Valued Customer";

        string body = $@"
    <!DOCTYPE html>
    <html>
    <head>
        <style>
            body {{ font-family: Helvetica, Arial, sans-serif; background-color: #f8f9fa; margin: 0; padding: 20px; }}
            .container {{ max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 8px; overflow: hidden; box-shadow: 0 4px 10px rgba(0,0,0,0.1); border: 1px solid #e0e0e0; }}
            .header {{ background-color: #ffffff; padding: 30px; border-bottom: 2px solid #f0f0f0; }}
            .content {{ padding: 30px; }}
            .car-box {{ background-color: #f8f9fa; border: 1px solid #e9ecef; border-radius: 6px; padding: 20px; margin-bottom: 20px; }}
            .table-row td {{ padding: 12px 0; border-bottom: 1px solid #eee; color: #555; }}
            .total-row td {{ padding-top: 15px; font-size: 18px; font-weight: bold; color: #198754; border-bottom: none; }}
            .footer {{ text-align: center; color: #888; font-size: 12px; padding: 20px; background-color: #f8f9fa; }}
        </style>
    </head>
    <body>
        <div class='container'>
            <table width='100%' class='header'>
                <tr>
                    <td align='left' valign='top'>
                        <h2 style='margin:0; color:#212529; font-size: 24px;'>RECEIPT</h2>
                        <div style='color:#6c757d; font-size:14px; margin-top:5px;'>Ref: #{rental.RentalId}</div>
                    </td>
                    <td align='right' valign='top'>
                        <span style='background-color:#198754; color:white; padding:5px 10px; border-radius:4px; font-size:12px; font-weight:bold;'>
                            {rental.Status.ToUpper()}
                        </span>
                        <div style='color:#6c757d; font-size:12px; margin-top:5px;'>
                            {rental.RentalDate:dd MMM yyyy}
                        </div>
                    </td>
                </tr>
            </table>

            <div class='content'>
                <div style='margin-bottom: 20px;'>
                    <div style='color:#6c757d; font-size:10px; font-weight:bold; text-transform:uppercase;'>BILLED TO</div>
                    <div style='font-size:16px; font-weight:bold; color:#212529;'>{customerName}</div>
                    <div style='color:#6c757d; font-size:14px;'>Valued Customer</div>
                </div>

                <div class='car-box'>
                    <table width='100%'>
                        <tr>
                            <td width='50' valign='top'>
                                <div style='font-size:30px;'>🚗</div>
                            </td>
                            <td>
                                <div style='font-size:16px; font-weight:bold; color:#0d6efd; margin-bottom:5px;'>
                                     {carFullTitle}
                                </div>
                                <div style='font-size:13px; color:#555;'>
                                    <strong>Pick-up:</strong> {rental.PickupDate:dd MMM yyyy}<br>
                                    <strong>Return:</strong> {rental.ReturnDate:dd MMM yyyy}<br>
                                    <strong>Duration:</strong> <span style='color:#0d6efd; font-weight:bold;'>{days} Day(s)</span>
                                </div>
                            </td>
                        </tr>
                    </table>
                </div>

                <table width='100%' cellspacing='0'>
                    <thead>
                        <tr>
                            <th align='left' style='color:#6c757d; font-size:12px; padding-bottom:10px; border-bottom:2px solid #eee;'>DESCRIPTION</th>
                            <th align='right' style='color:#6c757d; font-size:12px; padding-bottom:10px; border-bottom:2px solid #eee;'>AMOUNT (RM)</th>
                        </tr>
                    </thead>
                    <tbody>
                        <tr class='table-row'>
                            <td>Rental Charges ({days} Days)</td>
                            <td align='right' style='font-weight:bold; color:#000;'>{rental.TotalPrice:N2}</td>
                        </tr>
                        <tr class='table-row'>
                            <td>Security Deposit (Refundable)</td>
                            <td align='right'>{rental.DepositAmount:N2}</td>
                        </tr>
                        <tr class='total-row'>
                            <td>Total Paid</td>
                            <td align='right'>RM {totalPaid:N2}</td>
                        </tr>
                    </tbody>
                </table>
                
                <br><br>

                <div style='text-align:center;'>
                    <img src='cid:QRCodeImage' style='width:150px; height:auto;' alt='QR Code' />
                    <p style='color:#6c757d; font-size:12px; margin-top:10px;'>Show this QR Code at the counter for pickup.</p>
                </div>
            </div>

            <div class='footer'>
                Thank you for choosing us!<br>
                A PDF copy is attached to this email.
            </div>
        </div>
    </body>
    </html>";

        var fromAddress = new MailAddress(senderEmail, senderName);
        var toAddress = new MailAddress(userEmail);

        var smtp = new SmtpClient
        {
            Host = host,
            Port = port,
            EnableSsl = true,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(senderEmail, senderPass)
        };

        using (var message = new MailMessage(fromAddress, toAddress))
        {
            message.Subject = $"Booking Receipt";
            message.IsBodyHtml = true;

            var htmlView = AlternateView.CreateAlternateViewFromString(body, null, MediaTypeNames.Text.Html);

            if (qrCodeBytes != null)
            {
                var qrResource = new LinkedResource(new MemoryStream(qrCodeBytes), MediaTypeNames.Image.Jpeg);
                qrResource.ContentId = "QRCodeImage";
                htmlView.LinkedResources.Add(qrResource);
            }

            message.AlternateViews.Add(htmlView);

            if (pdfBytes != null)
            {
                var pdfStream = new MemoryStream(pdfBytes);
                var attachment = new Attachment(pdfStream, $"Receipt-{rental.RentalId}.pdf", MediaTypeNames.Application.Pdf);
                message.Attachments.Add(attachment);
            }

            smtp.Send(message);
        }
    }
}