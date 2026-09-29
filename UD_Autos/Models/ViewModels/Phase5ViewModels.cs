using UD_Autos.Models.Finance;
using UD_Autos.Models.Sales;
using UD_Autos.Models.Settings;
using UD_Autos.Services;

namespace UD_Autos.Models.ViewModels;

public class CashBankPageViewModel
{
    public CashBankStatement Statement { get; set; } = new();
    public PaymentAccount? AccountFilter { get; set; }
    public DateTime From { get; set; } = DateTime.UtcNow.Date;
    public DateTime To { get; set; } = DateTime.UtcNow.Date;
    public PaymentAccount OpeningAccount { get; set; } = PaymentAccount.Cash;
    public DateTime OpeningDate { get; set; } = DateTime.UtcNow.Date;
    public decimal OpeningAmount { get; set; }
    public PaymentAccount ManualAccount { get; set; } = PaymentAccount.Cash;
    public DateTime ManualDate { get; set; } = DateTime.UtcNow.Date;
    public CashBankMovementType ManualType { get; set; } = CashBankMovementType.OtherReceipt;
    public decimal ManualAmount { get; set; }
    public string ManualDetail { get; set; } = string.Empty;
    public string? ManualNotes { get; set; }
}

public class ExpenseFormViewModel
{
    public OperatingExpense Expense { get; set; } = new();
    public IReadOnlyList<ExpenseCategory> Categories { get; set; } = Array.Empty<ExpenseCategory>();
}

public class ReportsIndexViewModel
{
    public string Family { get; set; } = "sales";
    public string Report { get; set; } = "daily";
    public DateTime From { get; set; } = DateTime.UtcNow.Date.AddDays(-30);
    public DateTime To { get; set; } = DateTime.UtcNow.Date;
    public string? Search { get; set; }
    public ReportResult Result { get; set; } = new();
    public bool CanViewProfit { get; set; }
}
