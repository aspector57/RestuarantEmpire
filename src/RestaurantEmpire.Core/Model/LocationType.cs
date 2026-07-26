namespace RestaurantEmpire.Core.Model
{
    /// <summary>
    /// What kind of location a Restaurant is.
    ///
    /// Architecture Rule 5: this is a PARAMETER on the single Restaurant object, not a
    /// class hierarchy. A food truck is a Restaurant with tighter capacity constraints,
    /// not a different type — so Kitchen, Employees and Recipes all work uniformly
    /// across every location type without special-casing.
    /// </summary>
    public enum LocationType
    {
        BrickAndMortar = 0,
        FoodTruck = 1,
        GhostKitchen = 2,
        DeliveryOnly = 3
    }
}
