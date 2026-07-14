using EnglishMasterAI.Application.DTOs;
using EnglishMasterAI.Application.Services;
using EnglishMasterAI.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace EnglishMasterAI.API.Controllers;

/// <summary>
/// Endpoints de administración: revisar comprobantes de pago y
/// aprobar/rechazar solicitudes de registro. Protegidos con una contraseña
/// simple enviada en el header "X-Admin-Password" — suficiente para un
/// proyecto de un solo administrador, sin necesidad de un sistema de login
/// completo con roles y JWT.
/// </summary>
[ApiController]
[Route("api/admin/registrations")]
[Produces("application/json")]
public class AdminRegistrationsController : ControllerBase
{
    private readonly PendingRegistrationService _service;
    private readonly IConfiguration _configuration;

    public AdminRegistrationsController(PendingRegistrationService service, IConfiguration configuration)
    {
        _service = service;
        _configuration = configuration;
    }

    /// <summary>
    /// Verifica el header X-Admin-Password contra la contraseña configurada.
    /// Devuelve null si es válida, o un ObjectResult 401 si no lo es.
    /// </summary>
    private ActionResult? CheckAdminPassword()
    {
        string? configuredPassword = _configuration["AdminPassword"];

        if (string.IsNullOrEmpty(configuredPassword))
        {
            return StatusCode(StatusCodes.Status500InternalServerError, new ProblemDetails
            {
                Title = "Configuración faltante",
                Detail = "El servidor no tiene configurada la variable AdminPassword.",
                Status = 500
            });
        }

        string? providedPassword = Request.Headers["X-Admin-Password"].FirstOrDefault();

        if (string.IsNullOrEmpty(providedPassword) || providedPassword != configuredPassword)
        {
            return Unauthorized(new ProblemDetails
            {
                Title = "No autorizado",
                Detail = "Contraseña de administrador incorrecta o faltante.",
                Status = 401
            });
        }

        return null;
    }

    /// <summary>
    /// GET /api/admin/registrations/pending — Lista las solicitudes pendientes.
    /// </summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<PendingRegistrationSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PendingRegistrationSummaryDto>>> GetPending(
        CancellationToken cancellationToken)
    {
        if (CheckAdminPassword() is ActionResult unauthorized) return unauthorized;

        var result = await _service.GetPendingAsync(cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// GET /api/admin/registrations/{id}/receipt — Devuelve la imagen del
    /// comprobante como archivo binario (para mostrarla en un &lt;img&gt;).
    /// </summary>
    [HttpGet("{id:guid}/receipt")]
    public async Task<IActionResult> GetReceipt(Guid id, CancellationToken cancellationToken)
    {
        if (CheckAdminPassword() is ActionResult unauthorized) return unauthorized;

        try
        {
            var (bytes, contentType) = await _service.GetReceiptImageAsync(id, cancellationToken);
            return File(bytes, string.IsNullOrWhiteSpace(contentType) ? "image/png" : contentType);
        }
        catch (PendingRegistrationNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "No encontrado", Detail = ex.Message, Status = 404 });
        }
    }

    /// <summary>
    /// POST /api/admin/registrations/{id}/approve — Aprueba y crea el Student real.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken cancellationToken)
    {
        if (CheckAdminPassword() is ActionResult unauthorized) return unauthorized;

        try
        {
            await _service.ApproveAsync(id, cancellationToken);
            return Ok(new { message = "Solicitud aprobada. El estudiante ya puede iniciar sesión." });
        }
        catch (PendingRegistrationNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "No encontrado", Detail = ex.Message, Status = 404 });
        }
        catch (DuplicateEmailException ex)
        {
            return Conflict(new ProblemDetails { Title = "Correo duplicado", Detail = ex.Message, Status = 409 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "No se puede aprobar", Detail = ex.Message, Status = 400 });
        }
    }

    /// <summary>
    /// POST /api/admin/registrations/{id}/reject — Rechaza la solicitud.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(
        Guid id, [FromBody] RejectRegistrationCommand command, CancellationToken cancellationToken)
    {
        if (CheckAdminPassword() is ActionResult unauthorized) return unauthorized;

        try
        {
            await _service.RejectAsync(id, command?.Reason, cancellationToken);
            return Ok(new { message = "Solicitud rechazada." });
        }
        catch (PendingRegistrationNotFoundException ex)
        {
            return NotFound(new ProblemDetails { Title = "No encontrado", Detail = ex.Message, Status = 404 });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ProblemDetails { Title = "No se puede rechazar", Detail = ex.Message, Status = 400 });
        }
    }
}