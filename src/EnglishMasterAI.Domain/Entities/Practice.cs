namespace EnglishMasterAI.Domain.Entities;

public class Practice
{
    public Guid Id { get; private set; }
    public Guid StudentId { get; private set; }
    public int Grade { get; private set; }
    public string PracticeType { get; private set; } = string.Empty;
    public DateTime CompletedAt { get; private set; }

    private Practice() { }

    public static Practice Create(Guid studentId, int grade, string practiceType)
    {
        if (grade < 0 || grade > 20)
            throw new ArgumentException("La nota debe estar entre 0 y 20.");
        if (string.IsNullOrWhiteSpace(practiceType))
            throw new ArgumentException("El tipo de práctica es obligatorio.");

        return new Practice
        {
            Id = Guid.NewGuid(),
            StudentId = studentId,
            Grade = grade,
            PracticeType = practiceType,
            CompletedAt = DateTime.UtcNow
        };
    }
}
