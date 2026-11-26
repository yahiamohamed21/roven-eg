using System.Text.Json;

namespace Stockify.Helpers;

public static class AbandonedCheckoutHelper
{
    public static List<string> ParseCartItems(string? cartItemsJson, int fallbackItemCount)
    {
        var productsList = new List<string>();
        if (string.IsNullOrWhiteSpace(cartItemsJson))
        {
            productsList.Add(fallbackItemCount > 1 ? $"{fallbackItemCount} منتجات" : "عطور Roven");
            return productsList;
        }

        try
        {
            using var doc = JsonDocument.Parse(cartItemsJson);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var pName = el.TryGetProperty("ProductName", out var nameProp)
                    ? (nameProp.GetString() ?? "Fragrance")
                    : "Fragrance";
                var pQty = el.TryGetProperty("Quantity", out var qtyProp) && qtyProp.TryGetInt32(out var q)
                    ? q
                    : 1;
                var sizeLabel = el.TryGetProperty("SizeLabel", out var sizeProp)
                    ? sizeProp.GetString()
                    : null;
                var testersSummary = el.TryGetProperty("SelectedTestersSummary", out var testersProp)
                    ? testersProp.GetString()
                    : null;

                var label = string.IsNullOrWhiteSpace(sizeLabel)
                    ? pName
                    : $"{pName} ({sizeLabel})";

                if (!string.IsNullOrWhiteSpace(testersSummary))
                {
                    label += $" [{testersSummary}]";
                }

                productsList.Add($"{label} (×{pQty})");
            }
        }
        catch
        {
            productsList.Add(fallbackItemCount > 1 ? $"{fallbackItemCount} منتجات" : "عطور Roven");
        }

        if (productsList.Count == 0)
        {
            productsList.Add(fallbackItemCount > 1 ? $"{fallbackItemCount} منتجات" : "عطور Roven");
        }

        return productsList;
    }

    public static string BuildRecoveryWhatsAppUrl(string waPhone, string customerName, string? cartItemsJson, int fallbackItemCount)
    {
        var items = ParseCartItems(cartItemsJson, fallbackItemCount);
        var cleanName = string.IsNullOrWhiteSpace(customerName) ? "عميلنا العزيز" : customerName.Trim();

        string msg;
        if (items.Count <= 1)
        {
            msg = $"أهلاً بك أ/ {cleanName} في دار Roven للعطور، لاحظنا أنك لم تتمكن من استكمال طلبك لـ ({items[0]})، هل واجهتك أي مشكلة في إتمام الطلب؟ يسعدنا مساعدتك وتوفير كود خصم خاص لك!";
        }
        else
        {
            var bullets = string.Join("\n", items.Select(i => $"• {i}"));
            msg = $"أهلاً بك أ/ {cleanName} في دار Roven للعطور، لاحظنا أنك لم تتمكن من استكمال طلبك للمنتجات التالية:\n{bullets}\nهل واجهتك أي مشكلة في إتمام الطلب؟ يسعدنا مساعدتك وتوفير كود خصم خاص لك!";
        }

        return $"https://wa.me/{waPhone}?text={Uri.EscapeDataString(msg)}";
    }
}
