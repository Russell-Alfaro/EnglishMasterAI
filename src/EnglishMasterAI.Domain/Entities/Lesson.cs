namespace EnglishMasterAI.Domain.Entities;

public class Lesson
{
    public Guid Id { get; private set; }
    public Guid StudentId { get; private set; }
    public int LessonNumber { get; private set; }
    public int DurationMinutes { get; private set; }
    public DateTime CompletedAt { get; private set; }

    private Lesson() { }

    public static Lesson Create(Guid studentId, int lessonNumber, int durationMinutes)
    {
        if (lessonNumber <= 0)
            throw new ArgumentException("El número de lección debe ser mayor a cero.");
        if (durationMinutes <= 0)
            throw new ArgumentException("La duración debe ser mayor a cero.");

        return new Lesson
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            LessonNumber = lessonNumber,
            DurationMinutes = durationMinutes,
            CompletedAt = DateTime.UtcNow
        };
    }
}
