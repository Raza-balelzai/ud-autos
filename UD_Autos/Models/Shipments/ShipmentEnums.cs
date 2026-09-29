namespace UD_Autos.Models.Shipments;

public enum ShipmentStatus
{
    Draft = 0,
    ChinaWarehouse = 1,
    InTransit = 2,
    Karachi = 3,
    Tarnol = 4,
    Received = 5
}

public enum FreightMethod
{
    ByKg = 1,
    ByCbm = 2
}

public enum ShipmentDocumentType
{
    WarehouseReceivingSlip = 1,
    AgentInvoice = 2,
    PackingList = 3,
    FreightBill = 4,
    Bilty = 5,
    KarachiTransportReceipt = 6,
    CartonPhoto = 7,
    ProductPhoto = 8,
    Other = 99
}

public enum ShipmentTimelineEventType
{
    Created = 0,
    FactoryGoodsSent = 1,
    ChinaWarehouseReceived = 2,
    Consolidated = 3,
    DepartedChina = 4,
    InTransit = 5,
    ArrivedKarachi = 6,
    Processing = 7,
    DispatchedTarnol = 8,
    ReceivedTarnol = 9,
    Completed = 10,
    StatusChanged = 50,
    Note = 99
}
