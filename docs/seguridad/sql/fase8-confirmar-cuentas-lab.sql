-- FASE 8.5.5 - Confirmación exclusivamente de cuentas FICTICIAS locales.
-- Crear primero las cuatro cuentas por el formulario Register de NEXORA LAB.
-- Este script no crea contraseñas ni usuarios y NO sirve en bases reales.
SET NOCOUNT ON;
SET XACT_ABORT ON;
IF DB_NAME() <> N'NexoraSecurityLab_Phase8'
    THROW 51201, 'BLOQUEADO: solo NexoraSecurityLab_Phase8.', 1;
IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
    THROW 51202, 'AspNetUsers no existe.', 1;

DECLARE @Correos TABLE (Correo nvarchar(256) NOT NULL PRIMARY KEY);
INSERT INTO @Correos (Correo) VALUES
(N'propietario-a@nexora-lab.test'),
(N'admin-a@nexora-lab.test'),
(N'cajero-a@nexora-lab.test'),
(N'propietario-b@nexora-lab.test');

-- No modificar nada a menos que existan exactamente las 4 cuentas ficticias.
IF (SELECT COUNT(*) FROM dbo.AspNetUsers u
    INNER JOIN @Correos c ON u.NormalizedEmail = UPPER(c.Correo)) <> 4
    THROW 51203, 'Faltan cuentas ficticias. Regístralas primero en Nexora Lab.', 1;

BEGIN TRANSACTION;
UPDATE u
SET EmailConfirmed = 1
FROM dbo.AspNetUsers u
INNER JOIN @Correos c ON u.NormalizedEmail = UPPER(c.Correo)
WHERE u.EmailConfirmed = 0;
COMMIT TRANSACTION;

SELECT u.Email, u.EmailConfirmed
FROM dbo.AspNetUsers u
INNER JOIN @Correos c ON u.NormalizedEmail = UPPER(c.Correo)
ORDER BY u.Email;
