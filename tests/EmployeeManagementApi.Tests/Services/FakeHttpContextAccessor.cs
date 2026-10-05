using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace EmployeeManagementApi.Tests.Services;

// Simula un usuario autenticado para que EmployeeService pueda resolver "quién hizo el cambio"
// sin necesitar un pipeline HTTP real en las pruebas.
public static class FakeHttpContextAccessor
{
    public static IHttpContextAccessor Create(string username = "test-user")
    {
        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, username) }, "TestAuth");
        var context = new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
        return new HttpContextAccessor { HttpContext = context };
    }
}
