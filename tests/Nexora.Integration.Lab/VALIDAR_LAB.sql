/*
 Ejecutar en SSMS con la BASE NexoraIsolationTest seleccionada,
 después de aplicar las migraciones de Nexora SOLO a esa base.
 No usar en la base comercial. No crea empresas ni usuarios.
*/
IF DB_NAME() <> N'NexoraIsolationTest'
    THROW 51040, 'STOP: Ejecuta solo sobre NexoraIsolationTest.', 1;
IF OBJECT_ID(N'dbo.NexoraIsolationLabGuard', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NexoraIsolationLabGuard (
        Marca nvarchar(80) NOT NULL PRIMARY KEY
    );
END;
IF NOT EXISTS (SELECT 1 FROM dbo.NexoraIsolationLabGuard
               WHERE Marca = N'NEXORA_EXPLICIT_LAB_ONLY')
    INSERT INTO dbo.NexoraIsolationLabGuard(Marca)
    VALUES(N'NEXORA_EXPLICIT_LAB_ONLY');

SELECT DB_NAME() AS BaseDeDatos,
       Marca AS ConfirmacionDeLaboratorio
FROM dbo.NexoraIsolationLabGuard;
