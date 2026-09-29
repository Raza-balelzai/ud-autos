namespace UD_Autos.Models.Inventory;

public enum LandedCostAllocationMethod
{
    ByWeight = 1,
    ByCbm = 2,
    ByQuantity = 3,
    ByProductValue = 4,
    Custom = 5
}

public enum StockMovementType
{
    ShipmentReceived = 1,
    Sale = 2,
    CustomerReturn = 3,
    SupplierReturn = 4,
    Damaged = 5,
    Adjustment = 6
}
