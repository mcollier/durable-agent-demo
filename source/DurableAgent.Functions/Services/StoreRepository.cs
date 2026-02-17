using DurableAgent.Core.Models;

namespace DurableAgent.Functions.Services;

/// <summary>
/// Central repository for managing Froyo Foundry store data.
/// Provides a single source of truth for all store information across the application.
/// </summary>
public static class StoreRepository
{
    /// <summary>
    /// Gets all available store locations.
    /// </summary>
    public static IReadOnlyList<Store> GetAllStores() => Array.AsReadOnly(Stores);

    /// <summary>
    /// Gets a specific store by its ID.
    /// </summary>
    /// <param name="storeId">The unique identifier of the store.</param>
    /// <returns>The store if found, otherwise null.</returns>
    public static Store? GetStoreById(string storeId)
    {
        return Array.Find(Stores, s => s.StoreId.Equals(storeId, StringComparison.OrdinalIgnoreCase));
    }

    private static readonly Store[] Stores =
    [
        new() { StoreId = "store-001", Name = "Froyo Foundry - Hilliard", Address = new() { Street = "4182 Main St", City = "Hilliard", State = "OH", PostalCode = "43026" }, Phone = "+1-614-555-0101", Email = "hilliard@froyofoundry.com", Manager = new() { Name = "Emma Rodriguez", Email = "emma.rodriguez@froyofoundry.com" }, Timezone = "America/New_York", OpenedDate = new(2022, 6, 15) },
        new() { StoreId = "store-002", Name = "Froyo Foundry - Dublin", Address = new() { Street = "6750 Perimeter Loop Rd", City = "Dublin", State = "OH", PostalCode = "43017" }, Phone = "+1-614-555-0102", Email = "dublin@froyofoundry.com", Manager = new() { Name = "Marcus Chen", Email = "marcus.chen@froyofoundry.com" }, Timezone = "America/New_York", OpenedDate = new(2021, 9, 3) },
        new() { StoreId = "store-003", Name = "Froyo Foundry - Easton", Address = new() { Street = "4001 Easton Station", City = "Columbus", State = "OH", PostalCode = "43219" }, Phone = "+1-614-555-0103", Email = "easton@froyofoundry.com", Manager = new() { Name = "Priya Patel", Email = "priya.patel@froyofoundry.com" }, Timezone = "America/New_York", OpenedDate = new(2023, 3, 22) },
        new() { StoreId = "store-004", Name = "Froyo Foundry - Short North", Address = new() { Street = "1122 N High St", City = "Columbus", State = "OH", PostalCode = "43201" }, Phone = "+1-614-555-0104", Email = "shortnorth@froyofoundry.com", Manager = new() { Name = "Daniel Kim", Email = "daniel.kim@froyofoundry.com" }, Timezone = "America/New_York", OpenedDate = new(2020, 11, 10) },
        new() { StoreId = "store-005", Name = "Froyo Foundry - Polaris", Address = new() { Street = "1500 Polaris Pkwy", City = "Columbus", State = "OH", PostalCode = "43240" }, Phone = "+1-614-555-0105", Email = "polaris@froyofoundry.com", Manager = new() { Name = "Olivia Martinez", Email = "olivia.martinez@froyofoundry.com" }, Timezone = "America/New_York", OpenedDate = new(2024, 1, 18) },
    ];
}
