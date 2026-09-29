namespace UD_Autos.Models.Sales;

public enum PaymentMode
{
    Cash = 1,
    Bank = 2,
    Credit = 3,
    Partial = 4
}

public enum PaymentAccount
{
    Cash = 1,
    Bank = 2
}

public enum LedgerEntryType
{
    OpeningBalance = 0,
    Invoice = 1,
    Payment = 2,
    Return = 3,
    Adjustment = 4
}
