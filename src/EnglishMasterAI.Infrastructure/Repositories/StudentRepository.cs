using EnglishMasterAI.Application.Ports.Output;
using EnglishMasterAI.Domain.Entities;
using EnglishMasterAI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EnglishMasterAI.Infrastructure.Repositories;

/// <summary>
/// Adaptador de salida: implementación concreta de IStudentRepository usando EF Core + SQLite.
/// Esta clase es el "adaptador" que conecta el puerto de salida con la tecnología de persistencia.
/// </summary>
public class StudentRepository : IStudentRepository
{
    private readonly AppDbContext _context;

    public StudentRepository(AppDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc />
    public async Task<Student?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // AsNoTracking para consultas de solo lectura (GET)
        return await _context.Students
            .Include(s => s.Lessons)
            .Include(s => s.Practices)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <summary>
    /// Obtiene un estudiante con tracking activo para permitir actualizaciones.
    /// Usar solo cuando se va a llamar UpdateAsync a continuación.
    /// </summary>
    public async Task<Student?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        // SIN AsNoTracking — EF Core rastrea el objeto para SaveChanges
        return await _context.Students
            .Include(s => s.Lessons)
            .Include(s => s.Practices)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Student?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail = email.Trim().ToLowerInvariant();

        return await _context.Students
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Email == normalizedEmail, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Student>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Students
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderByDescending(s => s.RegisteredAt)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Student> AddAsync(
        Student student,
        CancellationToken cancellationToken = default)
    {
        await _context.Students.AddAsync(student, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        return student;
    }

    /// <inheritdoc />
    public async Task UpdateAsync(
        Student student,
        CancellationToken cancellationToken = default)
    {
        // Si la entidad ya está siendo rastreada, Update() la marca como Modified.
        // Si llegó sin tracking, EF Core la adjunta y marca todo como Modified.
        var entry = _context.Entry(student);
        if (entry.State == Microsoft.EntityFrameworkCore.EntityState.Detached)
        {
            _context.Students.Attach(student);
            entry.State = Microsoft.EntityFrameworkCore.EntityState.Modified;
        }
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExistsByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        string normalizedEmail = email.Trim().ToLowerInvariant();

        return await _context.Students
            .AnyAsync(s => s.Email == normalizedEmail, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
