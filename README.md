# Sistema de Gestión de Empleados — API

![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)
![EF Core](https://img.shields.io/badge/EF%20Core-8-512BD4)
![SQL Server](https://img.shields.io/badge/SQL%20Server-Azure%20SQL-CC2927?logo=microsoftsqlserver&logoColor=white)
![JWT](https://img.shields.io/badge/Auth-JWT-000000?logo=jsonwebtokens&logoColor=white)
![Tests](https://img.shields.io/badge/tests-xUnit-green)
![Docker](https://img.shields.io/badge/Docker-ready-2496ED?logo=docker&logoColor=white)

API REST para la administración de empleados y departamentos de una empresa: alta, baja, búsqueda avanzada, reportes, autenticación con roles y auditoría completa de cambios.

**🔗 Demo en vivo:** [Frontend](https://employeemanagementfrontend-grq5.onrender.com) · **API y Swagger:** [Backend](https://employeemanagementapi-vyx6.onrender.com)
**📦 Repositorio del frontend:** [EmployeeManagementFrontend](https://github.com/AaronChavezMtz/EmployeeManagementFrontend)

> **Acceso de prueba para revisión:**
>
> | Rol | Usuario | Contraseña | Permisos |
> |---|---|---|---|
> | Administrador | `adminprueba` | `123_Prueba` | Acceso completo: crear, editar y eliminar |
> | Empleado | `usuariolector` | `lector_123` | Portal del empleado: solo su ficha, su historial y su teléfono |
>
> *Nota: el servicio gratuito de Render puede tardar ~30-60 s en responder la primera vez (arranque en frío). Los datos de la demo son ficticios; no registres información real. Las cuentas de demo están protegidas (no se pueden desactivar ni cambiar de rol) y el dueño de la demo la restaura periódicamente.*

---

## Descripción

Backend de un sistema de recursos humanos que centraliza la información de la plantilla de una empresa. Resuelve tres necesidades concretas:

1. **Gestión**: administrar empleados y departamentos con reglas de negocio validadas en servidor.
2. **Trazabilidad**: saber quién cambió qué y cuándo (salario, departamento, estado).
3. **Análisis**: KPIs de plantilla y rotación, con reportes descargables en Excel y PDF.

Está diseñado como par del [frontend en React](https://employeemanagementfrontend-grq5.onrender.com) que lo consume, y desplegado en la nube (Render + Azure SQL Database).

## Arquitectura

```mermaid
flowchart LR
    A[Cliente React] -->|HTTPS + JWT| B[Controllers]
    B --> C[Services<br/>lógica de negocio]
    C --> D[EF Core<br/>AppDbContext]
    D --> E[(Azure SQL Database)]
    C -. vw_DepartmentSummary .-> E
    B --> F[Middleware de errores]
    B --> G[Rate limiting]
    B --> H[Serilog]
```

Arquitectura en capas: los **controllers** solo manejan HTTP, los **services** concentran la lógica de negocio y los **DTOs** definen el contrato público, de modo que las entidades de EF Core nunca se exponen.

## Funcionalidades

- **CRUD completo** de empleados y departamentos, con DTOs separados de las entidades
- **Búsqueda y filtros**: por nombre/correo/puesto, departamento, estado, rango de salario, rango de fechas de contratación, con orden y paginación
- **Autenticación JWT** con dos roles: `Admin` (RH, acceso completo) y `Employee` (portal del empleado)
- **Portal del empleado**: cada usuario `Employee` está vinculado a su ficha y solo ve su propia información; puede actualizar su teléfono
- **Vacaciones y ausencias**: el empleado solicita vacaciones, incapacidad o permiso; RH aprueba o rechaza. El saldo de vacaciones sigue la LFT (reforma 2023: 12 días el primer año, +2 por año hasta 20, luego +2 cada 5 años) por periodo de aniversario, y cuenta solo días hábiles
- **Historial de cambios**: cada alta, cambio de departamento, de salario, baja o recontratación queda registrado con quién lo hizo y cuándo
- **Bajas y recontratación**: la baja guarda fecha y motivo (renuncia, despido, término de contrato, otro); un empleado dado de baja se puede recontratar
- **Gestión de usuarios** desde la propia API (crear, cambiar rol, activar/desactivar)
- **Dashboard de KPIs**: plantilla activa, rotación de los últimos 12 meses (bajas / plantilla promedio), antigüedad promedio, bajas por motivo y tendencia mensual de altas y bajas
- **Reportes exportables** a Excel (ClosedXML) y PDF (QuestPDF)
- **Vista SQL** (`vw_DepartmentSummary`) consumida desde EF Core como entidad sin clave
- **Logging estructurado** con Serilog y **rate limiting** (protección extra en `/api/auth/login`)
- **Health check** (`/health`) para monitoreo del servicio
- **Manejo de errores centralizado** (middleware propio, respuestas JSON consistentes)
- **Swagger/OpenAPI** con soporte de autenticación Bearer
- **Pruebas unitarias** con xUnit, Moq y EF Core InMemory

## Decisiones técnicas

| Decisión | Motivo |
|---|---|
| **DTOs en lugar de exponer entidades** | Evita el *over-posting*, desacopla el contrato público del esquema de base de datos y permite evolucionar ambos por separado. |
| **Capa de Services** | Mantiene los controllers delgados y la lógica de negocio testeable sin levantar HTTP. |
| **Atributo de validación propio (`MinimumAge`)** | La regla de edad mínima se declara una vez sobre el DTO y se reutiliza, con pruebas unitarias dedicadas. |
| **Vista SQL para resumen de departamentos** | Agregaciones de solo lectura reutilizables y fáciles de consultar desde EF Core. |
| **BCrypt para contraseñas** | Hash con *salt* y factor de costo configurable; nunca se almacenan contraseñas en texto plano. |
| **Middleware de errores propio** | Respuestas JSON uniformes y sin filtrar detalles internos al cliente. |
| **Migraciones automáticas + seed del admin** | Un despliegue nuevo queda operativo sin pasos manuales sobre la base. |
| **Serilog** | Logs estructurados, listos para enviarse a cualquier *sink* (archivo, consola, Seq, etc.). |

## Modelo de datos

> `ChangeType` es un enum (`Created`, `Updated`, `DepartmentChanged`, `SalaryChanged`, `Activated`, `Deactivated`) y `Role` otro (`Employee`, `Admin`). `USER.EmployeeId` vincula una cuenta `Employee` con su ficha (único, opcional para `Admin`). `ChangedBy` guarda el *username* de quien hizo el cambio, por eso no es una llave foránea hacia `USER`: el historial se conserva aunque la cuenta cambie o se desactive.

```mermaid
erDiagram
    DEPARTMENT ||--o{ EMPLOYEE : tiene
    EMPLOYEE ||--o{ EMPLOYEE_HISTORY : registra
    EMPLOYEE ||--o| USER : "accede como"
    EMPLOYEE ||--o{ LEAVE_REQUEST : solicita

    DEPARTMENT {
        int Id PK
        string Name
        string Description
        datetime CreatedAt
    }
    EMPLOYEE {
        int Id PK
        string FirstName
        string LastName
        string Email
        string Phone
        string Position
        decimal Salary
        date HireDate
        date BirthDate
        bool IsActive
        datetime CreatedAt
        datetime UpdatedAt
        date TerminationDate
        enum TerminationReason
        int DepartmentId FK
    }
    EMPLOYEE_HISTORY {
        int Id PK
        int EmployeeId FK
        enum ChangeType
        string OldValue
        string NewValue
        string ChangedBy
        datetime ChangedAtUtc
    }
    LEAVE_REQUEST {
        int Id PK
        int EmployeeId FK
        enum Type
        date StartDate
        date EndDate
        int Days
        enum Status
        string ReviewedBy
        string ReviewComment
    }
    USER {
        int Id PK
        int EmployeeId FK
        string Username
        string Email
        string PasswordHash
        enum Role
        bool IsActive
        datetime CreatedAt
    }
```

## Objetos SQL

Un objeto vive directamente en SQL Server (script en `Database/schema.sql`) porque EF Core no lo gestiona:

**Vista `vw_DepartmentSummary`** — resumen por departamento (total de empleados, activos, salario promedio y nómina total de los activos). Usa `LEFT JOIN`, por lo que los departamentos sin empleados también aparecen. En EF Core se mapea como entidad sin clave (*keyless*): `DepartmentSummaryView`.

```sql
SELECT d.Id AS DepartmentId, d.Name AS DepartmentName,
       COUNT(e.Id) AS TotalEmployees,
       SUM(CASE WHEN e.IsActive = 1 THEN 1 ELSE 0 END) AS ActiveEmployees,
       ISNULL(AVG(CASE WHEN e.IsActive = 1 THEN e.Salary END), 0) AS AverageSalary,
       ISNULL(SUM(CASE WHEN e.IsActive = 1 THEN e.Salary END), 0) AS TotalPayroll
FROM dbo.Departments d
LEFT JOIN dbo.Employees e ON e.DepartmentId = d.Id
GROUP BY d.Id, d.Name;
```

## Endpoints principales

La referencia completa e interactiva está en [Swagger](https://employeemanagementapi-vyx6.onrender.com).

| Método | Ruta | Descripción | Acceso |
|---|---|---|---|
| `POST` | `/api/Auth/login` | Inicia sesión y devuelve un token JWT | Público |
| `POST` | `/api/Auth/register` | Crea un usuario (`employeeId` obligatorio si el rol es `Employee`) | Admin |
| `GET` | `/api/Users` | Lista los usuarios del sistema | Admin |
| `PUT` | `/api/Users/{id}` | Cambia el rol, el estado activo o el empleado vinculado | Admin |
| `GET` | `/api/me` | Ficha del empleado vinculado al usuario actual | Employee |
| `GET` | `/api/me/history` | Historial del empleado actual | Employee |
| `PUT` | `/api/me/phone` | Actualiza el teléfono del empleado actual | Employee |
| `GET` | `/api/me/vacation-balance` | Saldo de vacaciones del periodo de aniversario actual | Employee |
| `GET` | `/api/me/leave-requests` | Solicitudes de ausencia propias | Employee |
| `POST` | `/api/me/leave-requests` | Solicita vacaciones, incapacidad o permiso | Employee |
| `POST` | `/api/me/leave-requests/{id}/cancel` | Cancela una solicitud pendiente, o aprobada que aún no empieza | Employee |
| `GET` | `/api/leave-requests?status=Pending` | Bandeja de solicitudes (pendientes primero) | Admin |
| `POST` | `/api/leave-requests/{id}/approve` | Aprueba (revalida el saldo de vacaciones) | Admin |
| `POST` | `/api/leave-requests/{id}/reject` | Rechaza con comentario obligatorio | Admin |
| `GET` | `/api/Employees` | Lista con búsqueda, filtros, orden y paginación | Admin |
| `GET` | `/api/Employees/{id}` | Detalle de un empleado | Admin |
| `GET` | `/api/Employees/{id}/history` | Bitácora de cambios (altas, departamento, salario, estado) | Admin |
| `POST` | `/api/Employees` | Crea un empleado | Admin |
| `PUT` | `/api/Employees/{id}` | Actualiza un empleado | Admin |
| `POST` | `/api/Employees/{id}/terminate` | Da de baja (baja lógica) con fecha y motivo | Admin |
| `POST` | `/api/Employees/{id}/rehire` | Recontrata a un empleado dado de baja | Admin |
| `GET` | `/api/Departments` | Lista departamentos con conteo de empleados activos | Admin |
| `GET` | `/api/Departments/{id}` | Detalle de un departamento | Admin |
| `GET` | `/api/Departments/summary` | Resumen por departamento (vista `vw_DepartmentSummary`) | Admin |
| `POST` | `/api/Departments` | Crea un departamento | Admin |
| `PUT` | `/api/Departments/{id}` | Actualiza un departamento | Admin |
| `DELETE` | `/api/Departments/{id}` | Elimina un departamento (solo si no tiene empleados asignados) | Admin |
| `GET` | `/api/Dashboard/kpis` | Plantilla, rotación de 12 meses, antigüedad, bajas por motivo y tendencia mensual | Admin |
| `GET` | `/api/Reports/employees/excel` | Exporta empleados a Excel (acepta los mismos filtros de búsqueda) | Admin |
| `GET` | `/api/Reports/departments/excel` | Exporta el resumen por departamento a Excel | Admin |
| `GET` | `/api/Reports/departments/pdf` | Exporta el resumen por departamento a PDF | Admin |
| `GET` | `/api/Demo` | Indica si el usuario actual puede reiniciar la demo | Admin |
| `POST` | `/api/Demo/reset` | Restaura los datos de la demo (solo con `Demo__Enabled=true`; 2 veces cada 5 min) | Admin inicial y superusuario |
| `GET` | `/health` | Health check del servicio | Público |

Ejemplo de búsqueda con filtros:

```
GET /api/Employees?search=garcia&departmentId=3&isActive=true&minSalary=15000&sortBy=hireDate&pageNumber=1&pageSize=20
```

## Seguridad

- Autenticación **JWT Bearer** con expiración configurable; en cada petición se comprueba que la cuenta siga activa y con el mismo rol (un token de una cuenta desactivada o borrada deja de servir al instante)
- **Autorización por rol** aplicada en el servidor (el frontend solo oculta acciones; la API las bloquea)
- Contraseñas hasheadas con **BCrypt**; el login tarda lo mismo exista o no el usuario (evita enumerar cuentas)
- **Rate limiting** por IP real del cliente (lee `X-Forwarded-For` del proxy de Render): 100 peticiones/min en general y 5 intentos/min en `/api/auth/login`
- **CORS** restringido a `AllowedOrigins`; en producción, si está vacío no se permite ningún origen (solo en Development se permite cualquiera). CORS solo limita a los navegadores: la API sigue siendo accesible desde cualquier cliente, por eso todo lo sensible exige JWT
- **Cuentas protegidas**: el admin inicial, el superusuario y `Demo__ProtectedUsers` no se pueden modificar desde la API
- Cabeceras `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy` y HSTS fuera de Development
- El contenedor corre con un usuario sin privilegios (`USER $APP_UID`)
- Secretos fuera del repositorio (variables de entorno / `appsettings.Development.json` ignorado por Git)
- Errores controlados: el cliente nunca recibe *stack traces*

**Riesgos aceptados de la demo:** Swagger está expuesto en producción y hay credenciales de admin públicas (mitigado con cuentas protegidas y el reinicio de la demo).

## Stack

ASP.NET Core 8 · Entity Framework Core 8 · SQL Server · JWT (`Microsoft.AspNetCore.Authentication.JwtBearer`) · BCrypt.Net · Serilog · ClosedXML · QuestPDF · Swashbuckle · xUnit · Moq

## Estructura del proyecto

```
src/EmployeeManagementApi/
├── Controllers/       Endpoints HTTP (Employees, Departments, Auth, Users, Dashboard, Reports)
├── Services/          Lógica de negocio, separada de los controllers
├── DTOs/              Contratos de entrada/salida (nunca se exponen las entidades de EF)
├── Models/            Entidades de EF Core
├── Data/              AppDbContext y configuración Fluent API
├── Common/            Validaciones propias, manejo de errores, paginación
├── Middleware/        Middleware de manejo de errores
└── Database/          Script SQL de la vista de resumen
tests/EmployeeManagementApi.Tests/
├── Common/            MinimumAgeAttributeTests
├── Controllers/       EmployeesControllerTests
├── Services/          Pruebas de servicios + FakeHttpContextAccessor (simula el usuario autenticado)
└── TestDbContextFactory.cs      Crea un DbContext InMemory aislado por prueba
```

## Cómo correrlo en local

### Requisitos

- .NET 8 SDK
- SQL Server (local, en Docker o Azure SQL)

### Pasos

```bash
git clone https://github.com/AaronChavezMtz/EmployeeManagementApi.git
cd EmployeeManagementApi/src/EmployeeManagementApi
```

Configura `appsettings.Development.json` con tu cadena de conexión y un `Jwt:SecretKey` propio (32+ caracteres):

```json
{
  "ConnectionStrings": { "DefaultConnection": "..." },
  "Jwt": { "Issuer": "...", "Audience": "...", "SecretKey": "...", "ExpiryMinutes": 120 },
  "InitialAdmin": { "Username": "admin", "Email": "...", "Password": "..." }
}
```

Corre la app:

```bash
dotnet run
```

La app aplica las migraciones pendientes automáticamente al arrancar y crea (o actualiza la contraseña de) el usuario `Admin` definido en `InitialAdmin`, y el de `SuperAdmin` si está configurado.

Luego ejecuta `Database/schema.sql` una vez, conectado a tu base, para crear la vista (EF Core no la gestiona).

Swagger queda disponible en la raíz: `http://localhost:8080`

### Todo con Docker

`docker-compose.yml` levanta la API y SQL Server ya configurados (credenciales solo para uso local):

```bash
docker compose up --build
```

## Variables de entorno

| Variable | Descripción |
|---|---|
| `ConnectionStrings__DefaultConnection` | Cadena de conexión a SQL Server |
| `Jwt__SecretKey` | Clave de firma del token (32+ caracteres) |
| `Jwt__Issuer` / `Jwt__Audience` | Emisor y audiencia del token |
| `Jwt__ExpiryMinutes` | Vigencia del token en minutos |
| `InitialAdmin__Username` / `__Email` / `__Password` | Admin privado y protegido; se crea al arrancar y su contraseña se sincroniza con la variable en cada arranque. Junto con el superusuario, es el único que puede reiniciar la demo |
| `AllowedOrigins` | Dominios del frontend permitidos por CORS, separados por coma (por defecto, el frontend de la demo) |
| `SuperAdmin__Username` / `__Email` / `__Password` | Segundo admin privado y protegido, opcional |
| `Demo__Enabled` | `true` habilita `POST /api/Demo/reset` |
| `Demo__ProtectedUsers` | Cuentas que no se pueden modificar y que el reinicio no borra, separadas por coma |

## Pruebas

```bash
dotnet test
```

Las pruebas usan EF Core InMemory (una base nueva por prueba, vía `TestDbContextFactory`) y Moq para aislar dependencias. Cubren:

| Área | Archivo | Qué valida |
|---|---|---|
| Validaciones | `MinimumAgeAttributeTests` | Regla de edad mínima del empleado |
| Controllers | `EmployeesControllerTests` | Respuestas HTTP y códigos de estado de los endpoints |
| Servicios | `EmployeeServiceTests` | Lógica de negocio de empleados |
| Servicios | `DepartmentServiceTests` | Lógica de negocio de departamentos |
| Servicios | `AuthServiceTests` | Autenticación y emisión de tokens |
| Servicios | `UserServiceTests` | Las cuentas protegidas no se pueden modificar |
| Servicios | `DemoServiceTests` | El reinicio conserva las cuentas protegidas y vuelve a sembrar los datos |

`FakeHttpContextAccessor` simula el usuario autenticado para probar el registro de quién realiza cada cambio.

## Despliegue

Pensado para correr como contenedor Docker (`Dockerfile` incluido) en Render, con la base de datos en Azure SQL Database. Define las variables de la sección anterior en el panel del servicio.

## Roadmap

- [ ] Pipeline de CI con GitHub Actions (build + tests en cada PR)
- [ ] Refresh tokens
- [ ] Pruebas de integración con `WebApplicationFactory`
- [ ] Caché del dashboard

## Autor

**Aarón Yosef Chávez Martínez** · [GitHub](https://github.com/AaronChavezMtz) · [LinkedIn](https://www.linkedin.com/in/aaron-chavez-99bbb8393)

## Licencia

Distribuido bajo la licencia MIT — ver [LICENSE](LICENSE).