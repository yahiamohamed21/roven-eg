namespace Stockify.Models.Enums;

public enum AudienceTarget
{
    ForHim = 1,
    ForHer = 2,
    Unisex = 3
}

public enum FragranceSeason
{
    AllYear = 0,
    Spring = 1,
    Summer = 2,
    Fall = 3,
    Winter = 4
}

public enum FragranceProjection
{
    Moderate = 1, // Soft landing
    Strong = 2,   // Noticeable
    Enormous = 3  // Fills the room
}

public enum FragranceNoteCategory
{
    Citrus = 1,
    Floral = 2,
    Woody = 3,
    OrientalAmber = 4,
    FreshAromatic = 5,
    GourmandSweet = 6
}

public enum QuickPickContext
{
    Love = 1,  // Step 2: Notes You Love Quick Picks
    Avoid = 2  // Step 3: Notes to Avoid Quick Picks
}
