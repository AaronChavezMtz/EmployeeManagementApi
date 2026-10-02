using System.Net;
using System.Text.Json;
using EmployeeManagementApi.Common;
using Microsoft.EntityFrameworkCore;

namespace EmployeeManagementApi.Middleware;

// Middleware centralizado: captura toda excepción no manejada y la convierte
// en una respuesta JSON consistente (ErrorResponse), sin exponer stack traces.
public class ExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionMiddleware(RequestDelegate next, ILogger<ExceptionMiddleware> logger, IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = new ErrorResponse { TraceId = context.TraceIdentifier };

        var statusCode = exception switch
        {
            NotFoundException => HttpStatusCode.NotFound,
            BusinessRuleException => HttpStatusCode.Conflict,
            DbUpdateException => HttpStatusCode.Conflict,
            ArgumentException => HttpStatusCode.BadRequest,
            _ => HttpStatusCode.InternalServerError
        };

        response.StatusCode = (int)statusCode;
        response.Message = exception switch
        {
            NotFoundException or BusinessRuleException or ArgumentException => exception.Message,
            DbUpdateException => "No se pudo completar la operación por un conflicto de datos (posible duplicado o referencia inválida).",
            _ => "Ocurrió un error inesperado al procesar la solicitud."
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Error no controlado. TraceId: {TraceId}", context.TraceIdentifier);
            if (_env.IsDevelopment())
                response.Details = exception.ToString();
        }
        else
        {
            _logger.LogWarning("{ExceptionType}: {Message}", exception.GetType().Name, exception.Message);
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = response.StatusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
