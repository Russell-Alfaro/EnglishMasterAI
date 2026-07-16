using EnglishMasterAI.Application.DTOs;
using EnglishMasterAI.Application.Ports.Output;
using EnglishMasterAI.Application.Services;
using EnglishMasterAI.Domain.Entities;
using EnglishMasterAI.Domain.Exceptions;
using Moq;
using Xunit;

namespace EnglishMasterAI.Tests.Unit.Application;

/// <summary>
/// Suite de pruebas unitarias para <see cref="PendingRegistrationService"/>,
/// el servicio que orquesta el registro de estudiantes con verificación de
/// pago manual (crear solicitud, listar pendientes, aprobar, rechazar).
///
/// ESTRATEGIAS APLICADAS: Boundary Value Analysis (BVA), Equivalence
/// Partitioning (EP), [Theory]+[InlineData] para parametrización.
///
/// HERRAMIENTAS: xUnit, Moq (MockBehavior.Strict para ambos repositorios).
/// </summary>
public class PendingRegistrationServiceTests
{
    private readonly Mock<IPendingRegistrationRepository> _pendingRepoMock;
    private readonly Mock<IStudentRepository> _studentRepoMock;
    private readonly PendingRegistrationService _sut;

    private const string ValidImageBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    public PendingRegistrationServiceTests()
    {
        _pendingRepoMock = new Mock<IPendingRegistrationRepository>(MockBehavior.Strict);
        _studentRepoMock = new Mock<IStudentRepository>(MockBehavior.Strict);
        _sut = new PendingRegistrationService(_pendingRepoMock.Object, _studentRepoMock.Object);
    }

    private static CreatePendingRegistrationCommand ValidCommand(string level = "A1") => new(
        FullName: "Estudiante de Prueba",
        Email: "estudiante@test.com",
        Password: "Password1!",
        NativeLanguage: "Spanish",
        InitialLevel: level,
        ReceiptImageBase64: ValidImageBase64,
        ReceiptContentType: "image/png");

    // =========================================================================
    //  BLOQUE A ─ CreateAsync: camino feliz + precio correcto por nivel [6 casos]
    // =========================================================================

    [Theory]
    [Trait("Category", "CreateAsync_HappyPath")]
    [InlineData("A1", 100)]
    [InlineData("A2", 120)]
    [InlineData("B1", 140)]
    [InlineData("B2", 160)]
    [InlineData("C1", 180)]
    [InlineData("C2", 200)]
    public async Task CreateAsync_WithValidData_ComputesCorrectPriceAndPersists(
        string level, decimal expectedPrice)
    {
        var command = ValidCommand(level);

        _studentRepoMock
            .Setup(r => r.ExistsByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _pendingRepoMock
            .Setup(r => r.ExistsPendingOrApprovedByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _pendingRepoMock
            .Setup(r => r.AddAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingRegistration r, CancellationToken _) => r);

        PendingRegistrationDto result = await _sut.CreateAsync(command);

        Assert.Equal(expectedPrice, result.AmountDue);
        Assert.Equal(level, result.InitialLevel);
        Assert.Equal("Pending", result.Status);
        Assert.Equal(command.Email, result.Email);

        _pendingRepoMock.Verify(
            r => r.AddAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // =========================================================================
    //  BLOQUE B ─ CreateAsync: correo duplicado, dos orígenes distintos [2 casos]
    // =========================================================================

    [Fact]
    [Trait("Category", "CreateAsync_DuplicateEmail")]
    public async Task CreateAsync_WhenEmailAlreadyBelongsToActiveStudent_ThrowsDuplicateEmailException()
    {
        var command = ValidCommand();
        _studentRepoMock
            .Setup(r => r.ExistsByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateEmailException>(() => _sut.CreateAsync(command));

        _pendingRepoMock.Verify(
            r => r.AddAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    [Trait("Category", "CreateAsync_DuplicateEmail")]
    public async Task CreateAsync_WhenEmailHasPendingOrApprovedRequest_ThrowsDuplicateEmailException()
    {
        var command = ValidCommand();
        _studentRepoMock
            .Setup(r => r.ExistsByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _pendingRepoMock
            .Setup(r => r.ExistsPendingOrApprovedByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateEmailException>(() => _sut.CreateAsync(command));

        _pendingRepoMock.Verify(
            r => r.AddAsync(It.IsAny<PendingRegistration>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // =========================================================================
    //  BLOQUE C ─ CreateAsync: nivel inválido [3 casos, EP inválida]
    // =========================================================================

    [Theory]
    [Trait("Category", "CreateAsync_InvalidLevel")]
    [InlineData("Z9")]
    [InlineData("")]
    [InlineData("NIVEL-X")]
    public async Task CreateAsync_WithInvalidLevel_ThrowsArgumentException(string invalidLevel)
    {
        var command = ValidCommand(invalidLevel);

        _studentRepoMock
            .Setup(r => r.ExistsByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _pendingRepoMock
            .Setup(r => r.ExistsPendingOrApprovedByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(command));
    }

    // =========================================================================
    //  BLOQUE D ─ CreateAsync: imagen de comprobante inválida [2 casos]
    // =========================================================================

    [Fact]
    [Trait("Category", "CreateAsync_InvalidImage")]
    public async Task CreateAsync_WithMalformedBase64Image_ThrowsArgumentException()
    {
        var command = ValidCommand() with { ReceiptImageBase64 = "esto-no-es-base64-válido-!!" };

        _studentRepoMock
            .Setup(r => r.ExistsByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _pendingRepoMock
            .Setup(r => r.ExistsPendingOrApprovedByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(command));
    }

    [Fact]
    [Trait("Category", "CreateAsync_InvalidImage")]
    public async Task CreateAsync_WithImageLargerThan2MB_ThrowsArgumentException()
    {
        var oversized = new byte[2 * 1024 * 1024 + 1024];
        var command = ValidCommand() with { ReceiptImageBase64 = Convert.ToBase64String(oversized) };

        _studentRepoMock
            .Setup(r => r.ExistsByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _pendingRepoMock
            .Setup(r => r.ExistsPendingOrApprovedByEmailAsync(command.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.CreateAsync(command));
    }

    // =========================================================================
    //  BLOQUE E ─ GetPendingAsync [2 casos]
    // =========================================================================

    [Fact]
    [Trait("Category", "GetPendingAsync")]
    public async Task GetPendingAsync_ReturnsOnlyPendingRegistrations_OrderedByCreatedAt()
    {
        var older = PendingRegistration.Create(
            "Primero", "primero@test.com", "hash", "Spanish", "A1", 100m,
            Convert.FromBase64String(ValidImageBase64), "image/png");
        var newer = PendingRegistration.Create(
            "Segundo", "segundo@test.com", "hash", "Spanish", "B1", 140m,
            Convert.FromBase64String(ValidImageBase64), "image/png");

        _pendingRepoMock
            .Setup(r => r.GetByStatusAsync(RegistrationStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingRegistration> { newer, older });

        var result = await _sut.GetPendingAsync();

        Assert.Equal(2, result.Count);
        Assert.True(result[0].CreatedAt <= result[1].CreatedAt);
    }

    [Fact]
    [Trait("Category", "GetPendingAsync")]
    public async Task GetPendingAsync_WhenNoneArePending_ReturnsEmptyList()
    {
        _pendingRepoMock
            .Setup(r => r.GetByStatusAsync(RegistrationStatus.Pending, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<PendingRegistration>());

        var result = await _sut.GetPendingAsync();

        Assert.Empty(result);
    }

    // =========================================================================
    //  BLOQUE F ─ GetReceiptImageAsync [2 casos]
    // =========================================================================

    [Fact]
    [Trait("Category", "GetReceiptImageAsync")]
    public async Task GetReceiptImageAsync_WithExistingId_ReturnsBytesAndContentType()
    {
        var registration = PendingRegistration.Create(
            "Nombre", "receipt@test.com", "hash", "Spanish", "A1", 100m,
            Convert.FromBase64String(ValidImageBase64), "image/png");

        _pendingRepoMock
            .Setup(r => r.GetByIdAsync(registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);

        var (bytes, contentType) = await _sut.GetReceiptImageAsync(registration.Id);

        Assert.NotEmpty(bytes);
        Assert.Equal("image/png", contentType);
    }

    [Fact]
    [Trait("Category", "GetReceiptImageAsync")]
    public async Task GetReceiptImageAsync_WithNonExistentId_ThrowsPendingRegistrationNotFoundException()
    {
        var missingId = Guid.NewGuid();
        _pendingRepoMock
            .Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingRegistration?)null);

        var ex = await Assert.ThrowsAsync<PendingRegistrationNotFoundException>(
            () => _sut.GetReceiptImageAsync(missingId));
        Assert.Equal(missingId, ex.RegistrationId);
    }

    // =========================================================================
    //  BLOQUE G ─ ApproveAsync [4 casos]
    // =========================================================================

    [Fact]
    [Trait("Category", "ApproveAsync_HappyPath")]
    public async Task ApproveAsync_WithPendingRequest_CreatesStudentAndMarksApproved()
    {
        var registration = PendingRegistration.Create(
            "Nuevo Estudiante", "aprobar@test.com", "hashedpass", "Spanish", "B1", 140m,
            Convert.FromBase64String(ValidImageBase64), "image/png");

        _pendingRepoMock
            .Setup(r => r.GetByIdAsync(registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);
        _studentRepoMock
            .Setup(r => r.ExistsByEmailAsync(registration.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _studentRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Student s, CancellationToken _) => s);
        _pendingRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.ApproveAsync(registration.Id);

        Assert.Equal(RegistrationStatus.Approved, registration.Status);
        Assert.NotNull(registration.CreatedStudentId);
        _studentRepoMock.Verify(
            r => r.AddAsync(It.IsAny<Student>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    [Trait("Category", "ApproveAsync_NotFound")]
    public async Task ApproveAsync_WithNonExistentId_ThrowsPendingRegistrationNotFoundException()
    {
        var missingId = Guid.NewGuid();
        _pendingRepoMock
            .Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingRegistration?)null);

        await Assert.ThrowsAsync<PendingRegistrationNotFoundException>(() => _sut.ApproveAsync(missingId));
    }

    [Fact]
    [Trait("Category", "ApproveAsync_AlreadyReviewed")]
    public async Task ApproveAsync_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        var registration = PendingRegistration.Create(
            "Ya Aprobado", "yaaprobado@test.com", "hash", "Spanish", "A1", 100m,
            Convert.FromBase64String(ValidImageBase64), "image/png");
        registration.Approve(Guid.NewGuid());

        _pendingRepoMock
            .Setup(r => r.GetByIdAsync(registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.ApproveAsync(registration.Id));
    }

    [Fact]
    [Trait("Category", "ApproveAsync_RaceCondition")]
    public async Task ApproveAsync_WhenEmailWasTakenAfterSubmission_ThrowsDuplicateEmailException()
    {
        var registration = PendingRegistration.Create(
            "Carrera", "carrera@test.com", "hash", "Spanish", "A1", 100m,
            Convert.FromBase64String(ValidImageBase64), "image/png");

        _pendingRepoMock
            .Setup(r => r.GetByIdAsync(registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);
        _studentRepoMock
            .Setup(r => r.ExistsByEmailAsync(registration.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await Assert.ThrowsAsync<DuplicateEmailException>(() => _sut.ApproveAsync(registration.Id));

        Assert.Equal(RegistrationStatus.Pending, registration.Status);
    }

    // =========================================================================
    //  BLOQUE H ─ RejectAsync [3 casos]
    // =========================================================================

    [Fact]
    [Trait("Category", "RejectAsync_HappyPath")]
    public async Task RejectAsync_WithPendingRequest_MarksAsRejectedWithReason()
    {
        var registration = PendingRegistration.Create(
            "Rechazado", "rechazado@test.com", "hash", "Spanish", "A1", 100m,
            Convert.FromBase64String(ValidImageBase64), "image/png");

        _pendingRepoMock
            .Setup(r => r.GetByIdAsync(registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);
        _pendingRepoMock
            .Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        await _sut.RejectAsync(registration.Id, "Comprobante ilegible");

        Assert.Equal(RegistrationStatus.Rejected, registration.Status);
        Assert.Equal("Comprobante ilegible", registration.RejectionReason);
        Assert.Null(registration.CreatedStudentId);
    }

    [Fact]
    [Trait("Category", "RejectAsync_NotFound")]
    public async Task RejectAsync_WithNonExistentId_ThrowsPendingRegistrationNotFoundException()
    {
        var missingId = Guid.NewGuid();
        _pendingRepoMock
            .Setup(r => r.GetByIdAsync(missingId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PendingRegistration?)null);

        await Assert.ThrowsAsync<PendingRegistrationNotFoundException>(
            () => _sut.RejectAsync(missingId, null));
    }

    [Fact]
    [Trait("Category", "RejectAsync_AlreadyReviewed")]
    public async Task RejectAsync_WhenAlreadyRejected_ThrowsInvalidOperationException()
    {
        var registration = PendingRegistration.Create(
            "Doble Rechazo", "doblerechazo@test.com", "hash", "Spanish", "A1", 100m,
            Convert.FromBase64String(ValidImageBase64), "image/png");
        registration.Reject("Primer rechazo");

        _pendingRepoMock
            .Setup(r => r.GetByIdAsync(registration.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(registration);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.RejectAsync(registration.Id, "Segundo rechazo"));
    }

    // =========================================================================
    //  BLOQUE I ─ Constructor: dependencias nulas [2 casos]
    // =========================================================================

    [Fact]
    [Trait("Category", "Service_Constructor")]
    public void Constructor_WithNullPendingRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PendingRegistrationService(null!, _studentRepoMock.Object));
    }

    [Fact]
    [Trait("Category", "Service_Constructor")]
    public void Constructor_WithNullStudentRepository_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PendingRegistrationService(_pendingRepoMock.Object, null!));
    }
}