using EnglishMasterAI.Application.DTOs;
using EnglishMasterAI.Application.Services;
using EnglishMasterAI.Domain.Entities;
using EnglishMasterAI.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace EnglishMasterAI.API.Controllers;

/// <summary>
/// Endpoints públicos para que un estudiante nuevo se registre y suba su
/// comprobante de pago. No requiere autenticación.
/// </summary>
[ApiController]
[Route("api/registrations")]
[Produces("application/json")]
public class RegistrationsController : ControllerBase
{
    private readonly PendingRegistrationService _service;

    public RegistrationsController(PendingRegistrationService service)
    {
        _service = service;
    }

    /// <summary>
    /// GET /api/registrations/price/{level} — Devuelve el precio de un nivel.
    /// El frontend lo usa para mostrar el monto antes de pagar.
    /// </summary>
    [HttpGet("price/{level}")]
    [ProducesResponseType(typeof(decimal), StatusCodes.Status200OK)]
    public ActionResult<decimal> GetPrice(string level)
    {
        try
        {
            return Ok(LevelPricing.GetPrice(level));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Nivel inválido", Detail = ex.Message, Status = 400 });
        }
    }

    /// <summary>
    /// POST /api/registrations — Crea una solicitud de registro pendiente de
    /// verificación de pago. El estudiante NO queda activo hasta que un
    /// administrador la apruebe.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(PendingRegistrationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PendingRegistrationDto>> Create(
        [FromBody] CreatePendingRegistrationCommand command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.CreateAsync(command, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, result);
        }
        catch (DuplicateEmailException ex)
        {
            return Conflict(new ProblemDetails { Title = "Correo duplicado", Detail = ex.Message, Status = 409 });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new ProblemDetails { Title = "Datos inválidos", Detail = ex.Message, Status = 400 });
        }
    }
}