using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Time.Testing;
using NpgsqlTypes;
using SmartCampus.Auth;
using SmartCampus.Bookings.Api.Data;
using SmartCampus.Bookings.Api.Domain;
using SmartCampus.Bookings.Api.Features.Admin;
using SmartCampus.Bookings.Api.Features.Bookings;
using SmartCampus.Bookings.Api.Features.Bookings.Closure;
using SmartCampus.Bookings.Api.Features.Bookings.Spaces;
using SmartCampus.Bookings.Api.Features.Equipment;
using SmartCampus.Bookings.Api.Features.Notifications;
using SmartCampus.Bookings.Tests.Support;

namespace SmartCampus.Bookings.Tests.Features.Equipment;

public class EquipmentRequestValidationAndHttpTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 28, 8, 0, 0, TimeSpan.Zero);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Validator_Rejects_Single_Booking_With_Equipment_With_ValidationError()
    {
        var validator = new BookingRequestValidator(new FakeTimeProvider(Now));
        var start = new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

        var singleWithEquipment = new CreateBookingRequest(
            SpaceId: Guid.NewGuid(),
            Start: start,
            End: start.AddHours(1),
            ParticipantEmails: null,
            EquipmentRequests: [new BookingEquipmentItemInput("videoprojector")]);

        var errors = validator.Validate(singleWithEquipment, "giulia.rossi@smartcampus.test");

        Assert.Contains(nameof(CreateBookingRequest.EquipmentRequests), errors.Keys);
        Assert.Contains(
            "group",
            errors[nameof(CreateBookingRequest.EquipmentRequests)][0],
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validator_Accepts_Group_Booking_With_Valid_Equipment_And_Rejects_Other_Without_Notes()
    {
        var validator = new BookingRequestValidator(new FakeTimeProvider(Now));
        var start = new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);

        var validGroup = new CreateBookingRequest(
            SpaceId: Guid.NewGuid(),
            Start: start,
            End: start.AddHours(2),
            ParticipantEmails: ["marco.bianchi@smartcampus.test"],
            EquipmentRequests:
            [
                new BookingEquipmentItemInput("Videoproiettore"),
                new BookingEquipmentItemInput("Altro", "Serve adattatore HDMI")
            ]);

        Assert.Empty(validator.Validate(validGroup, "giulia.rossi@smartcampus.test"));

        var otherWithoutNotes = validGroup with
        {
            EquipmentRequests = [new BookingEquipmentItemInput("other", null)]
        };

        var errors = validator.Validate(otherWithoutNotes, "giulia.rossi@smartcampus.test");
        Assert.Contains(nameof(CreateBookingRequest.EquipmentRequests), errors.Keys);

        var unknownTool = validGroup with
        {
            EquipmentRequests = [new BookingEquipmentItemInput("macchina-caffe")]
        };
        Assert.Contains(
            nameof(CreateBookingRequest.EquipmentRequests),
            validator.Validate(unknownTool, "giulia.rossi@smartcampus.test").Keys);

        var organizerInParticipants = validGroup with
        {
            ParticipantEmails = ["GIULIA.ROSSI@smartcampus.test"]
        };
        Assert.Contains(
            nameof(CreateBookingRequest.ParticipantEmails),
            validator.Validate(organizerInParticipants, "giulia.rossi@smartcampus.test").Keys);
    }

    [Fact]
    public void JsonConverter_Deserializes_Both_String_Array_And_Object_Array()
    {
        const string payloadJson = """
        {
          "spaceId": "3f2a9c1e-0000-4000-8000-000000000001",
          "start": "2026-10-29T09:00:00Z",
          "end": "2026-10-29T11:00:00Z",
          "participantEmails": ["marco.bianchi@smartcampus.test"],
          "equipmentRequests": [
            "Videoproiettore",
            { "equipmentType": "other", "notes": "Serve cavo USB-C" }
          ]
        }
        """;

        var deserialized = JsonSerializer.Deserialize<CreateBookingRequest>(payloadJson, JsonOptions);

        Assert.NotNull(deserialized);
        Assert.NotNull(deserialized!.EquipmentRequests);
        Assert.Equal(2, deserialized.EquipmentRequests!.Count);
        Assert.Equal("Videoproiettore", deserialized.EquipmentRequests[0].EquipmentType);
        Assert.Null(deserialized.EquipmentRequests[0].Notes);
        Assert.Equal("other", deserialized.EquipmentRequests[1].EquipmentType);
        Assert.Equal("Serve cavo USB-C", deserialized.EquipmentRequests[1].Notes);

        const string apiResponseJson = """
        [
          {
            "id": "3f2a9c1e-0000-4000-8000-000000000010",
            "bookingId": "3f2a9c1e-0000-4000-8000-000000000020",
            "equipmentType": "videoprojector",
            "equipmentName": "Videoproiettore",
            "notes": null,
            "status": "Confirmed",
            "createdAt": "2026-10-29T09:00:00Z"
          },
          {
            "id": "3f2a9c1e-0000-4000-8000-000000000011",
            "bookingId": "3f2a9c1e-0000-4000-8000-000000000020",
            "equipmentType": "interactive-whiteboard",
            "equipmentName": "Lavagna interattiva",
            "notes": "Serve pennarello digitale",
            "status": "Pending",
            "createdAt": "2026-10-29T09:00:00Z"
          }
        ]
        """;

        var responseItems = JsonSerializer.Deserialize<List<EquipmentRequestResponse>>(apiResponseJson, JsonOptions);
        Assert.NotNull(responseItems);
        Assert.Equal(2, responseItems!.Count);
        Assert.Equal(EquipmentRequestStatus.Confirmed, responseItems[0].Status);
        Assert.Equal(EquipmentRequestStatus.Pending, responseItems[1].Status);
    }

    [Fact]
    public async Task PreparePlan_And_RaiseMissingEquipmentAlerts_Create_Exception_Only_For_Missing_Tools()
    {
        var spaceId = Guid.NewGuid();
        var fakeSpaceEquip = new FakeSpaceEquipmentClient
        {
            Behavior = _ => new HashSet<string>(["videoprojector"], StringComparer.OrdinalIgnoreCase)
        };
        var recordedAlerts = new List<(AdminAlertKind Kind, Guid BookingId, string Message)>();
        var spyAlerts = new SpyAdminAlerts(recordedAlerts);

        // No DB queries are executed in PreparePlanAsync / BuildEntities / RaiseMissingEquipmentAlertsAsync
        var dummyOptions = new DbContextOptionsBuilder<BookingsDbContext>().Options;
        await using var dummyContext = new BookingsDbContext(dummyOptions);

        var service = new BookingEquipmentService(
            dummyContext,
            fakeSpaceEquip,
            spyAlerts,
            new FakeTimeProvider(Now));

        Assert.True(EquipmentRequestValidator.TryValidateEquipmentRequests(
            [
                new BookingEquipmentItemInput("Videoproiettore"),
                new BookingEquipmentItemInput("Lavagna interattiva"),
                new BookingEquipmentItemInput("other", "Adattatore HDMI")
            ],
            isGroupBooking: true,
            requireAtLeastOneItem: true,
            out var normalized,
            out _));

        var plan = await service.PreparePlanAsync(spaceId, normalized);
        Assert.NotNull(plan);

        var booking = new Booking
        {
            Id = Guid.NewGuid(),
            ReferenceCode = "SC-261029-ABCD",
            SpaceId = spaceId,
            SpaceNameSnapshot = "Aula 101",
            SpaceTypeSnapshot = "Classroom",
            SpaceBuildingSnapshot = "Edificio A",
            Period = new NpgsqlRange<DateTime>(Now.UtcDateTime.AddHours(1), true, Now.UtcDateTime.AddHours(3), false),
            IsGroup = true,
            SeatsRequested = 3,
            Status = BookingStatus.Confirmed
        };

        var entities = service.BuildEntities(booking.Id, plan!, Now.UtcDateTime);
        Assert.Equal(3, entities.Count);

        // Available in room -> Confirmed
        Assert.Equal("videoprojector", entities[0].EquipmentType);
        Assert.Equal(EquipmentRequestStatus.Confirmed, entities[0].Status);

        // Missing from room -> Pending
        Assert.Equal("interactive-whiteboard", entities[1].EquipmentType);
        Assert.Equal(EquipmentRequestStatus.Pending, entities[1].Status);

        // 'other' -> Pending with notes
        Assert.Equal("other", entities[2].EquipmentType);
        Assert.Equal("Adattatore HDMI", entities[2].Notes);
        Assert.Equal(EquipmentRequestStatus.Pending, entities[2].Status);

        await service.RaiseMissingEquipmentAlertsAsync(booking, plan!);

        // Exactly 2 admin exceptions raised (for interactive-whiteboard and other, NOT for videoprojector)
        Assert.Equal(2, recordedAlerts.Count);
        Assert.All(recordedAlerts, a =>
        {
            Assert.Equal(AdminAlertKind.EquipmentRequest, a.Kind);
            Assert.Equal(booking.Id, a.BookingId);
            Assert.Contains("SC-261029-ABCD", a.Message);
        });
        Assert.Contains(recordedAlerts, a => a.Message.Contains("Lavagna interattiva"));
        Assert.Contains(recordedAlerts, a => a.Message.Contains("Altro") && a.Message.Contains("Adattatore HDMI"));
    }

    [Fact]
    public async Task SpaceEquipmentClient_NormalizesResponse_ReturnsNullOn404_AndThrowsOn503()
    {
        var existingId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        var failingId = Guid.NewGuid();

        using var httpClient = new HttpClient(new StubHttpMessageHandler(req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith(existingId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"id":"1","equipment":["Videoproiettore","audio-kit"]}""",
                        Encoding.UTF8,
                        "application/json")
                };
            }

            if (path.EndsWith(missingId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        }))
        {
            BaseAddress = new Uri("http://spaces.test/")
        };

        var client = new SpaceEquipmentClient(httpClient);

        var codes = await client.GetSpaceEquipmentCodesAsync(existingId);
        Assert.NotNull(codes);
        Assert.Contains("videoprojector", codes!);
        Assert.Contains("audio-kit", codes!);

        Assert.Null(await client.GetSpaceEquipmentCodesAsync(missingId));

        await Assert.ThrowsAsync<SpacesUnavailableException>(() =>
            client.GetSpaceEquipmentCodesAsync(failingId));
    }

    [Fact]
    public async Task BookingService_CreateAsync_Rejects_Single_Booking_With_Equipment_Before_Touching_Database()
    {
        var timeProvider = new FakeTimeProvider(Now);
        var dummyOptions = new DbContextOptionsBuilder<BookingsDbContext>().Options;
        await using var dummyContext = new BookingsDbContext(dummyOptions);

        var bookingService = new BookingService(
            dummyContext,
            new BookingRequestValidator(timeProvider),
            new FakeSpacesClient(),
            new NoPendingClosureChecker(),
            timeProvider,
            new StubEmailOutbox(),
            bookingEquipmentService: new StubBookingEquipmentService());

        var start = new DateTimeOffset(2026, 10, 29, 9, 0, 0, TimeSpan.Zero);
        var result = await bookingService.CreateAsync(
            Guid.NewGuid(),
            "giulia.rossi@smartcampus.test",
            "giulia.rossi@smartcampus.test",
            new CreateBookingRequest(
                Guid.NewGuid(),
                start,
                start.AddHours(1),
                ParticipantEmails: null,
                EquipmentRequests: [new BookingEquipmentItemInput("videoprojector")]),
            CancellationToken.None);

        Assert.Equal(CreateBookingOutcome.Invalid, result.Outcome);
        Assert.NotNull(result.ValidationErrors);
        Assert.Contains("EquipmentRequests", result.ValidationErrors!.Keys);
    }

    [Fact]
    public async Task EquipmentController_EnforcesAuth_ForbidsAdminRequesting_AndMapsOutcomes()
    {
        var stubService = new StubBookingEquipmentService();
        var bookingId = Guid.NewGuid();

        // 1. Missing valid userId claim -> 401 Unauthorized
        var unauthController = CreateController(stubService, new ClaimsPrincipal(new ClaimsIdentity()));
        Assert.IsType<UnauthorizedResult>(await unauthController.List(bookingId, CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await unauthController.Create(
            bookingId,
            new SubmitEquipmentRequestsPayload([new BookingEquipmentItemInput("videoprojector")]),
            CancellationToken.None));

        // 2. Admin cannot create equipment requests -> 403 Forbidden
        var adminController = CreateController(
            stubService,
            CreatePrincipal(Guid.NewGuid(), "admin@smartcampus.test", SmartCampusAuthConstants.RoleAdmin));
        var adminPostResult = Assert.IsType<ObjectResult>(await adminController.Create(
            bookingId,
            new SubmitEquipmentRequestsPayload([new BookingEquipmentItemInput("videoprojector")]),
            CancellationToken.None));
        Assert.Equal(StatusCodes.Status403Forbidden, adminPostResult.StatusCode);

        // 3. Single booking rejected on POST /bookings/{id}/equipment -> 400 ValidationProblem
        stubService.NextAddResult = EquipmentOperationResult.Invalid(new Dictionary<string, string[]>
        {
            ["EquipmentRequests"] = ["Only group bookings (with at least one participant) can request equipment."]
        });
        var userController = CreateController(
            stubService,
            CreatePrincipal(Guid.NewGuid(), "giulia.rossi@smartcampus.test", SmartCampusAuthConstants.RoleUser));
        var singlePostResult = await userController.Create(
            bookingId,
            new SubmitEquipmentRequestsPayload([new BookingEquipmentItemInput("videoprojector")]),
            CancellationToken.None);
        var badRequestObj = Assert.IsAssignableFrom<ObjectResult>(singlePostResult);
        Assert.IsType<ValidationProblemDetails>(badRequestObj.Value);

        // 4. Valid group booking equipment request -> 201 Created
        stubService.NextAddResult = EquipmentOperationResult.Succeeded(
        [
            new EquipmentRequestResponse(
                Guid.NewGuid(),
                bookingId,
                "interactive-whiteboard",
                "Lavagna interattiva",
                null,
                EquipmentRequestStatus.Pending,
                Now.UtcDateTime)
        ]);
        var createdResult = Assert.IsType<CreatedResult>(await userController.Create(
            bookingId,
            new SubmitEquipmentRequestsPayload([new BookingEquipmentItemInput("interactive-whiteboard")]),
            CancellationToken.None));
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);

        // 5. BookingNotActive -> 409, SpaceNotFound -> 404, SpacesUnavailable -> 503
        stubService.NextAddResult = EquipmentOperationResult.BookingNotActive("Ended");
        var conflictObj = Assert.IsType<ObjectResult>(await userController.Create(
            bookingId,
            new SubmitEquipmentRequestsPayload([new BookingEquipmentItemInput("videoprojector")]),
            CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, conflictObj.StatusCode);

        stubService.NextAddResult = EquipmentOperationResult.SpaceNotFound();
        var notFoundObj = Assert.IsType<ObjectResult>(await userController.Create(
            bookingId,
            new SubmitEquipmentRequestsPayload([new BookingEquipmentItemInput("videoprojector")]),
            CancellationToken.None));
        Assert.Equal(StatusCodes.Status404NotFound, notFoundObj.StatusCode);

        stubService.NextAddResult = EquipmentOperationResult.SpacesUnavailable();
        var unavailableObj = Assert.IsType<ObjectResult>(await userController.Create(
            bookingId,
            new SubmitEquipmentRequestsPayload([new BookingEquipmentItemInput("videoprojector")]),
            CancellationToken.None));
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, unavailableObj.StatusCode);
    }

    private static EquipmentRequestsController CreateController(
        IBookingEquipmentService equipmentService,
        ClaimsPrincipal user) =>
        new(equipmentService)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = user }
            }
        };

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string email, string role) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")),
            new Claim(ClaimTypes.Email, email),
            new Claim(ClaimTypes.Role, role)
        ], "TestAuth"));

    private sealed class NoPendingClosureChecker : IPendingClosureChecker
    {
        public Task<string?> GetPendingReferenceCodeAsync(Guid organizerId, CancellationToken cancellationToken) =>
            Task.FromResult<string?>(null);
    }

    private sealed class StubEmailOutbox : IEmailOutbox
    {
        public void EnqueueBookingCreatedEmail(Booking booking) { }

        public void EnqueueInvitationEmail(Invitation invitation, Booking booking) { }

        public void EnqueueInvitationExpiredEmails(Invitation invitation, Booking booking) { }

        public void EnqueueBookingCancelledEmails(Booking booking) { }
    }

    private sealed class SpyAdminAlerts(List<(AdminAlertKind Kind, Guid BookingId, string Message)> sink) : IAdminAlerts
    {
        public Task RaiseAsync(AdminAlertKind kind, Guid bookingId, string message, CancellationToken cancellationToken = default)
        {
            sink.Add((kind, bookingId, message));
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(handler(request));
    }

    private sealed class StubBookingEquipmentService : IBookingEquipmentService
    {
        public EquipmentOperationResult NextGetResult { get; set; } = EquipmentOperationResult.Succeeded([]);
        public EquipmentOperationResult NextAddResult { get; set; } = EquipmentOperationResult.Succeeded([]);

        public Task<EquipmentResolutionPlan?> PreparePlanAsync(
            Guid spaceId,
            IReadOnlyList<NormalizedEquipmentItem> requestedItems,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<EquipmentResolutionPlan?>(EquipmentResolutionPlan.Empty);

        public List<EquipmentRequest> BuildEntities(Guid bookingId, EquipmentResolutionPlan plan, DateTime createdAtUtc) => [];

        public Task RaiseMissingEquipmentAlertsAsync(
            Booking booking,
            EquipmentResolutionPlan plan,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<EquipmentOperationResult> GetByBookingIdAsync(
            Guid bookingId,
            Guid callerUserId,
            string? callerEmail,
            bool isAdmin,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(NextGetResult);

        public Task<EquipmentOperationResult> AddToBookingAsync(
            Guid bookingId,
            Guid callerUserId,
            SubmitEquipmentRequestsPayload payload,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(NextAddResult);
    }
}
