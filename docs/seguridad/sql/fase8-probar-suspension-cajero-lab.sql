-- NEXORA Fase 8.5.12: comprobar revocación inmediata de membresía.
-- EJECUTAR ÚNICAMENTE en NexoraSecurityLab_Phase8 (SQL Server Management Studio).
-- 1. Con el Cajero A conectado en una ventana privada, ejecutar Suspender.
-- 2. Probar la ruta /e/nexora-lab-a/Productos sin cerrar su sesión.
-- 3. Ejecutar Reactivar para dejar el laboratorio en su estado original.
-- Nunca ejecutar en bases reales. No modifica AspNetUsers ni contraseñas.
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @Accion nvarchar(12) = N'Suspender'; -- Cambiar a N'Reactivar' para restaurar.

IF DB_NAME() <> N'NexoraSecurityLab_Phase8'
    THROW 51200, 'Base incorrecta: requiere NexoraSecurityLab_Phase8.', 1;

IF @Accion NOT IN (N'Suspender', N'Reactivar')
    THROW 51201, 'Accion invalida: Suspender o Reactivar.', 1;

IF OBJECT_ID(N'dbo.Empresas', N'U') IS NULL
    OR OBJECT_ID(N'dbo.EmpresaUsuarios', N'U') IS NULL
    OR OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
    THROW 51202, 'Faltan tablas del laboratorio.', 1;

DECLARE @EstadoActual nvarchar(20) =
    CASE WHEN @Accion = N'Suspender' THEN N'Activo' ELSE N'Suspendido' END;
DECLARE @EstadoNuevo nvarchar(20) =
    CASE WHEN @Accion = N'Suspender' THEN N'Suspendido' ELSE N'Activo' END;

BEGIN TRANSACTION;

-- Bloqueos de actualizacion y validacion exacta de un unico registro.
DECLARE @Filas int;
SELECT @Filas = COUNT_BIG(*)
FROM dbo.EmpresaUsuarios AS m WITH (UPDLOCK, HOLDLOCK)
JOIN dbo.Empresas AS e ON e.IdEmpresa = m.IdEmpresa
JOIN dbo.AspNetUsers AS u ON u.Id = m.UserId
WHERE e.SlugNormalizado = N'nexora-lab-a'
  AND u.NormalizedEmail = N'CAJERO-A@NEXORA-LAB.TEST'
  AND m.Rol = N'Cajero'
  AND m.Estado = @EstadoActual
  AND m.FechaBajaUtc IS NULL
  AND e.Estado = N'Activa';

IF @Filas <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51203, 'Se esperaba exactamente un Cajero A en el estado de origen. Ningun cambio.', 1;
END;

UPDATE m
SET m.Estado = @EstadoNuevo
FROM dbo.EmpresaUsuarios AS m
JOIN dbo.Empresas AS e ON e.IdEmpresa = m.IdEmpresa
JOIN dbo.AspNetUsers AS u ON u.Id = m.UserId
WHERE e.SlugNormalizado = N'nexora-lab-a'
  AND u.NormalizedEmail = N'CAJERO-A@NEXORA-LAB.TEST'
  AND m.Rol = N'Cajero'
  AND m.Estado = @EstadoActual
  AND m.FechaBajaUtc IS NULL
  AND e.Estado = N'Activa';

IF @@ROWCOUNT <> 1
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51204, 'Actualizacion no afecto exactamente una membresia. Operacion revertida.', 1;
END;

COMMIT TRANSACTION;

SELECT e.SlugNormalizado, u.Email, m.Rol, m.Estado, m.FechaBajaUtc
FROM dbo.EmpresaUsuarios AS m
JOIN dbo.Empresas AS e ON e.IdEmpresa = m.IdEmpresa
JOIN dbo.AspNetUsers AS u ON u.Id = m.UserId
WHERE e.SlugNormalizado = N'nexora-lab-a'
  AND u.NormalizedEmail = N'CAJERO-A@NEXORA-LAB.TEST';
