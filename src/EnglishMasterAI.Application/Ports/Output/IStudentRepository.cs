using EnglishMasterAI.Domain.Entities;

namespace EnglishMasterAI.Application.Ports.Output;

/// <summary>
/// Puerto de salida (Output Port) en la arquitectura hexagonal.
/// Define el contrato que cualquier adaptador de persistencia debe cumplir.
/// La capa de Application NO conoce la implementación concreta (EF Core, dapper, etc.)
/// </summary>
public interface IStudentRepository
{
    /// <summary>
    /// Obtiene un estudiante por su identificador único (solo lectura).
    /// </summary>
    Task<Student?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un estudiante con tracking activo, listo para ser actualizado.
    /// </summary>
    Task<Student?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene un estudiante por su correo electrónico.
    /// </summary>
    Task<Student?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Obtiene todos los estudiantes activos.
    /// </summary>
    Task<IReadOnlyList<Student>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persiste un nuevo estudiante en el repositorio.
    /// </summary>
    Task<Student> AddAsync(Student student, CancellationToken cancellationToken = default);

    /// <summary>
    /// Actualiza los datos de un estudiante existente.
    /// </summary>
    Task UpdateAsync(Student student, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifica si ya existe un estudiante con el email dado.
    /// </summary>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Guarda los cambios directamente en el contexto para entidades ya rastreadas.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
