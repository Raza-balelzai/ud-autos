using UD_Autos.Models.Customers;
using UD_Autos.Models.Products;
using UD_Autos.Models.Sales;
using UD_Autos.Services;

namespace UD_Autos.Tests;

public class InvoicePdfTests
{
    [Fact]
    public void Generate_ReturnsPdfBytes()
    {
        var invoice = new Invoice
        {
            InvoiceNumber = "INV-TEST-001",
            InvoiceDate = new DateTime(2026, 9, 30),
            Customer = new Customer
            {
                Name = "Test Shop",
                ShopName = "Test Shop",
                Phone = "0300-0000000",
                City = "Islamabad"
            },
            Subtotal = 1000,
            DiscountAmount = 50,
            DeliveryCharge = 20,
            OtherCharge = 10,
            BillTotal = 980,
            PreviousBalance = 200,
            PaymentReceived = 500,
            NewBalance = 680,
            Items =
            {
                new InvoiceItem
                {
                    Quantity = 2,
                    UnitPrice = 500,
                    LineTotal = 1000,
                    Product = new Product { Name = "Brake Pad", Sku = "SKU-1" }
                }
            }
        };

        var bytes = new InvoicePdfService().Generate(invoice);

        Assert.True(bytes.Length > 100);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }
}
