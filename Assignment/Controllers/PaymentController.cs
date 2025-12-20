using Assignment.Models;
using Assignment.ViewModels;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
// NEW NAMESPACES FOR PDF
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

    // [GET] MakePayment ... (Keep your existing GET method exactly the same)
    [HttpGet]
    public IActionResult MakePayment(int modelId, DateTime rentalDate, DateTime returnDate, decimal totalPrice, decimal deposit)
    {
        var model = _db.CarModels.Find(modelId);
        if (model == null) return RedirectToAction("Index", "Home");

        var vm = new PaymentVM
        {
            CarModel = model.ModelName,
            TotalRentalPrice = totalPrice,
            DepositRequired = deposit,
            Amount = totalPrice + deposit
        };

        ViewBag.ModelId = modelId;
        ViewBag.RentalDate = rentalDate;
        ViewBag.ReturnDate = returnDate;

        return View(vm);
    }

    // [POST] MakePayment
    [HttpPost]
    public IActionResult MakePayment(
        int modelId,
        DateTime rentalDate,
        DateTime returnDate,
        decimal totalPrice,
        decimal deposit,
        string paymentMethod) // Receives "Stripe" or "StripeFPX"
    {
        var model = _db.CarModels.Find(modelId);
        if (model == null) return RedirectToAction("Index", "Home");

        decimal grandTotal = totalPrice + deposit;
        long amountInCents = (long)(grandTotal * 100);
        var domain = "https://localhost:7102";

        // 1. Determine Payment Type
        List<string> paymentTypes;
        if (paymentMethod == "StripeFPX")
        {
            paymentTypes = new List<string> { "fpx" };
        }
        else
        {
            // Default to Card if "Stripe" or anything else
            paymentTypes = new List<string> { "card" };
        }

        // 2. Create Stripe Session
        var options = new SessionCreateOptions
        {
            PaymentMethodTypes = paymentTypes,
            LineItems = new List<SessionLineItemOptions>
        {
            new SessionLineItemOptions
            {
                PriceData = new SessionLineItemPriceDataOptions
                {
                    UnitAmount = amountInCents,
                    Currency = "myr",
                    ProductData = new SessionLineItemPriceDataProductDataOptions
                    {
                        Name = $"Car Rental: {model.ModelName}",
                        Description = "Deposit + Rental Fee",
                    },
                },
                Quantity = 1,
            },
        },
            Mode = "payment",

            // 3. IMPORTANT: Pass 'paymentMethod' to the Success URL here!
            SuccessUrl = domain + $"/Payment/Success?modelId={modelId}&rentalDate={rentalDate:yyyy-MM-dd}&returnDate={returnDate:yyyy-MM-dd}&amount={grandTotal}&method={paymentMethod}",

            CancelUrl = domain + $"/Payment/MakePayment?modelId={modelId}&rentalDate={rentalDate:yyyy-MM-dd}&returnDate={returnDate:yyyy-MM-dd}&totalPrice={totalPrice}&deposit={deposit}",
        };

        var service = new SessionService();
        Session session = service.Create(options);
        return Redirect(session.Url);
    }

    // ============================================================
    // 3. SUCCESS HANDLER (UPDATED)
    // ============================================================
    public IActionResult Success(int modelId, DateTime rentalDate, DateTime returnDate, decimal amount, string method)
    {
        if (!User.Identity.IsAuthenticated) return RedirectToAction("Login", "Account");

        string? userIdentity = User.Identity?.Name;
        var customer = _db.Customers.FirstOrDefault(c => c.Email == userIdentity || c.Name == userIdentity);

        if (customer == null)
        {
            TempData["Info"] = "User not found.";
            return RedirectToAction("Index", "Home");
        }

        string loggedInCustomerId = customer.CustomerId;
        string customerEmail = customer.Email;

        // 1. DETERMINE PAYMENT METHOD LABEL
        string nicePaymentMethod = "Credit Card"; // Default
        if (method == "StripeFPX")
        {
            nicePaymentMethod = "Online Banking";
        }
        else if (method == "Stripe")
        {
            nicePaymentMethod = "Credit Card";
        }

        try
        {
            string newRentalId = GenerateRentalId();
            string newPaymentId = GeneratePaymentId();

            decimal rentalFee = Math.Round(amount / 1.2m, 2);
            decimal depositAmt = amount - rentalFee;

            var rental = new Rental
            {
                RentalId = newRentalId,
                CustomerId = customer.CustomerId,
                ModelId = modelId,
                RentalDate = DateTime.Now,
                PickupDate = rentalDate,
                ReturnDate = returnDate,
                DepositAmount = depositAmt,
                TotalPrice = rentalFee,
                Status = "Booked"
            };

            var payment = new Payment
            {
                PaymentId = newPaymentId,
                RentalId = newRentalId,
                Amount = amount,
                PaymentType = "Full Payment",

                // 2. SAVE THE CORRECT NAME TO DATABASE
                PaymentMethod = nicePaymentMethod,

                Status = "Successful",
                Date = DateTime.Now
            };

            _db.Rentals.Add(rental);
            _db.Payments.Add(payment);
            _db.SaveChanges();

            // === EMAIL GENERATION START ===
            try
            {
                string domain = $"{Request.Scheme}://{Request.Host}";
                string pickupUrl = $"{domain}/PickUpReturn/Pickup?RentalId={newRentalId}"; 
                byte[] qrBytes = GenerateQRCode(pickupUrl);
                var carModel = _db.CarModels.Find(modelId);
                string carName = carModel != null ? carModel.ModelName : "Unknown Car";
                byte[] pdfBytes = GeneratePdfReceipt(rental, qrBytes);
                SendEmail(customerEmail, rental, qrBytes, pdfBytes);
            }
            catch (Exception emailEx)
            {
                Console.WriteLine("Email/PDF Error: " + emailEx.Message);
            }
            // === EMAIL GENERATION END ===

            TempData["Info"] = "Payment Successful! Receipt has been sent to your email.";
            return RedirectToAction("Detail", "Rental", new { id = newRentalId });
        }
        catch (Exception ex)
        {
            TempData["Info"] = "DB Error: " + ex.Message;
            return RedirectToAction("Index", "Home");
        }
    }

    // ============================================================
    // HELPERS
    // ============================================================

    private string GenerateRentalId()
    {
        string? max = _db.Rentals.Max(r => r.RentalId);
        if (max == null) return "RN0001";
        int n = int.Parse(max.Substring(2));
        return $"RN{(n + 1):D4}";
    }

    private string GeneratePaymentId()
    {
        string? max = _db.Payments.Max(p => p.PaymentId);
        if (max == null) return "PM0001";
        int n = int.Parse(max.Substring(2));
        return $"PM{(n + 1):D4}";
    }

    private byte[] GenerateQRCode(string url)
    {
        QRCodeGenerator qrGenerator = new QRCodeGenerator();
        QRCodeData qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
        return qrCode.GetGraphic(20);
    }

    // ------------------------------------------------------------
    // NEW: Generate PDF Receipt using iText7
    // ------------------------------------------------------------
    private byte[] GeneratePdfReceipt(Assignment.Models.Rental rental, byte[] qrBytes)
    {
        using (MemoryStream stream = new MemoryStream())
        {
            PdfWriter writer = new PdfWriter(stream);
            PdfDocument pdf = new PdfDocument(writer);
            Document document = new Document(pdf);

            // 1. Setup Fonts & Colors (Matching Bootstrap)
            PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);
            PdfFont normalFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            Color primaryColor = new DeviceRgb(13, 110, 253); // Bootstrap Primary Blue
            Color successColor = new DeviceRgb(25, 135, 84);  // Bootstrap Success Green
            Color mutedColor = DeviceGray.GRAY;               // Bootstrap Muted
            Color lightBg = new DeviceRgb(248, 249, 250);     // Light Gray Background

            // 2. HEADER SECTION (Matches .card-header)
            Table headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 1 })).UseAllAvailableWidth();

            // Left: Title & Ref
            Cell leftHeader = new Cell().SetBorder(Border.NO_BORDER);
            leftHeader.Add(new Paragraph("RECEIPT")
                .SetFont(boldFont).SetFontSize(18));
            leftHeader.Add(new Paragraph($"Ref: #{rental.RentalId}")
                .SetFont(normalFont).SetFontColor(mutedColor).SetFontSize(10));

            // Right: Status & Date
            Cell rightHeader = new Cell().SetBorder(Border.NO_BORDER).SetTextAlignment(TextAlignment.RIGHT);

            // Status Badge (Simulated with Green Text)
            rightHeader.Add(new Paragraph(rental.Status.ToUpper())
                .SetFont(boldFont).SetFontColor(ColorConstants.WHITE)
                .SetBackgroundColor(successColor) // Green Background like Badge
                .SetPadding(3).SetFontSize(10));

            rightHeader.Add(new Paragraph(rental.RentalDate.ToString("dd MMM yyyy, hh:mm tt"))
                .SetFont(normalFont).SetFontColor(mutedColor).SetFontSize(9).SetMarginTop(2));

            headerTable.AddCell(leftHeader);
            headerTable.AddCell(rightHeader);
            document.Add(headerTable);

            document.Add(new Paragraph("\n")); // Spacing

            // 3. CUSTOMER SECTION (Matches .row mb-5)
            document.Add(new Paragraph("BILLED TO")
                .SetFont(boldFont).SetFontSize(8).SetFontColor(mutedColor));
            document.Add(new Paragraph(rental.Customer?.Name ?? "Valued Customer")
                .SetFont(boldFont).SetFontSize(12));
            document.Add(new Paragraph("Valued Customer")
                .SetFont(normalFont).SetFontSize(10).SetFontColor(mutedColor));

            document.Add(new Paragraph("\n"));

            // 4. CAR SUMMARY BOX (Matches .car-summary-box)
            // We create a table with a light gray background to mimic the box
            Table carBox = new Table(1).UseAllAvailableWidth();
            Cell carCell = new Cell();
            carCell.SetBackgroundColor(lightBg); // Light Gray BG
            carCell.SetBorder(new SolidBorder(DeviceGray.GRAY, 0.5f)); // Thin border
            carCell.SetPadding(15);

            // Calculate Days
            int days = (rental.ReturnDate - rental.PickupDate).Days;
            if (days < 1) days = 1;

            // Content inside the box
            string carTitle = $"Model: {rental.Model?.ModelName ?? "Unknown"}";

            carCell.Add(new Paragraph(carTitle)
                .SetFont(boldFont).SetFontSize(12).SetMarginBottom(5));

            carCell.Add(new Paragraph("RENTAL PERIOD")
                .SetFont(boldFont).SetFontSize(8).SetFontColor(mutedColor));

            // Use a nested table for the dates/icons layout
            Table dateTable = new Table(UnitValue.CreatePercentArray(new float[] { 1, 3 })).UseAllAvailableWidth();

            // Pick-up
            dateTable.AddCell(new Cell().Add(new Paragraph("Pick-up:").SetFont(boldFont).SetFontSize(10)).SetBorder(Border.NO_BORDER));
            dateTable.AddCell(new Cell().Add(new Paragraph(rental.PickupDate.ToString("dd MMM yyyy")).SetFont(normalFont).SetFontSize(10)).SetBorder(Border.NO_BORDER));

            // Return
            dateTable.AddCell(new Cell().Add(new Paragraph("Return:").SetFont(boldFont).SetFontSize(10)).SetBorder(Border.NO_BORDER));
            dateTable.AddCell(new Cell().Add(new Paragraph(rental.ReturnDate.ToString("dd MMM yyyy")).SetFont(normalFont).SetFontSize(10)).SetBorder(Border.NO_BORDER));

            // Duration (Blue Text)
            dateTable.AddCell(new Cell().Add(new Paragraph("Duration:").SetFont(boldFont).SetFontSize(10).SetFontColor(primaryColor)).SetBorder(Border.NO_BORDER));
            dateTable.AddCell(new Cell().Add(new Paragraph($"{days} Day(s)").SetFont(boldFont).SetFontSize(10).SetFontColor(primaryColor)).SetBorder(Border.NO_BORDER));

            carCell.Add(dateTable);
            carBox.AddCell(carCell);
            document.Add(carBox);

            document.Add(new Paragraph("\n"));

            // 5. PRICE TABLE (Matches .table-responsive)
            Table priceTable = new Table(UnitValue.CreatePercentArray(new float[] { 3, 1 })).UseAllAvailableWidth();

            // Header Row
            priceTable.AddHeaderCell(new Cell().Add(new Paragraph("DESCRIPTION").SetFont(boldFont).SetFontSize(9).SetFontColor(mutedColor)).SetBorder(Border.NO_BORDER));
            priceTable.AddHeaderCell(new Cell().Add(new Paragraph("AMOUNT (RM)").SetFont(boldFont).SetFontSize(9).SetFontColor(mutedColor)).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));

            // Divider Line
            priceTable.AddCell(new Cell(1, 2).SetBorderBottom(new SolidBorder(DeviceGray.GRAY, 0.5f)).SetBorderLeft(Border.NO_BORDER).SetBorderRight(Border.NO_BORDER).SetBorderTop(Border.NO_BORDER));

            // Item 1: Rental Charges
            priceTable.AddCell(new Cell().Add(new Paragraph($"Rental Charges ({days} Days)").SetFont(normalFont).SetFontSize(10)).SetBorder(Border.NO_BORDER).SetPaddingTop(10));
            priceTable.AddCell(new Cell().Add(new Paragraph(rental.TotalPrice.ToString("N2")).SetFont(boldFont).SetFontSize(10)).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER).SetPaddingTop(10));

            // Item 2: Security Deposit
            priceTable.AddCell(new Cell().Add(new Paragraph("Security Deposit (Refundable)").SetFont(normalFont).SetFontSize(10).SetFontColor(mutedColor)).SetBorder(Border.NO_BORDER));
            priceTable.AddCell(new Cell().Add(new Paragraph(rental.DepositAmount.ToString("N2")).SetFont(normalFont).SetFontSize(10).SetFontColor(mutedColor)).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER));

            // Footer Row (Total Paid)
            decimal totalPaid = rental.TotalPrice + rental.DepositAmount;

            // Add a line before total
            Cell lineCell = new Cell(1, 2).SetBorder(Border.NO_BORDER);
            lineCell.Add(new LineSeparator(new iText.Kernel.Pdf.Canvas.Draw.SolidLine(1f)).SetMarginTop(10));
            priceTable.AddCell(lineCell);

            priceTable.AddCell(new Cell().Add(new Paragraph("Total Paid").SetFont(boldFont).SetFontSize(12)).SetBorder(Border.NO_BORDER).SetPaddingTop(5));
            priceTable.AddCell(new Cell().Add(new Paragraph($"RM {totalPaid:N2}").SetFont(boldFont).SetFontSize(14).SetFontColor(successColor)).SetTextAlignment(TextAlignment.RIGHT).SetBorder(Border.NO_BORDER).SetPaddingTop(5));

            document.Add(priceTable);

            document.Add(new Paragraph("\n\n"));

            // 6. QR CODE SECTION
            if (qrBytes != null && qrBytes.Length > 0)
            {
                try
                {
                    var qrImage = new iText.Layout.Element.Image(
                        iText.IO.Image.ImageDataFactory.Create(qrBytes));
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


    // ------------------------------------------------------------
    // Send Email with Attachment
    // ------------------------------------------------------------
    private void SendEmail(string userEmail, Assignment.Models.Rental rental, byte[] qrCodeBytes, byte[] pdfBytes)
    {
        // 1. Setup Configuration
        string host = _configuration["Smtp:Host"] ?? "smtp.gmail.com";
        int port = int.Parse(_configuration["Smtp:Port"] ?? "587");
        string senderEmail = _configuration["Smtp:User"] ?? "waixianho@gmail.com";
        string senderPass = _configuration["Smtp:Pass"] ?? "hmor krvp syey vewp";
        string senderName = _configuration["Smtp:Name"] ?? "Car Rental Admin";

        // 2. Prepare Data for the Email View
        int days = (rental.ReturnDate - rental.PickupDate).Days;
        if (days < 1) days = 1;
        decimal totalPaid = rental.TotalPrice + rental.DepositAmount;
        string brand = rental.Model?.Brand?.BrandName ?? "Unknown";
        string model = rental.Model?.ModelName ?? "Unknown";
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
                                     {model}
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

        // 4. Send the Email
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

            // A. Create the View
            var htmlView = AlternateView.CreateAlternateViewFromString(body, null, MediaTypeNames.Text.Html);

            // B. Embed the QR Code (so 'cid:QRCodeImage' works)
            if (qrCodeBytes != null)
            {
                var qrResource = new LinkedResource(new MemoryStream(qrCodeBytes), MediaTypeNames.Image.Jpeg);
                qrResource.ContentId = "QRCodeImage";
                htmlView.LinkedResources.Add(qrResource);
            }

            message.AlternateViews.Add(htmlView);

            // C. Attach the PDF
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