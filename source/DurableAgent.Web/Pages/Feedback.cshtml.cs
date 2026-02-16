using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DurableAgent.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace DurableAgent.Web.Pages;

public class FeedbackModel(IConfiguration configuration, IHttpClientFactory httpClientFactory) : PageModel
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    [BindProperty]
    public string StoreId { get; set; } = string.Empty;

    [BindProperty]
    public string OrderId { get; set; } = string.Empty;

    [BindProperty]
    public string PreferredName { get; set; } = string.Empty;

    [BindProperty]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    public string LastName { get; set; } = string.Empty;

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string PhoneNumber { get; set; } = string.Empty;

    [BindProperty]
    public ContactMethod PreferredContactMethod { get; set; } = ContactMethod.Email;

    [BindProperty]
    public int Rating { get; set; }

    [BindProperty]
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
                catch
                {
                    ErrorMessage = $"Failed to submit feedback. Status: {response.StatusCode}";
                }

                if (ValidationErrors.Count == 0 && string.IsNullOrEmpty(ErrorMessage))
                {
                    ErrorMessage = "An unexpected error occurred while submitting your feedback.";
                }

                return Page();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Error submitting feedback: {ex.Message}";
            return Page();
        }
    }
}
