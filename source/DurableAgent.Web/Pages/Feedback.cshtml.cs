using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DurableAgent.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DurableAgent.Web.Pages;

public class FeedbackModel(IConfiguration configuration, IHttpClientFactory httpClientFactory, ILogger<FeedbackModel> logger) : PageModel
{
    private const string StoresLoadErrorMessage = "Unable to load store locations. Please try refreshing the page.";
    private const string FlavorsLoadErrorMessage = "Unable to load flavor options. Please try refreshing the page.";
    private const string GenericLoadErrorMessage = "An error occurred while loading form data. Please try refreshing the page.";
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [BindProperty]
    [Required(ErrorMessage = "Store ID is required")]
    [StringLength(50, ErrorMessage = "Store ID cannot exceed 50 characters")]
    public string StoreId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Order ID is required")]
    [StringLength(50, ErrorMessage = "Order ID cannot exceed 50 characters")]
    public string OrderId { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Preferred name is required")]
    [StringLength(100, ErrorMessage = "Preferred name cannot exceed 100 characters")]
    public string PreferredName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "First name is required")]
    [StringLength(100, ErrorMessage = "First name cannot exceed 100 characters")]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Last name is required")]
    [StringLength(100, ErrorMessage = "Last name cannot exceed 100 characters")]
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address")]
    [StringLength(200, ErrorMessage = "Email cannot exceed 200 characters")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Phone number is required")]
    [Phone(ErrorMessage = "Please enter a valid phone number")]
    [StringLength(20, ErrorMessage = "Phone number cannot exceed 20 characters")]
    public string PhoneNumber { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Preferred contact method is required")]
    public ContactMethod PreferredContactMethod { get; set; } = ContactMethod.Email;

    [BindProperty]
    [Required(ErrorMessage = "Rating is required")]
    [Range(1, 5, ErrorMessage = "Rating must be between 1 and 5")]
    public int? Rating { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Comment is required")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Comment must be between 10 and 2000 characters")]
    public string Comment { get; set; } = string.Empty;

    [BindProperty]
    [Required(ErrorMessage = "Flavor selection is required")]
    [StringLength(50, ErrorMessage = "Flavor ID cannot exceed 50 characters")]
    public string FlavorId { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = [];
    public List<Store> Stores { get; set; } = [];
    public List<Flavor> Flavors { get; set; } = [];
    public HashSet<string> ApiLoadWarnings { get; set; } = [];

    public async Task OnGetAsync()
    {
        await LoadStoresAndFlavorsAsync();
    }

    private async Task LoadStoresAndFlavorsAsync()
    {
        ApiLoadWarnings.Clear();
        
        try
        {
            var storesUrl = configuration["AzureFunctions:StoresApiUrl"];
            var flavorsUrl = configuration["AzureFunctions:FlavorsApiUrl"];

            if (string.IsNullOrWhiteSpace(storesUrl) || string.IsNullOrWhiteSpace(flavorsUrl))
            {
                logger.LogWarning("Stores or Flavors API URL not configured");
                // Add specific warnings for each unconfigured URL
                if (string.IsNullOrWhiteSpace(storesUrl))
                {
                    ApiLoadWarnings.Add(StoresLoadErrorMessage);
                }
                if (string.IsNullOrWhiteSpace(flavorsUrl))
                {
                    ApiLoadWarnings.Add(FlavorsLoadErrorMessage);
                }
                return;
            }

            var httpClient = httpClientFactory.CreateClient();

            // Load stores
            try
            {
                var storesResponse = await httpClient.GetAsync(storesUrl, HttpContext.RequestAborted);
                if (storesResponse.IsSuccessStatusCode)
                {
                    var storesJson = await storesResponse.Content.ReadAsStringAsync(HttpContext.RequestAborted);
                    Stores = JsonSerializer.Deserialize<List<Store>>(storesJson, JsonOptions) ?? [];
                }
                else
                {
                    logger.LogWarning("Failed to load stores. Status: {StatusCode}", storesResponse.StatusCode);
                    ApiLoadWarnings.Add(StoresLoadErrorMessage);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load stores");
                ApiLoadWarnings.Add(StoresLoadErrorMessage);
            }

            // Load flavors
            try
            {
                var flavorsResponse = await httpClient.GetAsync(flavorsUrl, HttpContext.RequestAborted);
                if (flavorsResponse.IsSuccessStatusCode)
                {
                    var flavorsJson = await flavorsResponse.Content.ReadAsStringAsync(HttpContext.RequestAborted);
                    Flavors = JsonSerializer.Deserialize<List<Flavor>>(flavorsJson, JsonOptions) ?? [];
                }
                else
                {
                    logger.LogWarning("Failed to load flavors. Status: {StatusCode}", flavorsResponse.StatusCode);
                    ApiLoadWarnings.Add(FlavorsLoadErrorMessage);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to load flavors");
                ApiLoadWarnings.Add(FlavorsLoadErrorMessage);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error loading stores and flavors");
            ApiLoadWarnings.Add(GenericLoadErrorMessage);
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidationErrors.Clear();
        ErrorMessage = null;
        IsSuccess = false;

        if (!ModelState.IsValid)
        {
            // Reload stores and flavors for the dropdowns when validation fails
            await LoadStoresAndFlavorsAsync();
            return Page();
        }

        var feedbackRequest = new
        {
            storeId = StoreId,
            orderId = OrderId,
            customer = new
            {
                preferredName = PreferredName,
                firstName = FirstName,
                lastName = LastName,
                email = Email,
                phoneNumber = PhoneNumber,
                preferredContactMethod = PreferredContactMethod.ToString()
            },
            channel = "web",
            rating = Rating,
            comment = Comment,
            flavorId = FlavorId
        };

        try
        {
            var apiUrl = configuration["AzureFunctions:FeedbackApiUrl"] 
                ?? throw new InvalidOperationException("FeedbackApiUrl not configured");

            var httpClient = httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(feedbackRequest, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(apiUrl, content, HttpContext.RequestAborted);

            if (response.IsSuccessStatusCode)
            {
                IsSuccess = true;
                ModelState.Clear();
                return Page();
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted);
                
                try
                {
                    var errorResponse = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(errorContent);
                    if (errorResponse != null && errorResponse.ContainsKey("errors"))
                    {
                        var errors = errorResponse["errors"];
                        if (errors.ValueKind == JsonValueKind.Array)
                        {
                            foreach (var error in errors.EnumerateArray())
                            {
                                ValidationErrors.Add(error.GetString() ?? "Unknown error");
                            }
                        }
                    }
                    else if (errorResponse != null && errorResponse.ContainsKey("error"))
                    {
                        ErrorMessage = errorResponse["error"].GetString();
                    }
                }
                catch (JsonException ex)
                {
                    logger.LogWarning(ex, "Failed to parse error response from feedback API. Status: {StatusCode}", response.StatusCode);
                    ErrorMessage = $"Failed to submit feedback. Status: {response.StatusCode}";
                }

                if (ValidationErrors.Count == 0 && string.IsNullOrEmpty(ErrorMessage))
                {
                    ErrorMessage = "An unexpected error occurred while submitting your feedback.";
                }

                return Page();
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error while submitting feedback");
            ErrorMessage = "Unable to connect to the feedback service. Please try again later.";
            return Page();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while submitting feedback");
            ErrorMessage = "An unexpected error occurred. Please try again later.";
            return Page();
        }
    }
}
