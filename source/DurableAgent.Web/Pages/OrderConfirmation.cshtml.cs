using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DurableAgent.Web.Pages;

public sealed class OrderConfirmationModel : PageModel
{
    public string OrderReference { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string FlavorName { get; set; } = string.Empty;
    public string StreetAddress { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    public IActionResult OnGet()
    {
        if (TempData["OrderReference"] is string orderRef)
        {
            OrderReference = orderRef;
            FirstName = TempData["FirstName"] as string ?? string.Empty;
            FlavorName = TempData["FlavorName"] as string ?? string.Empty;
            StreetAddress = TempData["StreetAddress"] as string ?? string.Empty;
            AddressLine2 = TempData["AddressLine2"] as string;
            City = TempData["City"] as string ?? string.Empty;
            State = TempData["State"] as string ?? string.Empty;
            ZipCode = TempData["ZipCode"] as string ?? string.Empty;
            Email = TempData["Email"] as string ?? string.Empty;
            
            return Page();
        }

        return RedirectToPage("/Order");
    }
}
