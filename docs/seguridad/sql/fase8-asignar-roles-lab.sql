-- FASE 8.5.5: asignar membresías a cuentas de Identity YA CREADAS.
-- Ejecutar SOLO en NexoraSecurityLab_Phase8, tras registrar 4 usuarios ficticios
-- y confirmar sus correos. No escribe contraseñas ni crea usuarios de Identity.
-- Correos reservados para identidades ficticias creadas en el laboratorio.
SET NOCOUNT ON;
SET XACT_ABORT ON;

IF DB_NAME() <> N'NexoraSecurityLab_Phase8'
    THROW 51100, 'Base incorrecta: se requiere NexoraSecurityLab_Phase8.', 1;
IF OBJECT_ID(N'dbo.Empresas', N'U') IS NULL OR
   OBJECT_ID(N'dbo.EmpresaUsuarios', N'U') IS NULL OR
   OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
    THROW 51101, 'Tablas de laboratorio ausentes; verificar migraciones.', 1;

DECLARE @Cuentas TABLE (Slug nvarchar(63), Correo nvarchar(256), Rol nvarchar(30));
INSERT INTO @Cuentas (Slug, Correo, Rol) VALUES
(N'nexora-lab-a', N'propietario-a@nexora-lab.test', N'Propietario'),
(N'nexora-lab-a', N'admin-a@nexora-lab.test', N'Administrador'),
(N'nexora-lab-a', N'cajero-a@nexora-lab.test', N'Cajero'),
(N'nexora-lab-b', N'propietario-b@nexora-lab.test', N'Propietario');

-- Las cuentas de prueba se deben crear primero mediante ASP.NET Core Identity.

IF (SELECT COUNT(*) FROM @Cuentas) <> 4 OR
   (SELECT COUNT(DISTINCT Correo) FROM @Cuentas) <> 4
    THROW 51103, 'Se necesitan cuatro correos de laboratorio distintos.', 1;

IF EXISTS (
    SELECT 1 FROM @Cuentas c
    LEFT JOIN dbo.Empresas e ON e.SlugNormalizado = c.Slug
    LEFT JOIN dbo.AspNetUsers u ON u.NormalizedEmail = UPPER(c.Correo)
    WHERE e.IdEmpresa IS NULL OR e.Estado <> N'Activa'
       OR u.Id IS NULL OR u.EmailConfirmed <> 1
)
    THROW 51104, 'Empresa activa o cuenta confirmada no encontrada. No se alteró nada.', 1;

BEGIN TRANSACTION;
-- No sobreescribir relaciones existentes: evita modificar accidentalmente roles.
IF EXISTS (
    SELECT 1 FROM @Cuentas c
    JOIN dbo.Empresas e ON e.SlugNormalizado = c.Slug
    JOIN dbo.AspNetUsers u ON u.NormalizedEmail = UPPER(c.Correo)
    JOIN dbo.EmpresaUsuarios m ON m.IdEmpresa = e.IdEmpresa AND m.UserId = u.Id
)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51105, 'Ya existen membresías de prueba; revisión manual requerida.', 1;
END;

INSERT INTO dbo.EmpresaUsuarios
    (IdEmpresa, UserId, Rol, Estado, FechaAltaUtc, FechaBajaUtc)
SELECT e.IdEmpresa, u.Id, c.Rol, N'Activo', SYSUTCDATETIME(), NULL
FROM @Cuentas c
JOIN dbo.Empresas e ON e.SlugNormalizado = c.Slug
JOIN dbo.AspNetUsers u ON u.NormalizedEmail = UPPER(c.Correo);

IF @@ROWCOUNT <> 4
BEGIN
    ROLLBACK TRANSACTION;
    THROW 51106, 'No se asignaron las cuatro membresías. Operación revertida.', 1;
END;

COMMIT TRANSACTION;

-- Mostrar solo correos ficticios, empresa, rol y estado para validación.
SELECT e.SlugNormalizado, u.Email, m.Rol, m.Estado
FROM dbo.EmpresaUsuarios m
JOIN dbo.Empresas e ON e.IdEmpresa = m.IdEmpresa
JOIN dbo.AspNetUsers u ON u.Id = m.UserId
WHERE e.SlugNormalizado IN (N'nexora-lab-a', N'nexora-lab-b')
ORDER BY e.SlugNormalizado, m.Rol;
