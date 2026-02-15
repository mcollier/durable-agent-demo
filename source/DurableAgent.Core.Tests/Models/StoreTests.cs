using DurableAgent.Core.Models;

namespace DurableAgent.Core.Tests.Models;

public class StoreTests
{
    [Fact]
    public void WhenCreatedWithRequiredProperties_ThenPropertiesAreSet()
    {
        var store = CreateTestStore();

        Assert.Equal("store-001", store.StoreId);
        Assert.Equal("Froyo Foundry - Hilliard", store.Name);
        Assert.Equal("+1-614-555-0101", store.Phone);
        Assert.Equal("hilliard@froyofoundry.com", store.Email);
        Assert.Equal("America/New_York", store.Timezone);
        Assert.Equal(new DateOnly(2022, 6, 15), store.OpenedDate);
    }

    [Fact]
    public void WhenStoreAddressCreated_ThenPropertiesAreSet()
    {
        var address = CreateTestAddress();

        Assert.Equal("4182 Main St", address.Street);
        Assert.Equal("Hilliard", address.City);
        Assert.Equal("OH", address.State);
        Assert.Equal("43026", address.PostalCode);
    }

    [Fact]
    public void WhenStoreManagerCreated_ThenPropertiesAreSet()
    {
        var manager = CreateTestManager();

        Assert.Equal("Emma Rodriguez", manager.Name);
        Assert.Equal("emma.rodriguez@froyofoundry.com", manager.Email);
    }

    [Fact]
    public void WhenTwoStoresHaveSameValues_ThenTheyAreEqual()
    {
        var a = CreateTestStore();
        var b = CreateTestStore();

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoStoresHaveDifferentIds_ThenTheyAreNotEqual()
    {
        var a = CreateTestStore();
        var b = CreateTestStore() with { StoreId = "store-999" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WhenTwoAddressesHaveSameValues_ThenTheyAreEqual()
    {
        var a = CreateTestAddress();
        var b = CreateTestAddress();

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoAddressesHaveDifferentStreets_ThenTheyAreNotEqual()
    {
        var a = CreateTestAddress();
        var b = CreateTestAddress() with { Street = "999 Other St" };

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void WhenTwoManagersHaveSameValues_ThenTheyAreEqual()
    {
        var a = CreateTestManager();
        var b = CreateTestManager();

        Assert.Equal(a, b);
    }

    [Fact]
    public void WhenTwoManagersHaveDifferentNames_ThenTheyAreNotEqual()
    {
        var a = CreateTestManager();
        var b = CreateTestManager() with { Name = "Other Person" };

        Assert.NotEqual(a, b);
    }

    private static StoreAddress CreateTestAddress() => new()
    {
        Street = "4182 Main St",
        City = "Hilliard",
        State = "OH",
        PostalCode = "43026"
    };

    private static StoreManager CreateTestManager() => new()
    {
        Name = "Emma Rodriguez",
        Email = "emma.rodriguez@froyofoundry.com"
    };

    private static Store CreateTestStore() => new()
    {
        StoreId = "store-001",
        Name = "Froyo Foundry - Hilliard",
        Address = CreateTestAddress(),
        Phone = "+1-614-555-0101",
        Email = "hilliard@froyofoundry.com",
        Manager = CreateTestManager(),
        Timezone = "America/New_York",
        OpenedDate = new DateOnly(2022, 6, 15)
    };
}
