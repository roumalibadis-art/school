namespace USTHBStudy.UnitTests.Common;

using FluentAssertions;
using USTHBStudy.Application.Common;

public class SlugifierTests
{
    [Theory]
    [InlineData("Bases de données", "bases-de-donnees")]
    [InlineData("Algorithmique 3", "algorithmique-3")]
    [InlineData("  Analyse   Mathématique  ", "analyse-mathematique")]
    [InlineData("Faculté d'Informatique", "faculte-d-informatique")]
    [InlineData("Réseaux & Sécurité", "reseaux-securite")]
    [InlineData("L1 / S1", "l1-s1")]
    [InlineData("2024-2025", "2024-2025")]
    public void Slugify_folds_accents_and_collapses_separators(string input, string expected)
    {
        Slugifier.Slugify(input).Should().Be(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("!!!")]
    public void Slugify_returns_empty_for_content_free_input(string input)
    {
        Slugifier.Slugify(input).Should().BeEmpty();
    }

    [Fact]
    public void Slugify_truncates_to_max_length_without_trailing_hyphen()
    {
        var slug = Slugifier.Slugify(new string('a', 50) + " " + new string('b', 50), maxLength: 60);

        slug.Length.Should().BeLessThanOrEqualTo(60);
        slug.Should().NotEndWith("-");
    }
}
