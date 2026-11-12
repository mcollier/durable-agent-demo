using DurableAgent.Functions.Models;

namespace DurableAgent.Functions.Tests.Models;

public class OrderIntakeResultTests
{
    [Fact]
    public void WhenOrderIsInvalid_ThenCustomerNameCanStillBePopulated()
    {
        OrderIntakeResult result = new()
        {
            IsValid = false,
            CustomerName = new OrderCustomerName
            {
                FirstName = "Jane",
                LastName = "Doe"
            },
            Order = null,
            ErrorMessage = "FlavorId XXX is not a valid product."
        };

        Assert.False(result.IsValid);
        Assert.Null(result.Order);
        Assert.NotNull(result.CustomerName);
        Assert.Equal("Jane", result.CustomerName.FirstName);
        Assert.Equal("Doe", result.CustomerName.LastName);
    }

    [Fact]
    public void WhenNoCustomerNameCanBeParsed_ThenCustomerNameIsNull()
    {
        OrderIntakeResult result = new()
        {
            IsValid = false,
            ErrorMessage = "No customer information was provided."
        };

        Assert.Null(result.CustomerName);
    }
}
