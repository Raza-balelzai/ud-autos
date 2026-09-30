using System.ComponentModel.DataAnnotations;
using UD_Autos.Models.Inventory;
using UD_Autos.Models.Shipments;
using UD_Autos.Services;

namespace UD_Autos.Models.ViewModels;

public class LandedCostPageViewModel
{
    public Shipment Shipment { get; set; } = null!;
    public LandedCostPreview? Preview { get; set; }
    public LandedCostAllocationMethod Method { get; set; } = LandedCostAllocationMethod.ByWeight;
    public decimal? CostingExchangeRate { get; set; }
    public decimal? AgentChargesPkr { get; set; }
    public List<CustomAllocationInput> CustomInputs { get; set; } = new();
}

public class ReceivingPageViewModel
{
    public Shipment Shipment { get; set; } = null!;
    public List<ReceivingLineInput> Lines { get; set; } = new();
}

public class StockAdjustmentViewModel
{
    public int ProductId { get; set; }
    public int? BatchId { get; set; }

    [Required]
    public decimal QuantityDelta { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class DamageStockViewModel
{
    public int ProductId { get; set; }
    public int BatchId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class StockHoldReleaseViewModel
{
    public int ProductId { get; set; }
    public int? BatchId { get; set; }

    [Range(0.0001, double.MaxValue)]
    public decimal Quantity { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;

    /// <summary>hold or release</summary>
    public string Mode { get; set; } = "hold";
}
