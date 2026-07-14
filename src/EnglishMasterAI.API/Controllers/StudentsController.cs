using EnglishMasterAI.Application.DTOs;
using EnglishMasterAI.Application.Services;
using EnglishMasterAI.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace EnglishMasterAI.API.Controllers;

/// <summary>
/// Adaptador primario: expone los casos de uso de Student a través de HTTP REST.
/// Delega toda la lógica al StudentService; el controlador solo traduce HTTP ↔ Dominio.
/// </summary>
[ApiController]
[Route("api/students")]
[Produces("application/json")]
public class StudentsController : ControllerBase
{
    private readonly StudentService _studentService;
    private readonly ILogger<StudentsController> _logger;

    public StudentsController(
        StudentService studentService,
        ILogger<StudentsController> logger)
    {
        _studentService = studentService;
        _logger = logger;
    }

    /// <summary>
    /// POST /api/students — Registra un nuevo estudiante.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(StudentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StudentDto>> RegisterStudent(
        [FromBody] RegisterStudentCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Registrando estudiante con email: {Email}", command.Email);
            StudentDto result = await _studentService.RegisterStudentAsync(command, cancellationToken);

            return CreatedAtAction(
                nameof(GetStudentById),
                new { id = result.Id },
                result);
        }
        catch (DuplicateEmailException ex)
        {
            _logger.LogWarning("Intento de registro con email duplicado: {Email}", command.Email);
            return Conflict(new ProblemDetails
            {
                Title  = "Correo duplicado",
                Detail = ex.Message,
                Status = StatusCodes.Status409Conflict
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title  = "Datos inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }

    /// <summary>
    /// GET /api/students — Obtiene la lista de todos los estudiantes.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StudentDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<StudentDto>>> GetAllStudents(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<StudentDto> students =
            await _studentService.GetAllStudentsAsync(cancellationToken);

        return Ok(students);
    }

    /// <summary>
    /// GET /api/students/{id} — Obtiene un estudiante por ID.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(StudentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDto>> GetStudentById(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            StudentDto student = await _studentService.GetStudentByIdAsync(id, cancellationToken);
            return Ok(student);
        }
        catch (StudentNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title  = "Estudiante no encontrado",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// GET /api/students/{id}/dashboard — Retorna el dashboard de progreso.
    /// </summary>
    [HttpGet("{id:guid}/dashboard")]
    [ProducesResponseType(typeof(StudentDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<StudentDashboardDto>> GetDashboard(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            StudentDashboardDto dashboard =
                await _studentService.GetDashboardAsync(id, cancellationToken);
            return Ok(dashboard);
        }
        catch (StudentNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title  = "Estudiante no encontrado",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
    }

    /// <summary>
    /// PUT /api/students/{id}/add-progress — Suma puntos, lecciones y minutos al estudiante.
    /// Retorna el dashboard actualizado con el nuevo estado de progreso.
    /// </summary>
    [HttpPut("{id:guid}/add-progress")]
    [ProducesResponseType(typeof(StudentDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StudentDashboardDto>> AddProgress(
        Guid id,
        [FromBody] AddProgressCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation(
                "Sumando progreso al estudiante {Id}: +{Score} pts, +{Lessons} lecc, +{Min} min",
                id, command.Points, command.LessonsCompleted, command.PracticeMinutes);

            StudentDashboardDto updated =
                await _studentService.AddProgressAsync(id, command, cancellationToken);

            return Ok(updated);
        }
        catch (StudentNotFoundException ex)
        {
            return NotFound(new ProblemDetails
            {
                Title  = "Estudiante no encontrado",
                Detail = ex.Message,
                Status = StatusCodes.Status404NotFound
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails
            {
                Title  = "Datos inválidos",
                Detail = ex.Message,
                Status = StatusCodes.Status400BadRequest
            });
        }
    }
    // ─────────────────────────────────────────────────────────────────────────
    //  NUEVOS ENDPOINTS PARA LECCIONES, PRÁCTICAS Y ASCENSO
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// POST /api/students/{id}/lessons — Registra una nueva lección.
    /// </summary>
    [HttpPost("{id:guid}/lessons")]
    [ProducesResponseType(typeof(StudentDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StudentDashboardDto>> AddLesson(
        Guid id,
        [FromBody] AddLessonCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            StudentDashboardDto updated = await _studentService.AddLessonAsync(id, command, cancellationToken);
            return Ok(updated);
        }
        catch (StudentNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Estudiante no encontrado", Detail = ex.Message, Status = 404 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Datos inválidos", Detail = ex.Message, Status = 400 });
        }
    }

    /// <summary>
    /// POST /api/students/{id}/practices — Registra una nueva práctica.
    /// </summary>
    [HttpPost("{id:guid}/practices")]
    [ProducesResponseType(typeof(StudentDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StudentDashboardDto>> AddPractice(
        Guid id,
        [FromBody] AddPracticeCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            StudentDashboardDto updated = await _studentService.AddPracticeAsync(id, command, cancellationToken);
            return Ok(updated);
        }
        catch (StudentNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Estudiante no encontrado", Detail = ex.Message, Status = 404 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Datos inválidos", Detail = ex.Message, Status = 400 });
        }
    }

    /// <summary>
    /// POST /api/students/{id}/level-up — Intenta ascender al estudiante de nivel.
    /// </summary>
    [HttpPost("{id:guid}/level-up")]
    [ProducesResponseType(typeof(StudentDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<StudentDashboardDto>> LevelUp(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            StudentDashboardDto updated = await _studentService.LevelUpAsync(id, cancellationToken);
            return Ok(updated);
        }
        catch (StudentNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "Estudiante no encontrado", Detail = ex.Message, Status = 404 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Requisitos no cumplidos", Detail = ex.Message, Status = 400 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "No se puede ascender", Detail = ex.Message, Status = 400 });
        }
    }
}
