/*
 NEXORA Fase 9.7 — PRE-FLIGHT SOLO LECTURA
 Ejecutar en SSMS sobre una copia DESCARTABLE, no sobre una base productiva.
 No crea, elimina ni modifica tablas.
*/
SET NOCOUNT ON;

SELECT
    DB_NAME() AS BaseActual,
    @@SERVERNAME AS Servidor,
    CASE
        WHEN DB_NAME() = N'NexoraSecurityLab_Phase8'
            THEN N'NO APLICAR: este es el laboratorio existente'
        WHEN DB_NAME() LIKE N'%Sandbox%' OR DB_NAME() LIKE N'%Descartable%'
            THEN N'Posible copia descartable: comprobar manualmente origen y respaldo'
        ELSE N'NO APLICAR: base no identificada como copia descartable'
    END AS Diagnostico;

IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
BEGIN
    SELECT N'SIN HISTORIAL EF: detener y revisar la base.' AS Advertencia;
END
ELSE
BEGIN
    SELECT MigrationId, ProductVersion
    FROM dbo.__EFMigrationsHistory
    ORDER BY MigrationId;

    SELECT
        N'20261023184000_EquipoSeguridadAuditoriaInvitaciones' AS Migracion,
        CASE WHEN EXISTS (
            SELECT 1 FROM dbo.__EFMigrationsHistory
            WHERE MigrationId = N'20261023184000_EquipoSeguridadAuditoriaInvitaciones')
        THEN N'Aplicada' ELSE N'Pendiente' END AS Estado
    UNION ALL
    SELECT
        N'20261024143000_RegistrarRevocacionInvitaciones',
        CASE WHEN EXISTS (
            SELECT 1 FROM dbo.__EFMigrationsHistory
            WHERE MigrationId = N'20261024143000_RegistrarRevocacionInvitaciones')
        THEN N'Aplicada' ELSE N'Pendiente' END
    UNION ALL
    SELECT
        N'20261103004333_Fase9NotasCreditoBorradores',
        CASE WHEN EXISTS (
            SELECT 1 FROM dbo.__EFMigrationsHistory
            WHERE MigrationId = N'20261103004333_Fase9NotasCreditoBorradores')
        THEN N'Aplicada' ELSE N'Pendiente' END;
END;

SELECT
    N'NotasCredito' AS Tabla,
    CASE WHEN OBJECT_ID(N'dbo.NotasCredito', N'U') IS NULL
        THEN N'No existe' ELSE N'Existe' END AS Estado
UNION ALL
SELECT
    N'DetallesNotaCredito',
    CASE WHEN OBJECT_ID(N'dbo.DetallesNotaCredito', N'U') IS NULL
        THEN N'No existe' ELSE N'Existe' END;

-- IMPORTANTE: las FKs actuales garantizan IdEmpresa, pero NO garantizan
-- que la línea acreditada pertenezca a la misma IdFactura de la cabecera.
-- No habilitar POST de notas de crédito hasta cerrar esta condición.
