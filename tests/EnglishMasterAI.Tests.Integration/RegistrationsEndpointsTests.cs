using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace EnglishMasterAI.Tests.Integration;

public class RegistrationsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    private const string ValidImageBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    public RegistrationsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Theory]
    [Trait("Category", "GetPrice")]
    [InlineData("A1", 100)]
    [InlineData("B1", 140)]
    [InlineData("C2", 200)]
    public async Task GetPrice_WithValidLevel_ReturnsOkWithCorrectAmount(string level, decimal expectedPrice)
    {
        var response = await _client.GetAsync($"/api/registrations/price/{level}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var price = await response.Content.ReadFromJsonAsync<decimal>();
        Assert.Equal(expectedPrice, price);
    }

    [Fact]
    [Trait("Category", "GetPrice")]
    public async Task GetPrice_WithInvalidLevel_ReturnsBadRequest()
    {
        var response = await _client.GetAsync("/api/registrations/price/Z9");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "CreateRegistration_HappyPath")]
    public async Task CreateRegistration_WithValidData_ReturnsCreatedWithPendingStatus()
    {
        var command = new
        {
            fullName = "Estudiante Integración",
            email = $"integracion-{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            nativeLanguage = "Spanish",
            initialLevel = "B1",
            receiptImageBase64 = ValidImageBase64,
            receiptContentType = "image/png"
        };

        var response = await _client.PostAsJsonAsync("/api/registrations", command);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<PendingRegistrationResponse>();
        Assert.NotNull(body);
        Assert.Equal("Pending", body!.Status);
        Assert.Equal(140, body.AmountDue);
    }

    [Fact]
    [Trait("Category", "CreateRegistration_Duplicate")]
    public async Task CreateRegistration_WithDuplicateEmail_ReturnsConflict()
    {
        var email = $"duplicado-{Guid.NewGuid():N}@test.com";
        var command = new
        {
            fullName = "Primero",
            email,
            password = "Password1!",
            nativeLanguage = "Spanish",
            initialLevel = "A1",
            receiptImageBase64 = ValidImageBase64,
            receiptContentType = "image/png"
        };

        var first = await _client.PostAsJsonAsync("/api/registrations", command);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/registrations", command);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    [Trait("Category", "CreateRegistration_InvalidData")]
    public async Task CreateRegistration_WithoutReceiptImage_ReturnsBadRequest()
    {
        var command = new
        {
            fullName = "Sin Comprobante",
            email = $"sincomprobante-{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            nativeLanguage = "Spanish",
            initialLevel = "A1",
            receiptImageBase64 = "",
            receiptContentType = "image/png"
        };

        var response = await _client.PostAsJsonAsync("/api/registrations", command);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private record PendingRegistrationResponse(
        Guid Id, string FullName, string Email, string NativeLanguage, string InitialLevel,
        decimal AmountDue, string Status, DateTime CreatedAt, DateTime? ReviewedAt, string? RejectionReason);
}