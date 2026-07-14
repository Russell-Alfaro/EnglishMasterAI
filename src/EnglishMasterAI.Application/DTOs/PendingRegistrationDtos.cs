namespace EnglishMasterAI.Application.DTOs;

/// <summary>
/// Comando para crear una solicitud de registro pendiente de verificación.
/// La imagen viene en Base64 porque el frontend Blazor la manda como JSON.
/// </summary>
public record CreatePendingRegistrationCommand(
    string FullName,
    string Email,
    string Password,
    string NativeLanguage,
    string InitialLevel,
    string ReceiptImageBase64,
    string ReceiptContentType
);

public record PendingRegistrationDto(
    Guid Id,
    string FullName,
    string Email,
    string NativeLanguage,
    string InitialLevel,
    decimal AmountDue,
    string Status,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    string? RejectionReason
);

/// <summary>
/// DTO liviano para la lista de admin — NO incluye la imagen (pesada),
/// esa se pide aparte por endpoint para no cargar todas las imágenes de golpe.
/// </summary>
public record PendingRegistrationSummaryDto(
    Guid Id,
    string FullName,
    string Email,
    string InitialLevel,
    decimal AmountDue,
    string Status,
    DateTime CreatedAt
);

public record RejectRegistrationCommand(string? Reason);