using System.Text.Json;
using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Tests.Tools;

public class GetStoreDetailsToolTests
{
    [Theory]
    [InlineData("store-001", "Froyo Foundry - Hilliard")]
    [InlineData("store-002", "Froyo Foundry - Dublin")]
    [InlineData("store-003", "Froyo Foundry - Easton")]
    [InlineData("store-004", "Froyo Foundry - Short North")]
    [InlineData("store-005", "Froyo Foundry - Polaris")]
    public void WhenKnownStoreId_ThenReturnsCorrectStoreName(string storeId, string expectedName)
    {
        var result = GetStoreDetailsTool.GetStoreDetails(storeId);

        var doc = JsonDocument.Parse(result);
        Assert.Equal(expectedName, doc.RootElement.GetProperty("name").GetString());
    }

    [Fact]
    public void WhenUnknownStoreId_ThenReturnsErrorJson()
    {
        var result = GetStoreDetailsTool.GetStoreDetails("store-999");

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("error", out var errorProp));
        Assert.Equal("Store not found", errorProp.GetString());
        Assert.Equal("store-999", doc.RootElement.GetProperty("storeId").GetString());
    }

    [Theory]
    [InlineData("STORE-001")]
    [InlineData("Store-001")]
    public void WhenStoreIdDifferentCase_ThenFindsStore(string storeId)
    {
        var result = GetStoreDetailsTool.GetStoreDetails(storeId);

        var doc = JsonDocument.Parse(result);
        Assert.True(doc.RootElement.TryGetProperty("name", out _));
        Assert.False(doc.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public void WhenKnownStore_ThenReturnsAllRequiredFields()
    {
        var result = GetStoreDetailsTool.GetStoreDetails("store-001");

        var doc = JsonDocument.Parse(result);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("storeId", out _));
        Assert.True(root.TryGetProperty("name", out _));
        Assert.True(root.TryGetProperty("address", out _));
        Assert.True(root.TryGetProperty("phone", out _));
        Assert.True(root.TryGetProperty("email", out _));
        Assert.True(root.TryGetProperty("manager", out _));
        Assert.True(root.TryGetProperty("timezone", out _));
        Assert.True(root.TryGetProperty("openedDate", out _));
    }
}
