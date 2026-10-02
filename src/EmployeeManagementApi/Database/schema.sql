/* =========================================================================
   Sistema de Gestión de Empleados
   Script complementario: procedimiento almacenado + vista.
   Ejecutar DESPUÉS de aplicar las migraciones de Entity Framework Core
   (las migraciones crean las tablas Departments y Employees).
   ========================================================================= */

USE EmployeeManagementDb;
GO

-- =========================================================================
-- VISTA: vw_DepartmentSummary
-- Resumen por departamento: total de empleados, activos, salario promedio
-- y nómina total. Se consume desde GET /api/departments/summary
-- =========================================================================
IF OBJECT_ID('dbo.vw_DepartmentSummary', 'V') IS NOT NULL
    DROP VIEW dbo.vw_DepartmentSummary;
GO

CREATE VIEW dbo.vw_DepartmentSummary AS
SELECT
    d.Id                                            AS DepartmentId,
    d.Name                                          AS DepartmentName,
    COUNT(e.Id)                                     AS TotalEmployees,
    SUM(CASE WHEN e.IsActive = 1 THEN 1 ELSE 0 END) AS ActiveEmployees,
    ISNULL(AVG(CASE WHEN e.IsActive = 1 THEN e.Salary END), 0)  AS AverageSalary,
    ISNULL(SUM(CASE WHEN e.IsActive = 1 THEN e.Salary END), 0)  AS TotalPayroll
FROM dbo.Departments d
LEFT JOIN dbo.Employees e ON e.DepartmentId = d.Id
GROUP BY d.Id, d.Name;
GO

-- =========================================================================
-- PROCEDIMIENTO ALMACENADO: sp_SearchEmployees
-- Búsqueda avanzada de empleados por término libre, departamento y estado.
-- Se consume desde GET /api/employees/search-sp
-- =========================================================================
IF OBJECT_ID('dbo.sp_SearchEmployees', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SearchEmployees;
GO

CREATE PROCEDURE dbo.sp_SearchEmployees
    @SearchTerm   NVARCHAR(150) = NULL,
    @DepartmentId INT           = NULL,
    @IsActive     BIT           = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        e.Id, e.FirstName, e.LastName, e.Email, e.Phone, e.Position,
        e.Salary, e.HireDate, e.BirthDate, e.IsActive, e.CreatedAt,
        e.UpdatedAt, e.DepartmentId
    FROM dbo.Employees e
    WHERE
        (@SearchTerm IS NULL OR
            e.FirstName LIKE '%' + @SearchTerm + '%' OR
            e.LastName  LIKE '%' + @SearchTerm + '%' OR
            e.Email     LIKE '%' + @SearchTerm + '%' OR
            e.Position  LIKE '%' + @SearchTerm + '%')
        AND (@DepartmentId IS NULL OR e.DepartmentId = @DepartmentId)
        AND (@IsActive IS NULL OR e.IsActive = @IsActive)
    ORDER BY e.LastName, e.FirstName;
END
GO
