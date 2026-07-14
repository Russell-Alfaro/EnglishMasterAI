using EnglishMasterAI.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EnglishMasterAI.Infrastructure.Persistence.Configurations;

/// <summary>
/// Configuración Fluent API para la entidad Student.
/// Define el esquema de la tabla en SQLite.
/// </summary>
public class StudentConfiguration : IEntityTypeConfiguration<Student>
{
    public void Configure(EntityTypeBuilder<Student> builder)
    {
        builder.ToTable("Students");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id)
            .IsRequired()
            .ValueGeneratedNever(); // El ID lo genera el dominio

        builder.Property(s => s.FullName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(s => s.Email)
            .IsRequired()
            .HasMaxLength(320);

        // Índice único en Email para garantizar la integridad a nivel de base de datos
        builder.HasIndex(s => s.Email)
            .IsUnique()
            .HasDatabaseName("IX_Students_Email");

        builder.Property(s => s.PasswordHash)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(s => s.NativeLanguage)
            .IsRequired()
            .HasMaxLength(100)
            .HasDefaultValue("Spanish");

        builder.Property(s => s.CurrentLevelScore)
            .HasDefaultValue(0);

        builder.Property(s => s.TotalLessonsCompleted)
            .HasDefaultValue(0);

        builder.Property(s => s.TotalPracticeMinutes)
            .HasDefaultValue(0);

        builder.Property(s => s.RegisteredAt)
            .IsRequired();

        builder.Property(s => s.IsActive)
            .IsRequired()
            .HasDefaultValue(true);
    }
}
