namespace EnglishMasterAI.Domain.Entities;

public enum RegistrationStatus
{
    Pending  = 0,
    Approved = 1,
    Rejected = 2
}

/// <summary>
/// Representa la solicitud de registro de un estudiante que aún no ha sido
/// verificada. Contiene los mismos datos que se necesitarían para crear un
/// Student, más la información del pago (monto, comprobante). Solo cuando un
/// administrador la Aprueba se crea el Student real.
/// </summary>
public class PendingRegistration
{
    public Guid Id { get; private set; }

    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string NativeLanguage { get; private set; } = string.Empty;
    public string InitialLevel { get; private set; } = string.Empty;

    public decimal AmountDue { get; private set; }
    public byte[] ReceiptImage { get; private set; } = Array.Empty<byte>();
    public string ReceiptContentType { get; private set; } = string.Empty;

    public RegistrationStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReviewedAt { get; private set; }
    public string? RejectionReason { get; private set; }

    public Guid? CreatedStudentId { get; private set; }

    private PendingRegistration() { }

    public static PendingRegistration Create(
        string fullName,
        string email,
        string passwordHash,
        string nativeLanguage,
        string initialLevel,
        decimal amountDue,
        byte[] receiptImage,
        string receiptContentType)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("El nombre completo es obligatorio.");
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("El correo electrónico no es válido.");
        if (receiptImage is null || receiptImage.Length == 0)
            throw new ArgumentException("Debes subir una imagen del comprobante de pago.");
        if (receiptImage.Length > 2 * 1024 * 1024)
            throw new ArgumentException("La imagen del comprobante no debe superar 2 MB.");
        if (amountDue <= 0)
            throw new ArgumentException("El monto a pagar debe ser mayor a cero.");

        return new PendingRegistration
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            NativeLanguage = nativeLanguage,
            InitialLevel = initialLevel,
            AmountDue = amountDue,
            ReceiptImage = receiptImage,
            ReceiptContentType = receiptContentType,
            Status = RegistrationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Approve(Guid createdStudentId)
    {
        if (Status != RegistrationStatus.Pending)
            throw new InvalidOperationException("Esta solicitud ya fue revisada.");

        Status = RegistrationStatus.Approved;
        ReviewedAt = DateTime.UtcNow;
        CreatedStudentId = createdStudentId;
    }

    public void Reject(string? reason)
    {
        if (Status != RegistrationStatus.Pending)
            throw new InvalidOperationException("Esta solicitud ya fue revisada.");

        Status = RegistrationStatus.Rejected;
        ReviewedAt = DateTime.UtcNow;
        RejectionReason = reason;
    }
}