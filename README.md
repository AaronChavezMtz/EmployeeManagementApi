# Sistema de Gestión de Empleados — API

API REST para la administración de empleados y departamentos de una empresa: alta, baja, búsqueda, filtros, reportes, autenticación con roles y auditoría de cambios.

```
ASP.NET Core Web API
        ↓
Entity Framework Core
        ↓
SQL Server (Azure SQL Database)
```

Proyecto de portafolio — backend construido para practicar un stack .NET + SQL Server de nivel profesional, pensado también como par del [frontend en React](https://employeemanagementfrontend-grq5.onrender.com) que lo consume.

## Funcionalidades

- **CRUD completo** de empleados y departamentos, con DTOs separados de las entidades
- **Búsqueda y filtros**: por nombre/correo/puesto, departamento, estado, rango de salario, rango de fechas de contratación, con orden y paginación
- **Autenticación JWT** con dos roles: `Admin` (acceso completo) y `Viewer` (solo lectura)
- **Historial de cambios**: cada alta, cambio de departamento, de salario o de estado queda registrado con quién lo hizo y cuándo
- **Gestión de usuarios** desde la propia API (crear, cambiar rol, activar/desactivar)
- **Dashboard de KPIs**: plantilla activa, tasa de rotación, tendencia de contrataciones de los últimos 12 meses
- **Reportes exportables** a Excel (ClosedXML) y PDF (QuestPDF)
- **Procedimiento almacenado** (`sp_SearchEmployees`) y **vista SQL** (`vw_DepartmentSummary`) usados desde EF Core
- **Logging estructurado** con Serilog y **rate limiting** (protección extra en `/api/auth/login`)
- **Manejo de errores centralizado** (middleware propio, respuestas JSON consistentes)
- **Swagger/OpenAPI** con soporte de autenticación Bearer
- **Pruebas unitarias** con xUnit, Moq y EF Core InMemory

## Stack

ASP.NET Core 8 · Entity Framework Core 8 · SQL Server · JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`) · BCrypt.Net · Serilog · ClosedXML · QuestPDF · Swashbuckle · xUnit

## Estructura del proyecto

```
src/EmployeeManagementApi/
├── Controllers/       Endpoints HTTP (Employees, Departments, Auth, Users, Dashboard, Reports)
├── Services/           Lógica de negocio, separada de los controllers
├── DTOs/                Contratos de entrada/salida (nunca se exponen las entidades de EF)
├── Models/              Entidades de EF Core
├── Data/                 AppDbContext y configuración Fluent API
├── Common/             Validaciones propias, manejo de errores, paginación
├── Middleware/        Middleware de manejo de errores
└── Database/           Script SQL con el procedimiento almacenado y la vista
tests/EmployeeManagementApi.Tests/   Pruebas unitarias (xUnit)
```

## Cómo correrlo en local

### Requisitos
- .NET 8 SDK
- SQL Server (local, en Docker, o Azure SQL)

### Pasos

```bash
cd src/EmployeeManagementApi
```

Configura `appsettings.Development.json` con tu cadena de conexión y un `Jwt:SecretKey` propio (32+ caracteres):

```json
{
  "ConnectionStrings": { "DefaultConnection": "..." },
  "Jwt": { "Issuer": "...", "Audience": "...", "SecretKey": "...", "ExpiryMinutes": 120 },
  "InitialAdmin": { "Username": "admin", "Email": "...", "Password": "..." }
}
```

Aplica las migraciones y corre la app:

```bash
dotnet ef database update
dotnet run
```

La app aplica las migraciones pendientes automáticamente al arrancar, y siembra un usuario `Admin` inicial (con los datos de `InitialAdmin`) si la tabla `Users` está vacía.

Luego corre `Database/schema.sql` una vez contra tu base para crear el procedimiento almacenado y la vista (EF Core no gestiona esos objetos).

Swagger queda disponible en la raíz: `http://localhost:8080`

### Pruebas

```bash
dotnet test
```

## Despliegue

Pensado para correr como contenedor Docker (`Dockerfile` incluido) en un servicio como Render, con la base de datos en Azure SQL Database. Variables de entorno necesarias en producción:

```
ConnectionStrings__DefaultConnection
Jwt__SecretKey / Jwt__Issuer / Jwt__Audience / Jwt__ExpiryMinutes
InitialAdmin__Username / InitialAdmin__Email / InitialAdmin__Password
AllowedOrigins   (dominios del frontend permitidos por CORS)
```
