using EnglishMasterAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EnglishMasterAI.Infrastructure.Persistence;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Student> Students => Set<Student>();
    public DbSet<Lesson> Lessons => Set<Lesson>();
    public DbSet<Practice> Practices => Set<Practice>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        modelBuilder.Entity<Student>()
            .HasMany(s => s.Lessons)
            .WithOne()
            .HasForeignKey(l => l.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Student>()
            .HasMany(s => s.Practices)
            .WithOne()
            .HasForeignKey(p => p.StudentId)
            .OnDelete(DeleteBehavior.Cascade);

        // IMPORTANTE: igual que Student.Id, el Id de Lesson y Practice lo genera
        // el dominio (Guid.NewGuid() en los métodos Create), NO la base de datos.
        // Sin ValueGeneratedNever(), EF Core asume que un Guid ya asignado
        // significa "esta fila ya existe" y genera un UPDATE en vez de un INSERT
        // para entidades nuevas — eso causaba el error al agregar Lecciones/Prácticas.
        modelBuilder.Entity<Lesson>()
            .Property(l => l.Id)
            .ValueGeneratedNever();

        modelBuilder.Entity<Practice>()
            .Property(p => p.Id)
            .ValueGeneratedNever();
    }
}