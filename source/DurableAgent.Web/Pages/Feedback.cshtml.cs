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
    public int Rating { get; set; }

    [BindProperty]
    [Required(ErrorMessage = "Comment is required")]
    [StringLength(2000, MinimumLength = 10, ErrorMessage = "Comment must be between 10 and 2000 characters")]
    public string Comment { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> ValidationErrors { get; set; } = [];

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        ValidationErrors.Clear();
        ErrorMessage = null;
        IsSuccess = false;

        if (!ModelState.IsValid)
        {
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
            comment = Comment
        };

        try
        {
            var apiUrl = configuration["AzureFunctions:FeedbackApiUrl"] 
                ?? throw new InvalidOperationException("FeedbackApiUrl not configured");

            var httpClient = httpClientFactory.CreateClient();
            var json = JsonSerializer.Serialize(feedbackRequest, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(apiUrl, content);

            if (response.IsSuccessStatusCode)
            {
                IsSuccess = true;
                ModelState.Clear();
                return Page();
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                
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
