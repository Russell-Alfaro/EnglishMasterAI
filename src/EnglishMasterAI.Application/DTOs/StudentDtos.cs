namespace EnglishMasterAI.Application.DTOs;

/// <summary>
/// DTO de transferencia que expone los datos de un estudiante hacia las capas externas.
/// Desacopla la entidad de dominio del contrato público de la API.
/// </summary>
public record StudentDto(
    Guid Id,
    string FullName,
    string Email,
    string NativeLanguage,
    int CurrentLevelScore,
    int TotalLessonsCompleted,
    int TotalPracticeMinutes,
    DateTime RegisteredAt,
    DateTime? LastActivityAt,
    bool IsActive,
    string ProficiencyLevel
);

/// <summary>
/// DTO para el comando de registro de un nuevo estudiante.
/// </summary>
public record RegisterStudentCommand(
    string FullName,
    string Email,
    string Password,
    string NativeLanguage = "Spanish",
    string InitialLevel = "A1"
);

/// <summary>
/// DTO que resume el progreso del estudiante para el Dashboard.
/// </summary>
public record StudentDashboardDto(
    Guid StudentId,
    string FullName,
    int CurrentLevelScore,
    int TotalLessonsCompleted,
    int TotalPracticeMinutes,
    string ProficiencyLevel,
    double ProgressPercentage,
    double AveragePracticeGrade,
    List<PracticeDto> PracticeHistory
);

public record PracticeDto(
    int Grade,
    string PracticeType,
    DateTime CompletedAt
);

/// <summary>
/// DTO para el comando de sumar progreso a un estudiante existente.
/// Usado por PUT /api/students/{id}/add-progress.
/// </summary>
public record AddProgressCommand(
    int Points,
    int LessonsCompleted,
    int PracticeMinutes
);

public record AddLessonCommand(
    int LessonNumber,
    int DurationMinutes
);

public record AddPracticeCommand(
    int Grade,
    string PracticeType
);
