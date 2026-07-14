using EnglishMasterAI.Application.DTOs;
using EnglishMasterAI.Application.Ports.Output;
using EnglishMasterAI.Domain.Entities;
using EnglishMasterAI.Domain.Exceptions;

namespace EnglishMasterAI.Application.Services;

/// <summary>
/// Servicio de aplicación (caso de uso) que orquesta la lógica de negocio
/// relacionada con los estudiantes. Depende únicamente de abstracciones (IStudentRepository).
///
/// En Arquitectura Hexagonal, este es el "núcleo" que implementa los casos de uso,
/// siendo independiente del framework, la base de datos y la UI.
/// </summary>
public class StudentService
{
    private readonly IStudentRepository _studentRepository;

    public StudentService(IStudentRepository studentRepository)
    {
        _studentRepository = studentRepository
            ?? throw new ArgumentNullException(nameof(studentRepository));
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CASO DE USO: Registrar Estudiante
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Registra un nuevo estudiante en el sistema.
    /// Lanza <see cref="DuplicateEmailException"/> si el correo ya existe.
    /// </summary>
    public async Task<StudentDto> RegisterStudentAsync(
        RegisterStudentCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Validar que el correo no esté duplicado (regla de negocio)
        bool emailExists = await _studentRepository
            .ExistsByEmailAsync(command.Email, cancellationToken);

        if (emailExists)
            throw new DuplicateEmailException(command.Email);

        // Hashear contraseña (simplificado para el proyecto universitario)
        string passwordHash = BCryptHashPassword(command.Password);

        // Delegar la creación a la entidad de dominio (que contiene sus propias reglas)
        Student newStudent = Student.Create(
            command.FullName,
            command.Email,
            passwordHash,
            command.NativeLanguage,
            command.InitialLevel);

        // Persistir a través del puerto de salida
        Student savedStudent = await _studentRepository
            .AddAsync(newStudent, cancellationToken);

        return MapToDto(savedStudent);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CASO DE USO: Obtener Estudiante por ID
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retorna los datos de un estudiante por su ID.
    /// Lanza <see cref="StudentNotFoundException"/> si no existe.
    /// </summary>
    public async Task<StudentDto> GetStudentByIdAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        Student? student = await _studentRepository
            .GetByIdAsync(studentId, cancellationToken);

        if (student is null)
            throw new StudentNotFoundException(studentId);

        return MapToDto(student);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CASO DE USO: Obtener Todos los Estudiantes
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Retorna la lista completa de estudiantes.
    /// </summary>
    public async Task<IReadOnlyList<StudentDto>> GetAllStudentsAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Student> students = await _studentRepository
            .GetAllAsync(cancellationToken);

        return students.Select(MapToDto).ToList().AsReadOnly();
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CASO DE USO: Dashboard de Progreso
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Genera el resumen de progreso para el Dashboard del estudiante.
    /// </summary>
    public async Task<StudentDashboardDto> GetDashboardAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        Student? student = await _studentRepository
            .GetByIdAsync(studentId, cancellationToken);

        if (student is null)
            throw new StudentNotFoundException(studentId);

        string proficiencyLevel = CalculateProficiencyLevel(student.CurrentLevelScore);
        double progressPercentage = CalculateProgressPercentage(student.CurrentLevelScore);

        double avgPracticeGrade = student.Practices.Any() 
            ? student.Practices.Average(p => p.Grade) 
            : 0;

        var practiceHistory = student.Practices
            .OrderByDescending(p => p.CompletedAt)
            .Select(p => new PracticeDto(p.Grade, p.PracticeType, p.CompletedAt))
            .ToList();

        return new StudentDashboardDto(
            student.Id,
            student.FullName,
            student.CurrentLevelScore,
            student.TotalLessonsCompleted,
            student.TotalPracticeMinutes,
            proficiencyLevel,
            progressPercentage,
            avgPracticeGrade,
            practiceHistory);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CASO DE USO: Sumar Progreso (Nueva Lección / Práctica) [Legacy/Combined]
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Suma puntos, lecciones y minutos al progreso de un estudiante existente
    /// y persiste el cambio en la base de datos.
    /// Lanza <see cref="StudentNotFoundException"/> si el ID no existe.
    /// </summary>
    public async Task<StudentDashboardDto> AddProgressAsync(
        Guid studentId,
        AddProgressCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        // GetByIdForUpdateAsync: carga CON tracking para que UpdateAsync pueda persistir
        Student? student = await _studentRepository
            .GetByIdForUpdateAsync(studentId, cancellationToken);

        if (student is null)
            throw new StudentNotFoundException(studentId);

        // El dominio valida sus propias reglas (score >= 0, minutes > 0)
        // Registramos tantas "lecciones" como LessonsToAdd indica.
        // Si LessonsToAdd == 0, sólo sumamos puntos y minutos en una sola llamada.
        int lessons  = Math.Max(1, command.LessonsCompleted);
        int scorePerLesson   = command.Points   / lessons;
        int minutesPerLesson = Math.Max(1, command.PracticeMinutes / lessons);

        for (int i = 0; i < lessons; i++)
        {
            // Distribuir el puntaje restante en la última iteración
            int score = (i == lessons - 1)
                ? command.Points - (scorePerLesson * (lessons - 1))
                : scorePerLesson;

            student.CompleteLesson(score, minutesPerLesson);
        }

        await _studentRepository.SaveChangesAsync(cancellationToken);

        // Retornar el dashboard actualizado en la misma respuesta
        return await GetDashboardAsync(studentId, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CASO DE USO: Agregar Lección
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<StudentDashboardDto> AddLessonAsync(
        Guid studentId,
        AddLessonCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Student? student = await _studentRepository.GetByIdAsync(studentId, cancellationToken);
        if (student is null) throw new StudentNotFoundException(studentId);

        var lesson = Lesson.Create(studentId, command.LessonNumber, command.DurationMinutes);

        // Paso 1: solo agregamos la Lección nueva (genera un INSERT puro).
        student.RegisterLesson(lesson);
        await _studentRepository.SaveChangesAsync(cancellationToken);

        // Paso 2: ahora sí actualizamos los contadores del estudiante (genera un
        // UPDATE puro). Separarlo del INSERT anterior evita el
        // DbUpdateConcurrencyException falso que lanza el proveedor de SQLite
        // cuando ambos comandos van juntos en el mismo SaveChanges.
        student.ApplyLessonProgress(lesson);
        await _studentRepository.SaveChangesAsync(cancellationToken);

        return await GetDashboardAsync(studentId, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CASO DE USO: Agregar Práctica
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<StudentDashboardDto> AddPracticeAsync(
        Guid studentId,
        AddPracticeCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        Student? student = await _studentRepository.GetByIdAsync(studentId, cancellationToken);
        if (student is null) throw new StudentNotFoundException(studentId);

        var practice = Practice.Create(studentId, command.Grade, command.PracticeType);

        // Mismo patrón que AddLessonAsync: separar el INSERT de la Práctica del
        // UPDATE de puntaje del estudiante en dos SaveChanges distintos.
        student.RegisterPractice(practice);
        await _studentRepository.SaveChangesAsync(cancellationToken);

        student.ApplyPracticeProgress(practice);
        await _studentRepository.SaveChangesAsync(cancellationToken);

        return await GetDashboardAsync(studentId, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  CASO DE USO: Ascender de Nivel
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<StudentDashboardDto> LevelUpAsync(
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        Student? student = await _studentRepository.GetByIdForUpdateAsync(studentId, cancellationToken);
        if (student is null) throw new StudentNotFoundException(studentId);

        if (!student.CanLevelUp(out string reason))
        {
            throw new ArgumentException(reason);
        }

        student.LevelUp();
        await _studentRepository.SaveChangesAsync(cancellationToken);

        return await GetDashboardAsync(studentId, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  Métodos privados de soporte
    // ─────────────────────────────────────────────────────────────────────────

    private static StudentDto MapToDto(Student student) => new(
        student.Id,
        student.FullName,
        student.Email,
        student.NativeLanguage,
        student.CurrentLevelScore,
        student.TotalLessonsCompleted,
        student.TotalPracticeMinutes,
        student.RegisteredAt,
        student.LastActivityAt,
        student.IsActive,
        CalculateProficiencyLevel(student.CurrentLevelScore));

    private static string CalculateProficiencyLevel(int score) => score switch
    {
        < 100  => "A1 - Principiante",
        < 250  => "A2 - Básico",
        < 500  => "B1 - Intermedio",
        < 800  => "B2 - Intermedio Alto",
        < 1100 => "C1 - Avanzado",
        _      => "C2 - Maestría"
    };

    private static double CalculateProgressPercentage(int score)
    {
        const int MaxScore = 1200;
        return Math.Min(100.0, Math.Round((double)score / MaxScore * 100, 2));
    }

    // Hash simplificado para el proyecto (en producción usar BCrypt NuGet)
    private static string BCryptHashPassword(string password)
        => Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(password)));
}
