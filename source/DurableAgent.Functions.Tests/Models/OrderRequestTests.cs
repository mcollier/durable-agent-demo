using DurableAgent.Functions.Models;

namespace DurableAgent.Functions.Tests.Models;

public class OrderRequestTests
{
    private static OrderRequest CreateValidRequest() => new()
    {
        OrderReference = "FRY-20260308-AB12",
        FlavorId = "VNE",
        FirstName = "Jane",
        LastName = "Smith",
        StreetAddress = "123 Main St",
        City = "Springfield",
        State = "IL",
        ZipCode = "62701",
        Quantity = 5
    };

    // ── Valid → no errors ────────────────────────────────────────────────────

    [Fact]
    public void WhenOrderRequestIsValid_ThenValidateReturnsNoErrors()
    {
        var order = CreateValidRequest();

        var errors = order.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void WhenOptionalFieldsOmitted_ThenValidateReturnsNoErrors()
    {
        // addressLine2, email, phoneNumber are optional
        var order = CreateValidRequest();

        var errors = order.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void WhenAllOptionalFieldsProvided_ThenValidateReturnsNoErrors()
    {
        var order = CreateValidRequest() with
        {
            AddressLine2 = "Apt 4B",
            Email = "jane@example.com",
            PhoneNumber = "555-0199"
        };

        var errors = order.Validate();

        Assert.Empty(errors);
    }

    // ── Missing required fields → errors ─────────────────────────────────────

    [Fact]
    public void WhenOrderRequestMissingRequiredFields_ThenValidateReturnsErrors()
    {
        var order = new OrderRequest(); // all properties null

        var errors = order.Validate();

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void WhenOrderReferenceMissing_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { OrderReference = null };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("OrderReference", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenFlavorIdMissing_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { FlavorId = null };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("FlavorId", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenFirstNameMissing_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { FirstName = null };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("FirstName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenLastNameMissing_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { LastName = null };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("LastName", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenStreetAddressMissing_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { StreetAddress = null };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("StreetAddress", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenCityMissing_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { City = null };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("City", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenStateMissing_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { State = null };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("State", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenZipCodeMissing_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { ZipCode = null };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("ZipCode", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenMultipleRequiredFieldsMissing_ThenValidateReturnsMultipleErrors()
    {
        // Missing all name and address fields
        var order = new OrderRequest
        {
            OrderReference = "FRY-20260308-AB12",
            FlavorId = "VNE",
            Quantity = 5
        };

        var errors = order.Validate();

        Assert.True(errors.Count >= 2, $"Expected at least 2 errors, got {errors.Count}.");
    }

    // ── Quantity validation ──────────────────────────────────────────────────

    [Fact]
    public void WhenQuantityIsNull_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { Quantity = null };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("quantity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenQuantityIsZero_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { Quantity = 0 };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("quantity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenQuantityIsEleven_ThenValidateReturnsError()
    {
        var order = CreateValidRequest() with { Quantity = 11 };

        var errors = order.Validate();

        Assert.Contains(errors, e => e.Contains("quantity", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void WhenQuantityIsOne_ThenValidateReturnsNoErrors()
    {
        // Boundary: minimum valid quantity
        var order = CreateValidRequest() with { Quantity = 1 };

        var errors = order.Validate();

        Assert.Empty(errors);
    }

    [Fact]
    public void WhenQuantityIsTen_ThenValidateReturnsNoErrors()
    {
        // Boundary: maximum valid quantity
        var order = CreateValidRequest() with { Quantity = 10 };

        var errors = order.Validate();

        Assert.Empty(errors);
    }
}
