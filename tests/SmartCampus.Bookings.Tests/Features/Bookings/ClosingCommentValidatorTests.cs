using SmartCampus.Bookings.Api.Features.Bookings.Closure;

namespace SmartCampus.Bookings.Tests.Features.Bookings;

public class ClosingCommentValidatorTests
{
    private readonly ClosingCommentValidator _validator = new();

    [Fact]
    public void Accepts_valid_comment()
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 15,
            IssuesReported: false,
            Comment: "Aula pulita e proiettore funzionante.");

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.Empty(errors);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_empty_or_whitespace_comment(string? comment)
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 1,
            IssuesReported: false,
            Comment: comment);

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.True(errors.ContainsKey("Comment"));
        Assert.Equal("The comment is required.", errors["Comment"][0]);
    }

    [Fact]
    public void Rejects_comment_shorter_than_10_characters()
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 1,
            IssuesReported: false,
            Comment: "123456789");

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.True(errors.ContainsKey("Comment"));
        Assert.Contains("between 10 and 500 characters", errors["Comment"][0]);
    }

    [Fact]
    public void Accepts_comment_of_exactly_10_characters()
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 1,
            IssuesReported: false,
            Comment: "1234567890");

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.Empty(errors);
    }

    [Fact]
    public void Accepts_comment_of_exactly_500_characters()
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 1,
            IssuesReported: false,
            Comment: new string('a', 500));

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.Empty(errors);
    }

    [Fact]
    public void Rejects_comment_longer_than_500_characters()
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 1,
            IssuesReported: false,
            Comment: new string('a', 501));

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.True(errors.ContainsKey("Comment"));
        Assert.Contains("between 10 and 500 characters", errors["Comment"][0]);
    }

    [Fact]
    public void Rejects_negative_attendees_count()
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: -1,
            IssuesReported: false,
            Comment: "Aula usata regolarmente per studio.");

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.True(errors.ContainsKey("AttendeesCount"));
        Assert.Equal("The attendees count cannot be negative.", errors["AttendeesCount"][0]);
    }

    [Fact]
    public void Rejects_attendees_exceeding_capacity()
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 31,
            IssuesReported: false,
            Comment: "Aula usata regolarmente per studio.");

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.True(errors.ContainsKey("AttendeesCount"));
        Assert.Contains("cannot exceed the space capacity (30)", errors["AttendeesCount"][0]);
    }

    [Fact]
    public void Accepts_attendees_equal_to_capacity()
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: true,
            AttendeesCount: 30,
            IssuesReported: false,
            Comment: "Aula al completo per una conferenza.");

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.Empty(errors);
    }

    [Fact]
    public void Rejects_positive_attendees_when_space_not_used()
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: false,
            AttendeesCount: 5,
            IssuesReported: false,
            Comment: "Non ci siamo presentati per un imprevisto.");

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.True(errors.ContainsKey("AttendeesCount"));
        Assert.Equal("The attendees count must be 0 if the space was not used.", errors["AttendeesCount"][0]);
    }

    [Fact]
    public void Accepts_zero_attendees_when_space_not_used()
    {
        var request = new SubmitClosingCommentRequest(
            SpaceUsed: false,
            AttendeesCount: 0,
            IssuesReported: false,
            Comment: "Non ci siamo presentati per un imprevisto.");

        var errors = _validator.Validate(request, spaceCapacity: 30);

        Assert.Empty(errors);
    }
}
