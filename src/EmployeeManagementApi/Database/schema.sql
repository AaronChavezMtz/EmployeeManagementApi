/* =========================================================================
   Sistema de Gestión de Empleados
   Script complementario: vista de resumen por departamento.
   Ejecutar DESPUÉS de aplicar las migraciones de Entity Framework Core
   (las migraciones crean las tablas Departments y Employees).
   Ejecutar conectado a la base de la app (sin USE: Azure SQL no lo admite).
   ========================================================================= */

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

-- sp_SearchEmployees ya no se usa (la búsqueda vive en GET /api/employees); se elimina si existe.
IF OBJECT_ID('dbo.sp_SearchEmployees', 'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_SearchEmployees;
GO
