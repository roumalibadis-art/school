namespace USTHBStudy.UnitTests.Classification;

using FluentAssertions;
using USTHBStudy.Application.Classification;
using USTHBStudy.Domain.Classification;

public class TaxonomyTextTests
{
    [Theory]
    [InlineData("  Génie   Logiciel ", "Génie Logiciel")]
    [InlineData("Intelligence\tArtificielle\n", "Intelligence Artificielle")]
    [InlineData("A\u0000B  C", "AB C")]
    public void Normalize_collapses_whitespace_and_strips_control_characters(string input, string expected) =>
        TaxonomyText.Normalize(input).Should().Be(expected);

    [Fact]
    public void Key_ignores_case_accents_punctuation_and_spacing()
    {
        TaxonomyText.Key("Génie  Logiciel").Should().Be(TaxonomyText.Key("genie-logiciel"));
        TaxonomyText.Key("L'Informatique").Should().Be("linformatique");
        TaxonomyText.Key("Électronique").Should().Be("electronique");
    }

    [Fact]
    public void Key_keeps_non_latin_letters_so_arabic_values_do_not_collapse_to_empty()
    {
        TaxonomyText.Key("علوم الحاسوب").Should().NotBeEmpty();
        TaxonomyText.Key("علوم الحاسوب").Should().NotBe(TaxonomyText.Key("الرياضيات"));
    }

    [Theory]
    [InlineData(ProposalCategory.Specialty, "x", false)]
    [InlineData(ProposalCategory.Specialty, "ok", true)]
    [InlineData(ProposalCategory.Specialty, "<b>bold</b>", false)]
    [InlineData(ProposalCategory.Specialty, "a{b}", false)]
    [InlineData(ProposalCategory.Specialty, "--", false)]
    [InlineData(ProposalCategory.AcademicYear, "2024-2025", true)]
    [InlineData(ProposalCategory.AcademicYear, "2024/2025", true)]
    [InlineData(ProposalCategory.AcademicYear, "2024", true)]
    [InlineData(ProposalCategory.AcademicYear, "2024-2026", false)]
    [InlineData(ProposalCategory.AcademicYear, "1800-1801", false)]
    [InlineData(ProposalCategory.AcademicYear, "Spring", false)]
    public void Validate_applies_category_rules(ProposalCategory category, string value, bool valid) =>
        (TaxonomyText.Validate(category, TaxonomyText.Normalize(value)) is null).Should().Be(valid);

    [Fact]
    public void Validate_rejects_overlong_values()
    {
        TaxonomyText.Validate(ProposalCategory.Specialty, new string('a', TaxonomyText.MaxLength + 1)).Should().NotBeNull();
        TaxonomyText.Validate(ProposalCategory.Specialty, new string('a', TaxonomyText.MaxLength)).Should().BeNull();
    }

    [Theory]
    [InlineData("2024-2025", 2024, 2025)]
    [InlineData("2024 / 2025", 2024, 2025)]
    [InlineData("2031", 2031, 2032)]
    public void Academic_years_parse_to_consecutive_years(string text, int start, int end)
    {
        TaxonomyText.TryParseAcademicYear(text, out var s, out var e).Should().BeTrue();
        (s, e).Should().Be((start, end));
    }

    [Theory]
    [InlineData("intelligenceartificielle", "intelligenceartificiele", true)]
    [InlineData("informatique", "informatiquegenerale", true)]
    [InlineData("mathematiques", "physique", false)]
    [InlineData("tp", "td", true)]
    [InlineData("", "abc", false)]
    public void Similarity_flags_typos_and_containment_but_not_unrelated_values(string a, string b, bool similar) =>
        TaxonomyText.AreSimilar(a, b).Should().Be(similar);

    [Fact]
    public void Levenshtein_distance_is_correct() =>
        TaxonomyText.Levenshtein("kitten", "sitting").Should().Be(3);
}
