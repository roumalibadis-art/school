namespace USTHBStudy.UnitTests.Auth;

using FluentAssertions;
using FluentValidation.TestHelper;
using USTHBStudy.Application.Auth.Dtos;
using USTHBStudy.Application.Auth.Validators;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    private static RegisterRequest Valid() => new(
        "sami@example.local", "passw0rd", "passw0rd", "Sami", "Student");

    [Fact]
    public void Accepts_a_well_formed_request()
    {
        _validator.TestValidate(Valid()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Rejects_bad_email(string email)
    {
        _validator.TestValidate(Valid() with { Email = email })
            .ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public void Rejects_short_password()
    {
        _validator.TestValidate(Valid() with { Password = "a1", ConfirmPassword = "a1" })
            .ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Rejects_password_without_a_digit()
    {
        _validator.TestValidate(Valid() with { Password = "onlyletters", ConfirmPassword = "onlyletters" })
            .ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Rejects_mismatched_confirmation()
    {
        _validator.TestValidate(Valid() with { ConfirmPassword = "different1" })
            .ShouldHaveValidationErrorFor(x => x.ConfirmPassword);
    }

    [Fact]
    public void Rejects_missing_names()
    {
        var result = _validator.TestValidate(Valid() with { FirstName = "", LastName = "" });

        result.ShouldHaveValidationErrorFor(x => x.FirstName);
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }
}
