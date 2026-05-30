namespace LegendsOfWarAndMagic.Game.World.Domain.Settlements
{
    public enum SettlementTier
    {
        Camp,
        Hamlet,
        Village,
        Town,
        City,
        Capital
    }

    public enum SettlementDistrictType
    {
        Core,
        Residential,
        Market,
        Craft,
        Military,
        Sacred,
        Rural,
        Walls
    }

    public enum BuildingType
    {
        Tent,
        House,
        Longhouse,
        Storage,
        Well,
        Campfire,
        Blacksmith,
        Tavern,
        Market,
        Temple,
        Barracks,
        Watchtower,
        Wall,
        Gate,
        TownHall,
        Farm,
        Stable,
        Workshop,
        Shrine
    }

    public enum BuildingTag
    {
        Residential,
        Service,
        Military,
        Sacred,
        Economy,
        Resource,
        Defense,
        Temporary,
        Civic,
        Rural
    }

    public enum SettlementService
    {
        Rest,
        Trade,
        Repair,
        Crafting,
        Healing,
        Training,
        Storage,
        Rumors,
        Governance
    }
}
