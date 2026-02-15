using System.Text.Json;
using DurableAgent.Functions.Tools;

namespace DurableAgent.Functions.Tests.Tools;

public class ListFlavorsToolTests
{
    [Fact]
    public void WhenCalled_ThenReturnsValidJsonArray()
    {
        var result = ListFlavorsTool.ListFlavors();

        var doc = JsonDocument.Parse(result);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
    }

    [Fact]
    public void WhenCalled_ThenReturnsTenFlavors()
    {
        var result = ListFlavorsTool.ListFlavors();

        var doc = JsonDocument.Parse(result);
        Assert.Equal(10, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void WhenCalled_ThenEachFlavorHasRequiredProperties()
    {
        var result = ListFlavorsTool.ListFlavors();

        var doc = JsonDocument.Parse(result);
        foreach (var flavor in doc.RootElement.EnumerateArray())
        {
            Assert.True(flavor.TryGetProperty("flavorId", out _));
            Assert.True(flavor.TryGetProperty("name", out _));
            Assert.True(flavor.TryGetProperty("category", out _));
            Assert.True(flavor.TryGetProperty("containsDairy", out _));
            Assert.True(flavor.TryGetProperty("containsNuts", out _));
            Assert.True(flavor.TryGetProperty("description", out _));
        }
    }

    [Fact]
    public void WhenCalled_ThenFlavorIdsAreUnique()
    {
        var result = ListFlavorsTool.ListFlavors();

        var doc = JsonDocument.Parse(result);
        var ids = doc.RootElement.EnumerateArray()
            .Select(f => f.GetProperty("flavorId").GetString())
            .ToList();

        Assert.Equal(ids.Count, ids.Distinct().Count());
    }
}
