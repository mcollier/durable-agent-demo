using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Serialization;
using DurableAgent.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DurableAgent.Web.Pages;

public sealed class OrderModel(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<OrderModel> logger) : PageModel
{
    private const string FlavorsLoadErrorMessage = "Unable to load flavor options. Please try refreshing the page.";
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private static readonly IReadOnlyList<(string Code, string Name)> States =
    [
        ("AL", "Alabama"), ("AK", "Alaska"), ("AZ", "Arizona"), ("AR", "Arkansas"),
        ("CA", "California"), ("CO", "Colorado"), ("CT", "Connecticut"), ("DE", "Delaware"),
        ("DC", "District of Columbia"), ("FL", "Florida"), ("GA", "Georgia"), ("HI", "Hawaii"),
        ("ID", "Idaho"), ("IL", "Illinois"), ("IN", "Indiana"), ("IA", "Iowa"),
        ("KS", "Kansas"), ("KY", "Kentucky"), ("LA", "Louisiana"), ("ME", "Maine"),
        ("MD", "Maryland"), ("MA", "Massachusetts"), ("MI", "Michigan"), ("MN", "Minnesota"),
        ("MS", "Mississippi"), ("MO", "Missouri"), ("MT", "Montana"), ("NE", "Nebraska"),
        ("NV", "Nevada"), ("NH", "New Hampshire"), ("NJ", "New Jersey"), ("NM", "New Mexico"),
        ("NY", "New York"), ("NC", "North Carolina"), ("ND", "North Dakota"), ("OH", "Ohio"),
        ("OK", "Oklahoma"), ("OR", "Oregon"), ("PA", "Pennsylvania"), ("RI", "Rhode Island"),
        ("SC", "South Carolina"), ("SD", "South Dakota"), ("TN", "Tennessee"), ("TX", "Texas"),
        ("UT", "Utah"), ("VT", "Vermont"), ("VA", "Virginia"), ("WA", "Washington"),
        ("WV", "West Virginia"), ("WI", "Wisconsin"), ("WY", "Wyoming")
    ];

    [BindProperty]
    [Required(ErrorMessage = "Flavor is required")]
    public string FlavorId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "First name is required")]
    [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Last name is required")]
    [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Street address is required")]
    [StringLength(200, ErrorMessage = "Street address cannot exceed 200 characters")]
    public string StreetAddress { get; set; } = string.Empty;

    [BindProperty]
    [StringLength(100, ErrorMessage = "Address line 2 cannot exceed 100 characters")]
    public string? AddressLine2 { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "City is required")]
    [StringLength(100, ErrorMessage = "City cannot exceed 100 characters")]
    public string City { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "State is required")]
    public string State { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "ZIP code is required")]
    [RegularExpression(@"[0-9]{5}(-[0-9]{4})?", ErrorMessage = "ZIP code must be in format 12345 or 12345-6789")]
    public string ZipCode { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [StringLength(200, ErrorMessage = "Email cannot exceed 200 characters")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Phone number is required")]
    [RegularExpression(@"^\(\d{3}\) \d{3}-\d{4}$", ErrorMessage = "Phone number must be in the format (xxx) xxx-xxxx")]
    [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
    public string PhoneNumber { get; set; } = string.Empty;

    public IReadOnlyList<Flavor> Flavors { get; set; } = [];
    public HashSet<string> WarningMessages { get; set; } = [];
    public List<string> ValidationErrors { get; set; } = [];

    public IReadOnlyList<(string Code, string Name)> UsStates => States;

    public async Task OnGetAsync()
    {
        await LoadFlavorsAsync();
    }

    private async Task LoadFlavorsAsync()
    {
        WarningMessages.Clear();
        
        try
        {
            var baseUrl = configuration["AzureFunctions:BaseUrl"];
            var flavorsPath = configuration["AzureFunctions:FlavorsPath"] ?? "api/flavors";

            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                logger.LogWarning("Azure Functions base URL not configured");
                WarningMessages.Add(FlavorsLoadErrorMessage);
                return;
            }

            var flavorsUrl = $"{baseUrl!.TrimEnd('/')}/{flavorsPath.TrimStart('/')}";
            var httpClient = httpClientFactory.CreateClient();

            try
            {
                var flavorsResponse = await httpClient.GetAsync(flavorsUrl, HttpContext.RequestAborted);
                if (flavorsResponse.IsSuccessStatusCode)
                {
                    var flavorsJson = await flavorsResponse.Content.ReadAsStringAsync(HttpContext.RequestAborted);
                    var flavorsList = JsonSerializer.Deserialize<List<Flavor>>(flavorsJson, JsonOptions);
                    Flavors = flavorsList ?? [];
                }
                else
                {
                    logger.LogWarning("Failed to load flavors. Status: {StatusCode}", flavorsResponse.StatusCode);
                    WarningMessages.Add(FlavorsLoadErrorMessage);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load flavors");
                WarningMessages.Add(FlavorsLoadErrorMessage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading flavors");
            WarningMessages.Add(FlavorsLoadErrorMessage);
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidationErrors.Clear();
        WarningMessages.Clear();

        if (!ModelState.IsValid)
        {
            await LoadFlavorsAsync();
            return Page();
        }

        var orderReference = $"FRY-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";

        await LoadFlavorsAsync();
        
        var flavorName = Flavors.FirstOrDefault(f => f.FlavorId == FlavorId)?.Name ?? FlavorId;

        TempData["OrderReference"] = orderReference;
        TempData["FirstName"] = FirstName;
        TempData["FlavorName"] = flavorName;
        TempData["StreetAddress"] = StreetAddress;
        TempData["AddressLine2"] = AddressLine2;
        TempData["City"] = City;
        TempData["State"] = State;
        TempData["ZipCode"] = ZipCode;
        TempData["Email"] = Email;

        return RedirectToPage("/OrderConfirmation");
    }
}
