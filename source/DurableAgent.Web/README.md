# Froyo Foundry Web Application

An ASP.NET Core Razor Pages web application for collecting customer feedback with fun ice cream-themed branding.

## Overview

This web application provides a customer-facing feedback form that submits data to the Azure Functions backend via the `/api/feedback` endpoint. The form collects store information, customer details, ratings, and comments.

## Features

- **Froyo Foundry Branding**: Fun ice cream colors and emoji graphics
- **Interactive Rating System**: 5-star rating selector
- **Form Validation**: Client-side and server-side validation
- **Error Handling**: User-friendly error messages
- **Responsive Design**: Bootstrap-based responsive layout

## Running Locally

### Prerequisites

- .NET 10 SDK
- Azure Functions backend running (optional for testing form UI)

### Steps

1. Navigate to the web project directory:
   ```bash
   cd source/DurableAgent.Web
   ```

2. Run the application:
   ```bash
   dotnet run
   ```

3. Open your browser to:
   ```
   http://localhost:5012
   ```

### Configuration

The application is configured via `appsettings.json`. Update the Azure Function endpoint URL as needed:

```json
{
  "AzureFunctions": {
    "FeedbackApiUrl": "http://localhost:7071/api/feedback"
  }
}
```

For production, set this to your deployed Azure Function App URL:
```json
{
  "AzureFunctions": {
    "FeedbackApiUrl": "https://your-function-app.azurewebsites.net/api/feedback"
  }
}
```

## Project Structure

```
DurableAgent.Web/
├── Pages/
│   ├── Shared/
│   │   └── _Layout.cshtml          # Main layout with Froyo branding
│   ├── Index.cshtml                # Home page
│   ├── Index.cshtml.cs             # Home page model
│   ├── Feedback.cshtml             # Feedback form view
│   └── Feedback.cshtml.cs          # Feedback form logic
├── wwwroot/
│   ├── css/
│   │   ├── froyo.css               # Froyo Foundry theme
│   │   └── site.css                # Additional styles
│   └── lib/                        # Bootstrap, jQuery, etc.
├── Program.cs                      # Application startup
└── appsettings.json                # Configuration
```

## Form Fields

The feedback form includes:

- **Store ID**: Identifier for the store location
- **Order ID**: Order reference number
- **Customer Information**:
  - Preferred Name
  - First Name
  - Last Name
  - Email Address
  - Phone Number
  - Preferred Contact Method (Email/Phone)
- **Rating**: 1-5 star rating
- **Comment**: Free-text feedback

## API Integration

The form submits a JSON payload to the Azure Function endpoint:

```json
{
  "storeId": "STORE-001",
  "orderId": "ORD-12345",
  "customer": {
    "preferredName": "Sam",
    "firstName": "Samuel",
    "lastName": "Johnson",
    "email": "sam.johnson@example.com",
    "phoneNumber": "555-123-4567",
    "preferredContactMethod": "Email"
  },
  "channel": "web",
  "rating": 5,
  "comment": "Great experience!"
}
```

## Deployment

### Azure App Service

1. Build the project:
   ```bash
   dotnet publish -c Release
   ```

2. Deploy to Azure App Service using your preferred method (Azure CLI, Visual Studio, GitHub Actions, etc.)

3. Update the `FeedbackApiUrl` configuration in Azure App Service Application Settings to point to your Azure Function App.

### Docker (Optional)

A Dockerfile can be added for containerized deployment if needed.

## Theming

The Froyo Foundry theme uses ice cream-inspired colors defined in `wwwroot/css/froyo.css`:

- **Strawberry Pink** (#FFB3D9, #FF6B9D)
- **Mint Green** (#B4E7CE, #93C572)
- **Vanilla** (#FFF8DC)
- **Blueberry** (#A8D8EA)
- **Mango** (#FFD96A)
- **Chocolate** (#8B4513)

## Development

### Adding New Pages

1. Create a new `.cshtml` file in the `Pages` directory
2. Add a corresponding `.cshtml.cs` file for the page model
3. Update the navigation in `Pages/Shared/_Layout.cshtml` if needed

### Modifying Styles

- Edit `wwwroot/css/froyo.css` for theme-specific styles
- Edit `wwwroot/css/site.css` for general site styles

## License

See the repository LICENSE file for details.
