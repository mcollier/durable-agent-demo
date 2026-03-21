using DurableAgent.Functions.Services;

namespace DurableAgent.Functions.Tests.Services;

public class InventoryRepositoryTests
{
    [Fact]
    public void WhenFlavorIdProvided_ThenReturnsTubSku()
    {
        var sku = InventoryRepository.GetSkuForFlavorId("vne");

        Assert.Equal("VNE-TUB", sku);
    }

    [Fact]
    public void WhenCanonicalFlavorIdProvided_ThenReturnsInventoryQuantity()
    {
        var quantity = InventoryRepository.GetAvailableQuantityForFlavorId("VNE");

        Assert.Equal(15, quantity);
    }

    [Fact]
    public void WhenInventoryOnlySkuWasRemoved_ThenQuantityIsZero()
    {
        var quantity = InventoryRepository.GetAvailableQuantity("QCC-TUB");

        Assert.Equal(0, quantity);
    }
}
