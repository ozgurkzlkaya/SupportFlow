using System.Text;

namespace SupportFlow.Api.Services;

public static class TaxonomyNameNormalizer
{
    public static string Normalize(string name) => name
        .Trim()
        .Normalize(NormalizationForm.FormKC)
        .ToUpperInvariant();
}
