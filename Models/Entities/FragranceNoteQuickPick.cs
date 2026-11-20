using Stockify.Models.Enums;

namespace Stockify.Models.Entities;

public class FragranceNoteQuickPick
{
    public int FragranceNoteId { get; set; }
    public FragranceNote FragranceNote { get; set; } = null!;

    public QuickPickContext Context { get; set; }

    public int DisplayOrder { get; set; } = 0;
}
