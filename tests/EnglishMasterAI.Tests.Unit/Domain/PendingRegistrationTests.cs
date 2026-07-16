using EnglishMasterAI.Domain.Entities;
using Xunit;

namespace EnglishMasterAI.Tests.Unit.Domain;

/// <summary>
/// Pruebas unitarias de dominio para <see cref="PendingRegistration"/>
/// (reglas de creación y transiciones de estado Approve/Reject) y para
/// <see cref="LevelPricing"/> (cálculo de precio por nivel).
/// </summary>
public class PendingRegistrationTests
{
    private const string ValidImageBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";

    private static byte[] ValidImage => Convert.FromBase64String(ValidImageBase64);

    [Fact]
    [Trait("Category", "Create_HappyPath")]
    public void Create_WithValidData_ReturnsRegistrationInPendingStatus()
    {
        var registration = PendingRegistration.Create(
            "Ana Torres", "ANA@TEST.COM", "hashedpwd", "Spanish", "B2", 160m,
            ValidImage, "image/jpeg");

        Assert.NotEqual(Guid.Empty, registration.Id);
        Assert.Equal("Ana Torres", registration.FullName);
        Assert.Equal("ana@test.com", registration.Email);
        Assert.Equal(RegistrationStatus.Pending, registration.Status);
        Assert.Equal(160m, registration.AmountDue);
        Assert.Null(registration.ReviewedAt);
        Assert.Null(registration.CreatedStudentId);
    }

    [Theory]
    [Trait("Category", "Create_InvalidName")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void Create_WithBlankFullName_ThrowsArgumentException(string blankName)
    {
        Assert.Throws<ArgumentException>(() =>
            PendingRegistration.Create(blankName, "valido@test.com", "hash", "Spanish", "A1", 100m,
                ValidImage, "image/png"));
    }

    [Theory]
    [Trait("Category", "Create_InvalidEmail")]
    [InlineData("")]
    [InlineData("sin-arroba.com")]
    [InlineData("   ")]
    public void Create_WithInvalidEmail_ThrowsArgumentException(string invalidEmail)
    {
        Assert.Throws<ArgumentException>(() =>
            PendingRegistration.Create("Nombre Valido", invalidEmail, "hash", "Spanish", "A1", 100m,
                ValidImage, "image/png"));
    }

    [Fact]
    [Trait("Category", "Create_InvalidReceipt")]
    public void Create_WithNullReceiptImage_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            PendingRegistration.Create("Nombre", "email@test.com", "hash", "Spanish", "A1", 100m,
                null!, "image/png"));
    }

    [Fact]
    [Trait("Category", "Create_InvalidReceipt")]
    public void Create_WithEmptyReceiptImage_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            PendingRegistration.Create("Nombre", "email@test.com", "hash", "Spanish", "A1", 100m,
                Array.Empty<byte>(), "image/png"));
    }

    [Fact]
    [Trait("Category", "Create_InvalidReceipt_BVA")]
    public void Create_WithReceiptImageExactlyAtLimit_Succeeds()
    {
        var exactlyTwoMb = new byte[2 * 1024 * 1024];

        var registration = PendingRegistration.Create(
            "Nombre Límite", "limite@test.com", "hash", "Spanish", "A1", 100m,
            exactlyTwoMb, "image/png");

        Assert.NotNull(registration);
    }

    [Fact]
    [Trait("Category", "Create_InvalidReceipt_BVA")]
    public void Create_WithReceiptImageOneByteOverLimit_ThrowsArgumentException()
    {
        var overLimit = new byte[2 * 1024 * 1024 + 1];

        Assert.Throws<ArgumentException>(() =>
            PendingRegistration.Create("Nombre", "sobrepeso@test.com", "hash", "Spanish", "A1", 100m,
                overLimit, "image/png"));
    }

    [Theory]
    [Trait("Category", "Create_InvalidAmount_BVA")]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithZeroOrNegativeAmount_ThrowsArgumentException(decimal invalidAmount)
    {
        Assert.Throws<ArgumentException>(() =>
            PendingRegistration.Create("Nombre", "monto@test.com", "hash", "Spanish", "A1", invalidAmount,
                ValidImage, "image/png"));
    }

    [Fact]
    [Trait("Category", "Approve_HappyPath")]
    public void Approve_WhenPending_SetsStatusApprovedAndStudentId()
    {
        var registration = PendingRegistration.Create(
            "Nombre", "aprobar@test.com", "hash", "Spanish", "A1", 100m, ValidImage, "image/png");
        var studentId = Guid.NewGuid();

        registration.Approve(studentId);

        Assert.Equal(RegistrationStatus.Approved, registration.Status);
        Assert.Equal(studentId, registration.CreatedStudentId);
        Assert.NotNull(registration.ReviewedAt);
    }

    [Fact]
    [Trait("Category", "Approve_InvalidTransition")]
    public void Approve_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        var registration = PendingRegistration.Create(
            "Nombre", "doble@test.com", "hash", "Spanish", "A1", 100m, ValidImage, "image/png");
        registration.Approve(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => registration.Approve(Guid.NewGuid()));
    }

    [Fact]
    [Trait("Category", "Approve_InvalidTransition")]
    public void Approve_WhenAlreadyRejected_ThrowsInvalidOperationException()
    {
        var registration = PendingRegistration.Create(
            "Nombre", "rechazadoprevio@test.com", "hash", "Spanish", "A1", 100m, ValidImage, "image/png");
        registration.Reject("motivo");

        Assert.Throws<InvalidOperationException>(() => registration.Approve(Guid.NewGuid()));
    }

    [Fact]
    [Trait("Category", "Reject_HappyPath")]
    public void Reject_WhenPending_SetsStatusRejectedWithReason()
    {
        var registration = PendingRegistration.Create(
            "Nombre", "rechazar@test.com", "hash", "Spanish", "A1", 100m, ValidImage, "image/png");

        registration.Reject("Comprobante no coincide con el monto");

        Assert.Equal(RegistrationStatus.Rejected, registration.Status);
        Assert.Equal("Comprobante no coincide con el monto", registration.RejectionReason);
        Assert.NotNull(registration.ReviewedAt);
    }

    [Fact]
    [Trait("Category", "Reject_HappyPath")]
    public void Reject_WithNullReason_IsAllowed()
    {
        var registration = PendingRegistration.Create(
            "Nombre", "sinmotivo@test.com", "hash", "Spanish", "A1", 100m, ValidImage, "image/png");

        registration.Reject(null);

        Assert.Equal(RegistrationStatus.Rejected, registration.Status);
        Assert.Null(registration.RejectionReason);
    }

    [Fact]
    [Trait("Category", "Reject_InvalidTransition")]
    public void Reject_WhenAlreadyApproved_ThrowsInvalidOperationException()
    {
        var registration = PendingRegistration.Create(
            "Nombre", "aprobadoprevio@test.com", "hash", "Spanish", "A1", 100m, ValidImage, "image/png");
        registration.Approve(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => registration.Reject("intento tardío"));
    }

    [Theory]
    [Trait("Category", "LevelPricing_Valid")]
    [InlineData("A1", 100)]
    [InlineData("A2", 120)]
    [InlineData("B1", 140)]
    [InlineData("B2", 160)]
    [InlineData("C1", 180)]
    [InlineData("C2", 200)]
    public void GetPrice_WithValidLevel_ReturnsExpectedPrice(string level, decimal expectedPrice)
    {
        Assert.Equal(expectedPrice, LevelPricing.GetPrice(level));
    }

    [Theory]
    [Trait("Category", "LevelPricing_Normalization")]
    [InlineData("a1", 100)]
    [InlineData(" B1 ", 140)]
    [InlineData("c2", 200)]
    public void GetPrice_IsCaseInsensitiveAndTrimsWhitespace(string level, decimal expectedPrice)
    {
        Assert.Equal(expectedPrice, LevelPricing.GetPrice(level));
    }

    [Theory]
    [Trait("Category", "LevelPricing_Invalid")]
    [InlineData("D1")]
    [InlineData("")]
    [InlineData("A0")]
    [InlineData("XYZ")]
    public void GetPrice_WithInvalidLevel_ThrowsArgumentException(string invalidLevel)
    {
        Assert.Throws<ArgumentException>(() => LevelPricing.GetPrice(invalidLevel));
    }
}