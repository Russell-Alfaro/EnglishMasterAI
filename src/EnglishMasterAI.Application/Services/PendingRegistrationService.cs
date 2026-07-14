using System.Security.Cryptography;
using System.Text;
using EnglishMasterAI.Application.DTOs;
using EnglishMasterAI.Application.Ports.Output;
using EnglishMasterAI.Domain.Entities;
using EnglishMasterAI.Domain.Exceptions;

namespace EnglishMasterAI.Application.Services;

public class PendingRegistrationService
{
    private readonly IPendingRegistrationRepository _pendingRepository;
    private readonly IStudentRepository _studentRepository;

    public PendingRegistrationService(
        IPendingRegistrationRepository pendingRepository,
        IStudentRepository studentRepository)
    {
        _pendingRepository = pendingRepository ?? throw new ArgumentNullException(nameof(pendingRepository));
        _studentRepository = studentRepository ?? throw new ArgumentNullException(nameof(studentRepository));
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Crear solicitud de registro (público, sin autenticación)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<PendingRegistrationDto> CreateAsync(
        CreatePendingRegistrationCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var normalizedEmail = command.Email.Trim().ToLowerInvariant();

        // No permitir duplicados: ni como estudiante ya activo, ni como
        // solicitud pendiente/aprobada previa con el mismo correo.
        if (await _studentRepository.ExistsByEmailAsync(normalizedEmail, cancellationToken))
            throw new DuplicateEmailException(normalizedEmail);

        if (await _pendingRepository.ExistsPendingOrApprovedByEmailAsync(normalizedEmail, cancellationToken))
            throw new DuplicateEmailException(normalizedEmail);

        // El precio SIEMPRE se calcula en el servidor según el nivel — nunca
        // se confía en un monto que venga del cliente.
        decimal amount = LevelPricing.GetPrice(command.InitialLevel);

        byte[] imageBytes;
        try
        {
            imageBytes = Convert.FromBase64String(command.ReceiptImageBase64);
        }
        catch (FormatException)
        {
            throw new ArgumentException("La imagen del comprobante no es válida.");
        }

        string passwordHash = HashPassword(command.Password);

        var registration = PendingRegistration.Create(
            command.FullName,
            normalizedEmail,
            passwordHash,
            command.NativeLanguage,
            command.InitialLevel,
            amount,
            imageBytes,
            command.ReceiptContentType);

        var saved = await _pendingRepository.AddAsync(registration, cancellationToken);
        return MapToDto(saved);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Listar solicitudes pendientes (solo admin)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<IReadOnlyList<PendingRegistrationSummaryDto>> GetPendingAsync(
        CancellationToken cancellationToken = default)
    {
        var registrations = await _pendingRepository.GetByStatusAsync(RegistrationStatus.Pending, cancellationToken);

        return registrations
            .OrderBy(r => r.CreatedAt)
            .Select(r => new PendingRegistrationSummaryDto(
                r.Id, r.FullName, r.Email, r.InitialLevel, r.AmountDue, r.Status.ToString(), r.CreatedAt))
            .ToList();
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Obtener solo la imagen del comprobante (para no cargarlas todas juntas)
    // ─────────────────────────────────────────────────────────────────────
    public async Task<(byte[] Bytes, string ContentType)> GetReceiptImageAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var registration = await _pendingRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new PendingRegistrationNotFoundException(id);

        return (registration.ReceiptImage, registration.ReceiptContentType);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Aprobar: crea el Student real y marca la solicitud como Approved
    // ─────────────────────────────────────────────────────────────────────
    public async Task ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var registration = await _pendingRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new PendingRegistrationNotFoundException(id);

        if (registration.Status != RegistrationStatus.Pending)
            throw new InvalidOperationException("Esta solicitud ya fue revisada.");

        // Re-verificar que el correo siga libre (por si se registró otro
        // estudiante con el mismo correo mientras esta solicitud esperaba).
        if (await _studentRepository.ExistsByEmailAsync(registration.Email, cancellationToken))
            throw new DuplicateEmailException(registration.Email);

        var student = Student.Create(
            registration.FullName,
            registration.Email,
            registration.PasswordHash,
            registration.NativeLanguage,
            registration.InitialLevel);

        var savedStudent = await _studentRepository.AddAsync(student, cancellationToken);

        registration.Approve(savedStudent.Id);
        await _pendingRepository.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Rechazar: solo cambia el estado, nunca crea un Student
    // ─────────────────────────────────────────────────────────────────────
    public async Task RejectAsync(Guid id, string? reason, CancellationToken cancellationToken = default)
    {
        var registration = await _pendingRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new PendingRegistrationNotFoundException(id);

        registration.Reject(reason);
        await _pendingRepository.SaveChangesAsync(cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────
    private static PendingRegistrationDto MapToDto(PendingRegistration r) => new(
        r.Id, r.FullName, r.Email, r.NativeLanguage, r.InitialLevel,
        r.AmountDue, r.Status.ToString(), r.CreatedAt, r.ReviewedAt, r.RejectionReason);

    // Mismo algoritmo de hash que usa StudentService, para que las
    // contraseñas sean compatibles al crear el Student real en Approve().
    private static string HashPassword(string password) =>
        Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(password)));
}