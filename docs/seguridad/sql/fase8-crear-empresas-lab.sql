-- NEXORA Fase 8.5 - SOLO laboratorio local, no ejecutar en producción.
-- Crea dos empresas ficticias, sin usuarios ni permisos asignados.
-- Ejecutar conectándose explícitamente a NexoraSecurityLab_Phase8.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'NexoraSecurityLab_Phase8'
    THROW 51001, 'BLOQUEADO: la base activa no es NexoraSecurityLab_Phase8.', 1;

IF OBJECT_ID(N'dbo.Empresas', N'U') IS NULL
    THROW 51002, 'La tabla dbo.Empresas no existe. Revisar migraciones.', 1;

BEGIN TRANSACTION;

IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
               WHERE SlugNormalizado = N'nexora-lab-a')
BEGIN
    INSERT INTO dbo.Empresas
        (IdEmpresa, SlugNormalizado, NombreComercial, Estado,
         ZonaHoraria, Moneda, FechaCreacionUtc)
    VALUES
        (NEWID(), N'nexora-lab-a', N'Nexora Lab A', N'Activa',
         N'America/Santo_Domingo', N'DOP', SYSUTCDATETIME());
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Empresas WITH (UPDLOCK, HOLDLOCK)
               WHERE SlugNormalizado = N'nexora-lab-b')
BEGIN
    INSERT INTO dbo.Empresas
        (IdEmpresa, SlugNormalizado, NombreComercial, Estado,
         ZonaHoraria, Moneda, FechaCreacionUtc)
    VALUES
        (NEWID(), N'nexora-lab-b', N'Nexora Lab B', N'Activa',
         N'America/Santo_Domingo', N'DOP', SYSUTCDATETIME());
END;

COMMIT TRANSACTION;

-- Solo para comprobar creación: no concede acceso a usuarios.
SELECT SlugNormalizado, NombreComercial, Estado, Moneda
FROM dbo.Empresas
WHERE SlugNormalizado IN (N'nexora-lab-a', N'nexora-lab-b')
ORDER BY SlugNormalizado;
