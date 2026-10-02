namespace EmployeeManagementApi.Common;

// Formato estándar de error para todas las respuestas de la API.
public class ErrorResponse
{
    public int StatusCode { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Details { get; set; }
    public IDictionary<string, string[]>? ValidationErrors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string? TraceId { get; set; }
}
