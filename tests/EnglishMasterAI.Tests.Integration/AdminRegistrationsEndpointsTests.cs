using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace EnglishMasterAI.Tests.Integration;

public class AdminRegistrationsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private const string CorrectAdminPassword = "TestAdminPassword123";
    private const string ValidImageBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    public AdminRegistrationsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    [Trait("Category", "Auth_MissingHeader")]
    public async Task GetPending_WithoutAdminPasswordHeader_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/api/admin/registrations/pending");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Auth_WrongPassword")]
    public async Task GetPending_WithWrongAdminPassword_ReturnsUnauthorized()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/registrations/pending");
        request.Headers.Add("X-Admin-Password", "contraseña-incorrecta");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "Auth_CorrectPassword")]
    public async Task GetPending_WithCorrectAdminPassword_ReturnsOk()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/registrations/pending");
        request.Headers.Add("X-Admin-Password", CorrectAdminPassword);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Category", "ApproveFlow_EndToEnd")]
    public async Task Approve_ValidPendingRegistration_CreatesStudentAndRemovesFromPendingList()
    {
        var email = $"e2e-approve-{Guid.NewGuid():N}@test.com";
        var createCommand = new
        {
            fullName = "Flujo Completo",
            email,
            password = "Password1!",
            nativeLanguage = "Spanish",
            initialLevel = "A1",
            receiptImageBase64 = ValidImageBase64,
            receiptContentType = "image/png"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/registrations", createCommand);
        var created = await createResponse.Content.ReadFromJsonAsync<PendingRegistrationResponse>();

        var approveRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/registrations/{created!.Id}/approve");
        approveRequest.Headers.Add("X-Admin-Password", CorrectAdminPassword);
        var approveResponse = await _client.SendAsync(approveRequest);

        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/api/admin/registrations/pending");
        listRequest.Headers.Add("X-Admin-Password", CorrectAdminPassword);
        var listResponse = await _client.SendAsync(listRequest);
        var pendingList = await listResponse.Content
            .ReadFromJsonAsync<List<PendingRegistrationSummaryResponse>>();

        Assert.DoesNotContain(pendingList!, r => r.Id == created.Id);

        var studentsResponse = await _client.GetAsync("/api/students");
        Assert.Equal(HttpStatusCode.OK, studentsResponse.StatusCode);
        var students = await studentsResponse.Content.ReadFromJsonAsync<List<StudentSummaryResponse>>();
        Assert.Contains(students!, s => s.Email == email);
    }

    [Fact]
    [Trait("Category", "ApproveFlow_DoubleApprove")]
    public async Task Approve_SameRegistrationTwice_SecondCallReturnsBadRequest()
    {
        var createCommand = new
        {
            fullName = "Doble Aprobación",
            email = $"doble-{Guid.NewGuid():N}@test.com",
            password = "Password1!",
            nativeLanguage = "Spanish",
            initialLevel = "A1",
            receiptImageBase64 = ValidImageBase64,
            receiptContentType = "image/png"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/registrations", createCommand);
        var created = await createResponse.Content.ReadFromJsonAsync<PendingRegistrationResponse>();

        var firstApprove = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/registrations/{created!.Id}/approve");
        firstApprove.Headers.Add("X-Admin-Password", CorrectAdminPassword);
        await _client.SendAsync(firstApprove);

        var secondApprove = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/registrations/{created.Id}/approve");
        secondApprove.Headers.Add("X-Admin-Password", CorrectAdminPassword);
        var secondResponse = await _client.SendAsync(secondApprove);

        Assert.Equal(HttpStatusCode.BadRequest, secondResponse.StatusCode);
    }

    [Fact]
    [Trait("Category", "RejectFlow_EndToEnd")]
    public async Task Reject_ValidPendingRegistration_DoesNotCreateStudent()
    {
        var email = $"e2e-reject-{Guid.NewGuid():N}@test.com";
        var createCommand = new
        {
            fullName = "Rechazo Completo",
            email,
            password = "Password1!",
            nativeLanguage = "Spanish",
            initialLevel = "A1",
            receiptImageBase64 = ValidImageBase64,
            receiptContentType = "image/png"
        };
        var createResponse = await _client.PostAsJsonAsync("/api/registrations", createCommand);
        var created = await createResponse.Content.ReadFromJsonAsync<PendingRegistrationResponse>();

        var rejectRequest = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/registrations/{created!.Id}/reject");
        rejectRequest.Headers.Add("X-Admin-Password", CorrectAdminPassword);
        rejectRequest.Content = JsonContent.Create(new { reason = "Comprobante ilegible" });
        var rejectResponse = await _client.SendAsync(rejectRequest);

        Assert.Equal(HttpStatusCode.OK, rejectResponse.StatusCode);

        var studentsResponse = await _client.GetAsync("/api/students");
        var students = await studentsResponse.Content.ReadFromJsonAsync<List<StudentSummaryResponse>>();
        Assert.DoesNotContain(students!, s => s.Email == email);
    }

    [Fact]
    [Trait("Category", "ApproveFlow_NotFound")]
    public async Task Approve_NonExistentId_ReturnsNotFound()
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post, $"/api/admin/registrations/{Guid.NewGuid()}/approve");
        request.Headers.Add("X-Admin-Password", CorrectAdminPassword);

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private record PendingRegistrationResponse(
        Guid Id, string FullName, string Email, string NativeLanguage, string InitialLevel,
        decimal AmountDue, string Status, DateTime CreatedAt, DateTime? ReviewedAt, string? RejectionReason);

    private record PendingRegistrationSummaryResponse(
        Guid Id, string FullName, string Email, string InitialLevel,
        decimal AmountDue, string Status, DateTime CreatedAt);

    private record StudentSummaryResponse(Guid Id, string FullName, string Email);
}