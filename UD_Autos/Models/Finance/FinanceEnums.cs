namespace UD_Autos.Models.Finance;

public enum CashBankMovementType
{
    Opening = 0,
    Collection = 1,
    OtherReceipt = 2,
    Hawala = 3,
    Expense = 4,
    OtherPayment = 5,
    SalesRefund = 6
}

public enum CashBankDirection
{
    In = 1,
    Out = 2
}
