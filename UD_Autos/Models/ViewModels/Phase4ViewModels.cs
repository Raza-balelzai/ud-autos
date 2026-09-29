using UD_Autos.Models.Customers;
using UD_Autos.Models.Products;
using UD_Autos.Models.Sales;
using UD_Autos.Services;

namespace UD_Autos.Models.ViewModels;

public class BillingPageViewModel
{
    public int? CustomerId { get; set; }
    public Customer? Customer { get; set; }
    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow.Date;
    public PaymentMode PaymentMode { get; set; } = PaymentMode.Credit;
    public PaymentAccount? PaymentAccount { get; set; } = Models.Sales.PaymentAccount.Cash;
    public decimal DiscountAmount { get; set; }
    public decimal DeliveryCharge { get; set; }
    public decimal OtherCharge { get; set; }
    public decimal PaymentReceived { get; set; }
    public string? PaymentReference { get; set; }
    public string? Notes { get; set; }
    public string IdempotencyKey { get; set; } = Guid.NewGuid().ToString("N");
    public List<SaleLineInput> Items { get; set; } = new() { new() };
    public IReadOnlyList<Customer> Customers { get; set; } = Array.Empty<Customer>();
    public IReadOnlyList<Product> Products { get; set; } = Array.Empty<Product>();
}

public class ReceivePaymentViewModel
{
    public int CustomerId { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow.Date;
    public decimal Amount { get; set; }
    public PaymentAccount Account { get; set; } = PaymentAccount.Cash;
    public string? Reference { get; set; }
    public string? Notes { get; set; }
    public bool UseManualAllocation { get; set; }
    public List<ManualAllocationInput> Allocations { get; set; } = new();
    public IReadOnlyList<Customer> Customers { get; set; } = Array.Empty<Customer>();
    public IReadOnlyList<Invoice> OpenInvoices { get; set; } = Array.Empty<Invoice>();
}

public class SalesReturnFormViewModel
{
    public int InvoiceId { get; set; }
    public Invoice? Invoice { get; set; }
    public DateTime ReturnDate { get; set; } = DateTime.UtcNow.Date;
    public string Reason { get; set; } = string.Empty;
    public bool AdjustKhata { get; set; } = true;
    public PaymentAccount? RefundAccount { get; set; }
    public List<ReturnLineInput> Lines { get; set; } = new();
}
