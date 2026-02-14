using DurableAgent.Core.Models;

namespace DurableAgent.Core.Tests.Models;

public class CustomerInfoTests
{
    [Fact]
    public void WhenCreatedWithRequiredProperties_ThenPropertiesAreSet()
    {
        var customer = new CustomerInfo
        {
            PreferredName = "Aidan",
            FirstName = "Aidan",
            LastName = "Smith",
            Email = "aidan@example.com",
            PhoneNumber = "555-0100",
            PreferredContactMethod = ContactMethod.Email
        };

        Assert.Equal("Aidan", customer.PreferredName);
        Assert.Equal("Aidan", customer.FirstName);
        Assert.Equal("Smith", customer.LastName);
        Assert.Equal("aidan@example.com", customer.Email);
        Assert.Equal("555-0100", customer.PhoneNumber);
        Assert.Equal(ContactMethod.Email, customer.PreferredContactMethod);
    }

    [Fact]
    public void WhenTwoCustomersHaveSameValues_ThenTheyAreEqual()
    {
        var a = new CustomerInfo
        {
            PreferredName = "Aidan",
            FirstName = "Aidan",
            LastName = "Smith",
            Email = "aidan@example.com",
            PhoneNumber = "555-0100",
            PreferredContactMethod = ContactMethod.Phone
        };

        var b = new CustomerInfo
        {
            PreferredName = "Aidan",
            FirstName = "Aidan",
            LastName = "Smith",
            Email = "aidan@example.com",
            PhoneNumber = "555-0100",
            PreferredContactMethod = ContactMethod.Phone
        };

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoCustomersHaveDifferentEmails_ThenTheyAreNotEqual()
    {
        var a = new CustomerInfo
        {
            PreferredName = "Aidan",
            FirstName = "Aidan",
            LastName = "Smith",
            Email = "aidan@example.com",
            PhoneNumber = "555-0100",
            PreferredContactMethod = ContactMethod.Email
        };

        var b = new CustomerInfo
        {
            PreferredName = "Aidan",
            FirstName = "Aidan",
            LastName = "Smith",
            Email = "different@example.com",
            PhoneNumber = "555-0100",
            PreferredContactMethod = ContactMethod.Email
        };

        Assert.NotEqual(a, b);
    }

    [Theory]
    [InlineData(ContactMethod.Email)]
    [InlineData(ContactMethod.Phone)]
    public void WhenPreferredContactMethodSet_ThenValueIsPreserved(ContactMethod method)
    {
        var customer = new CustomerInfo
        {
            PreferredName = "Test",
            FirstName = "Test",
            LastName = "User",
            Email = "test@example.com",
            PhoneNumber = "555-0000",
            PreferredContactMethod = method
        };

        Assert.Equal(method, customer.PreferredContactMethod);
    }
}
