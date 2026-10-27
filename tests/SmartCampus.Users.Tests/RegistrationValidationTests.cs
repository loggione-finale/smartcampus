using SmartCampus.Users.Api.Features.Registration;

namespace SmartCampus.Users.Tests;

public class RegistrationValidationTests
{
    private static RegisterRequest CreateValidRequest() => new(
        Matricola: "1234567890",
        FirstName: "Mario",
        LastName: "Rossi",
        Email: "mario.rossi@smartcampus.test",
        PhoneNumber: "+393331234567",
        Password: "Password123"
    );

    [Fact]
    public void Validate_WithValidRequest_ReturnsNoErrors()
    {
        var request = CreateValidRequest();
        var errors = RegisterValidator.Validate(request);
        Assert.Empty(errors);
    }

    [Fact]
    public void Validate_WithNullRequest_ReturnsBodyError()
    {
        var errors = RegisterValidator.Validate(null);
        Assert.True(errors.ContainsKey("Body"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")] // 5 digits - too short
    [InlineData("123456")] // 6 digits - now invalid (must be 10)
    [InlineData("12345678")] // 8 digits - now invalid (must be 10)
    [InlineData("123456789")] // 9 digits - too short
    [InlineData("12345678901")] // 11 digits - too long
    [InlineData("123456789a")] // contains letter
    [InlineData("1234567 89")] // contains space
    [InlineData("123-456789")] // contains hyphen
    public void Validate_WithInvalidMatricola_ReturnsError(string matricola)
    {
        var request = CreateValidRequest() with { Matricola = matricola };
        var errors = RegisterValidator.Validate(request);
        Assert.True(errors.ContainsKey(nameof(request.Matricola)));
        if (!string.IsNullOrWhiteSpace(matricola))
        {
            Assert.Contains("Matricola must be exactly 10 digits.", errors[nameof(request.Matricola)]);
        }
    }

    [Theory]
    [InlineData("1000000001")] // 10 digits
    [InlineData("0000000001")] // 10 digits with leading zeros
    [InlineData("1234567890")] // 10 digits
    public void Validate_WithValidMatricola_Passes(string matricola)
    {
        var request = CreateValidRequest() with { Matricola = matricola };
        var errors = RegisterValidator.Validate(request);
        Assert.False(errors.ContainsKey(nameof(request.Matricola)));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("A")] // 1 char - too short
    [InlineData("ThisNameIsWayTooLongAndExceedsTheFiftyCharacterLimitAllowedByValidation")] // > 50 chars
    [InlineData("Mario123")] // contains numbers
    [InlineData("Mario@Rossi")] // invalid symbol
    public void Validate_WithInvalidFirstName_ReturnsError(string firstName)
    {
        var request = CreateValidRequest() with { FirstName = firstName };
        var errors = RegisterValidator.Validate(request);
        Assert.True(errors.ContainsKey(nameof(request.FirstName)));
    }

    [Theory]
    [InlineData("Mario")]
    [InlineData("Jean-Luc")]
    [InlineData("O'Connor")]
    [InlineData("Maria Sole")]
    [InlineData("José")]
    public void Validate_WithValidFirstName_Passes(string firstName)
    {
        var request = CreateValidRequest() with { FirstName = firstName };
        var errors = RegisterValidator.Validate(request);
        Assert.False(errors.ContainsKey(nameof(request.FirstName)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("B")]
    [InlineData("Rossi123")]
    public void Validate_WithInvalidLastName_ReturnsError(string lastName)
    {
        var request = CreateValidRequest() with { LastName = lastName };
        var errors = RegisterValidator.Validate(request);
        Assert.True(errors.ContainsKey(nameof(request.LastName)));
    }

    [Theory]
    [InlineData("Rossi")]
    [InlineData("De Santis")]
    [InlineData("D'Amico")]
    public void Validate_WithValidLastName_Passes(string lastName)
    {
        var request = CreateValidRequest() with { LastName = lastName };
        var errors = RegisterValidator.Validate(request);
        Assert.False(errors.ContainsKey(nameof(request.LastName)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("plainaddress")]
    [InlineData("@missingusername.com")]
    [InlineData("username@.com")]
    [InlineData("username@domain")]
    public void Validate_WithInvalidEmail_ReturnsError(string email)
    {
        var request = CreateValidRequest() with { Email = email };
        var errors = RegisterValidator.Validate(request);
        Assert.True(errors.ContainsKey(nameof(request.Email)));
    }

    [Fact]
    public void Validate_WithEmailExceeding254Chars_ReturnsError()
    {
        var longEmail = new string('a', 245) + "@domain.com"; // > 254
        var request = CreateValidRequest() with { Email = longEmail };
        var errors = RegisterValidator.Validate(request);
        Assert.True(errors.ContainsKey(nameof(request.Email)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("3331234567")] // missing +39
    [InlineData("+3912345678")] // 8 digits (too short)
    [InlineData("+3912345678901")] // 11 digits (too long)
    [InlineData("+39123456789a")] // contains letter
    [InlineData("00391234567890")] // wrong prefix format
    public void Validate_WithInvalidPhoneNumber_ReturnsError(string phone)
    {
        var request = CreateValidRequest() with { PhoneNumber = phone };
        var errors = RegisterValidator.Validate(request);
        Assert.True(errors.ContainsKey(nameof(request.PhoneNumber)));
    }

    [Theory]
    [InlineData("+39333123456")] // 9 digits
    [InlineData("+393331234567")] // 10 digits
    public void Validate_WithValidPhoneNumber_Passes(string phone)
    {
        var request = CreateValidRequest() with { PhoneNumber = phone };
        var errors = RegisterValidator.Validate(request);
        Assert.False(errors.ContainsKey(nameof(request.PhoneNumber)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Pass1234")] // 8 chars - too short (< 10)
    [InlineData("abcdefghij")] // 10 chars, but no digit
    [InlineData("1234567890")] // 10 chars, but no letter
    public void Validate_WithInvalidPassword_ReturnsError(string password)
    {
        var request = CreateValidRequest() with { Password = password };
        var errors = RegisterValidator.Validate(request);
        Assert.True(errors.ContainsKey(nameof(request.Password)));
    }

    [Theory]
    [InlineData("Password10")] // 10 chars, letter + digit
    [InlineData("SecurePass2026!")]
    public void Validate_WithValidPassword_Passes(string password)
    {
        var request = CreateValidRequest() with { Password = password };
        var errors = RegisterValidator.Validate(request);
        Assert.False(errors.ContainsKey(nameof(request.Password)));
    }
}
