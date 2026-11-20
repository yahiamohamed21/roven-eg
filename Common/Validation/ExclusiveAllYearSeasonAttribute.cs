using System.ComponentModel.DataAnnotations;
using Stockify.Models.Enums;

namespace Stockify.Common.Validation;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter, AllowMultiple = false)]
public class ExclusiveAllYearSeasonAttribute : ValidationAttribute
{
    public override bool IsValid(object? value)
    {
        if (value is not IEnumerable<FragranceSeason> seasons)
        {
            return true;
        }

        var list = seasons.ToList();
        if (list.Contains(FragranceSeason.AllYear) && list.Count > 1)
        {
            return false;
        }

        return true;
    }

    public override string FormatErrorMessage(string name)
    {
        return "When 'All Year' (طوال العام) is selected, individual seasons (Spring, Summer, Fall, Winter) cannot be combined. Please select either 'All Year' or specific individual seasons.";
    }
}

public static class FragranceSeasonValidator
{
    public static bool ValidateSeasons(IEnumerable<FragranceSeason>? seasons, out string? errorMessage)
    {
        var list = seasons?.ToList() ?? new List<FragranceSeason>();
        if (list.Contains(FragranceSeason.AllYear) && list.Count > 1)
        {
            errorMessage = "When 'All Year' (طوال العام) is selected, individual seasons (Spring, Summer, Fall, Winter) cannot be combined.";
            return false;
        }
        errorMessage = null;
        return true;
    }
}
