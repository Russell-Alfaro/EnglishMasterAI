namespace EnglishMasterAI.Domain.Entities;

/// <summary>
/// Entidad principal del dominio que representa a un estudiante.
/// Siguiendo DDD, la entidad encapsula sus propias reglas de negocio.
/// </summary>
public class Student
{
    // Propiedades con setters privados para proteger el estado del dominio
    public Guid Id { get; private set; }
    public string FullName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string NativeLanguage { get; private set; } = string.Empty;
    public int CurrentLevelScore { get; private set; }
    public int TotalLessonsCompleted { get; private set; }
    public int TotalPracticeMinutes { get; private set; }
    public DateTime RegisteredAt { get; private set; }
    public DateTime? LastActivityAt { get; private set; }
    public bool IsActive { get; private set; }
    public string InitialLevel { get; private set; } = string.Empty;

    // Navegación
    private readonly List<Lesson> _lessons = new();
    public IReadOnlyList<Lesson> Lessons => _lessons.AsReadOnly();

    private readonly List<Practice> _practices = new();
    public IReadOnlyList<Practice> Practices => _practices.AsReadOnly();

    // Constructor privado para EF Core
    private Student() { }

    /// <summary>
    /// Fábrica estática que aplica las reglas de negocio para crear un nuevo estudiante.
    /// </summary>
    public static Student Create(
        string fullName,
        string email,
        string passwordHash,
        string nativeLanguage = "Spanish",
        string initialLevel = "A1")
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException("El nombre completo es obligatorio.", nameof(fullName));

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("El correo electrónico no es válido.", nameof(email));

        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("La contraseña es obligatoria.", nameof(passwordHash));

        int startingScore = initialLevel.ToUpper() switch
        {
            "A2" => 100,
            "B1" => 250,
            "B2" => 500,
            "C1" => 800,
            "C2" => 1100,
            _    => 0 // A1 o no reconocido
        };

        return new Student
        {
            Id = Guid.NewGuid(),
            FullName = fullName.Trim(),
            Email = email.Trim().ToLowerInvariant(),
            PasswordHash = passwordHash,
            NativeLanguage = nativeLanguage,
            InitialLevel = initialLevel,
            CurrentLevelScore = startingScore,
            TotalLessonsCompleted = 0,
            TotalPracticeMinutes = 0,
            RegisteredAt = DateTime.UtcNow,
            LastActivityAt = null,
            IsActive = true
        };
    }

    /// <summary>
    /// Registra la finalización de una lección y actualiza el progreso.
    /// </summary>
    public void CompleteLesson(int scoreGained, int minutesPracticed)
    {
        if (scoreGained < 0) throw new ArgumentException("El puntaje no puede ser negativo.");
        if (minutesPracticed <= 0) throw new ArgumentException("Los minutos deben ser positivos.");

        CurrentLevelScore += scoreGained;
        TotalLessonsCompleted += 1;
        TotalPracticeMinutes += minutesPracticed;
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Desactiva la cuenta del estudiante.
    /// </summary>
    public void Deactivate() => IsActive = false;

    public void AddLesson(Lesson lesson)
    {
        _lessons.Add(lesson);
        TotalLessonsCompleted++;
        TotalPracticeMinutes += lesson.DurationMinutes;
        CurrentLevelScore += 50; // Supongamos 50 pts por lección base
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Paso 1 de 2: solo agrega la lección a la colección (genera un INSERT puro
    /// al guardar). Ver <see cref="ApplyLessonProgress"/> para el paso 2.
    /// </summary>
    public void RegisterLesson(Lesson lesson) => _lessons.Add(lesson);

    /// <summary>
    /// Paso 2 de 2: actualiza los contadores agregados del estudiante
    /// (genera un UPDATE puro al guardar).
    /// </summary>
    public void ApplyLessonProgress(Lesson lesson)
    {
        TotalLessonsCompleted++;
        TotalPracticeMinutes += lesson.DurationMinutes;
        CurrentLevelScore += 50;
        LastActivityAt = DateTime.UtcNow;
    }

    public void AddPractice(Practice practice)
    {
        _practices.Add(practice);
        CurrentLevelScore += practice.Grade * 5; // Supongamos 5 pts por punto de nota
        LastActivityAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Paso 1 de 2: solo agrega la práctica a la colección (INSERT puro).
    /// </summary>
    public void RegisterPractice(Practice practice) => _practices.Add(practice);

    /// <summary>
    /// Paso 2 de 2: actualiza el puntaje del estudiante (UPDATE puro).
    /// </summary>
    public void ApplyPracticeProgress(Practice practice)
    {
        CurrentLevelScore += practice.Grade * 5;
        LastActivityAt = DateTime.UtcNow;
    }

    public bool CanLevelUp(out string reason)
    {
        if (_lessons.Count < 8)
        {
            reason = $"Se requieren al menos 8 lecciones. Actual: {_lessons.Count}.";
            return false;
        }

        if (!_practices.Any())
        {
            reason = "No hay prácticas registradas para evaluar la nota.";
            return false;
        }

        double averageGrade = _practices.Average(p => p.Grade);
        if (averageGrade <= 13)
        {
            reason = $"El promedio de notas debe ser estrictamente mayor a 13. Actual: {averageGrade:F1}.";
            return false;
        }

        reason = "Cumple con los requisitos.";
        return true;
    }

    public void LevelUp()
    {
        if (!CanLevelUp(out string reason))
            throw new InvalidOperationException($"No se puede ascender: {reason}");

        string currentCEFR = CalculateCurrentCEFR();
        string nextCEFR = currentCEFR switch
        {
            "A1" => "A2",
            "A2" => "B1",
            "B1" => "B2",
            "B2" => "C1",
            "C1" => "C2",
            _ => currentCEFR
        };

        if (currentCEFR == nextCEFR) return;

        // Ascender actualizando el score al base del nuevo nivel si es necesario
        int newBaseScore = nextCEFR switch
        {
            "A2" => 100,
            "B1" => 250,
            "B2" => 500,
            "C1" => 800,
            "C2" => 1100,
            _ => CurrentLevelScore
        };

        if (CurrentLevelScore < newBaseScore)
        {
            CurrentLevelScore = newBaseScore;
        }
    }

    private string CalculateCurrentCEFR()
    {
        return CurrentLevelScore switch
        {
            < 100  => "A1",
            < 250  => "A2",
            < 500  => "B1",
            < 800  => "B2",
            < 1100 => "C1",
            _      => "C2"
        };
    }
}
