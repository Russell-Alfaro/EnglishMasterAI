namespace EnglishMasterAI.Domain.Exceptions;

/// <summary>
/// Excepción de dominio lanzada cuando se intenta registrar un correo ya existente.
/// </summary>
public class DuplicateEmailException : DomainException
{
    public string Email { get; }

    public DuplicateEmailException(string email)
        : base($"El correo electrónico '{email}' ya está registrado en el sistema.")
    {
        Email = email;
    }
}

/// <summary>
/// Excepción de dominio base para el dominio de EnglishMasterAI.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception innerException) : base(message, innerException) { }
}

/// <summary>
/// Excepción lanzada cuando no se encuentra un estudiante por ID.
/// </summary>
public class StudentNotFoundException : DomainException
{
    public Guid StudentId { get; }

    public StudentNotFoundException(Guid studentId)
        : base($"No se encontró ningún estudiante con el ID '{studentId}'.")
    {
        StudentId = studentId;
    }
}
