using EnglishMasterAI.Application.Services;
using EnglishMasterAI.Application.Ports.Output;
using EnglishMasterAI.Infrastructure.Persistence;
using EnglishMasterAI.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Servicios ───────────────────────────────────────────────────────────────

// EF Core con PostgreSQL (Neon)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException(
            "Falta la cadena de conexión 'DefaultConnection'. Configúrala en appsettings.json " +
            "o en la variable de entorno ConnectionStrings__DefaultConnection.")));

// Inyección de dependencias: puertos de salida
builder.Services.AddScoped<IStudentRepository, StudentRepository>();
builder.Services.AddScoped<StudentService>();
builder.Services.AddScoped<IPendingRegistrationRepository, PendingRegistrationRepository>();
builder.Services.AddScoped<PendingRegistrationService>();

// Controladores + Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title       = "EnglishMasterAI API",
        Version     = "v1",
        Description = "API para la Plataforma de Aprendizaje de Inglés — Proyecto Universitario de QA"
    });
});

// CORS para Blazor WASM
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader()));

// ─── Pipeline HTTP ────────────────────────────────────────────────────────────

var app = builder.Build();

// Aplicar migraciones pendientes automáticamente al iniciar.
// IMPORTANTE: usamos Migrate() en vez de EnsureCreated() porque EnsureCreated()
// NO aplica migraciones nuevas si la base de datos ya existe — con Migrate()
// cualquier tabla/columna agregada en una migración futura se crea automáticamente.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    // Muestra el detalle completo de cualquier excepción no controlada
    // directamente en la respuesta HTTP (visible en el navegador / Swagger),
    // en vez de solo un "500" genérico sin información.
    app.UseDeveloperExceptionPage();

    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "EnglishMasterAI v1");
        c.RoutePrefix = string.Empty; // Swagger en la raíz
    });
}

app.UseCors();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Necesario para que los tests de integración puedan acceder al Program
public partial class Program { }