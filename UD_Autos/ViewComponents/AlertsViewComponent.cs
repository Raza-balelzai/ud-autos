using Microsoft.AspNetCore.Mvc;
using UD_Autos.Services;

namespace UD_Autos.ViewComponents;

public class AlertsViewComponent : ViewComponent
{
    private readonly IOperationalAlertService _alerts;

    public AlertsViewComponent(IOperationalAlertService alerts) => _alerts = alerts;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var items = await _alerts.GetAlertsAsync(HttpContext.RequestAborted);
        return View(items);
    }
}
