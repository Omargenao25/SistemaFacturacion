/*
NEXORA 9.11 — Prueba POSITIVA de FK: linea vendida de la misma factura.
Solo para base desechable NexoraMigrationSandbox_Phase9.
NO ejecutar con una transaccion previamente abierta.
Todo dato ficticio se revierte. No requiere cuentas de usuarios.
*/
USE [NexoraMigrationSandbox_Phase9];
GO
SET NOCOUNT ON;
SET XACT_ABORT ON; -- Abortar transacciones ante errores de SQL Server.

IF DB_NAME() <> N'NexoraMigrationSandbox_Phase9'
    THROW 51410, 'Base de datos incorrecta.', 1;

IF @@TRANCOUNT <> 0
    THROW 51411, 'Existe una transaccion abierta. Usar sesion limpia.', 1;

IF NOT EXISTS (
    SELECT 1 FROM dbo.__EFMigrationsHistory
    WHERE MigrationId = N'20261103023918_Fase9IntegridadFacturaDetalle')
    THROW 51412, 'Falta migracion de integridad. No ejecutar prueba.', 1;

IF (SELECT COUNT_BIG(*) FROM dbo.Empresas) <> 1
    THROW 51413, 'Se esperaba exactamente una empresa ficticia.', 1;

IF EXISTS (SELECT 1 FROM dbo.Facturas)
   OR EXISTS (SELECT 1 FROM dbo.DetallesFactura)
   OR EXISTS (SELECT 1 FROM dbo.NotasCredito)
   OR EXISTS (SELECT 1 FROM dbo.DetallesNotaCredito)
    THROW 51414, 'La base contiene datos financieros. No ejecutar prueba.', 1;

DECLARE @Empresa uniqueidentifier = (SELECT TOP (1) IdEmpresa FROM dbo.Empresas);
DECLARE @Cliente int, @Categoria int, @Producto int;
DECLARE @FacturaA int, @DetalleA int, @NotaA int;

BEGIN TRY
    BEGIN TRANSACTION;

    INSERT INTO dbo.Clientes (IdEmpresa, Nombre, Apellido)
    VALUES (@Empresa, N'Cliente', N'Prueba Temporal');
    SET @Cliente = CONVERT(int, SCOPE_IDENTITY());

    INSERT INTO dbo.Categorias (IdEmpresa, Nombre)
    VALUES (@Empresa, N'Categoria Temporal Fase9');
    SET @Categoria = CONVERT(int, SCOPE_IDENTITY());

    INSERT INTO dbo.Productos
        (IdEmpresa, IdCategoria, Codigo, Nombre, Precio, Costo, Stock, GarantiaMeses, Estado)
    VALUES
        (@Empresa, @Categoria, N'FASE9-TEMP-01', N'Producto Temporal', 100.00, 60.00, 10, 0, 1);
    SET @Producto = CONVERT(int, SCOPE_IDENTITY());

    INSERT INTO dbo.Facturas
        (IdEmpresa, IdCliente, NumeroFactura, Fecha, Subtotal, Impuesto, Descuento, Total, Estado)
    VALUES
        (@Empresa, @Cliente, N'FASE9-TEMP-A', GETDATE(), 100, 18, 0, 118, N'Emitida');
    SET @FacturaA = CONVERT(int, SCOPE_IDENTITY());

    INSERT INTO dbo.DetallesFactura
        (IdEmpresa, IdFactura, IdProducto, Cantidad, GarantiaMeses, Precio, CostoUnitario, Subtotal)
    VALUES
        (@Empresa, @FacturaA, @Producto, 1, 0, 100, 60, 100);
    SET @DetalleA = CONVERT(int, SCOPE_IDENTITY());

    INSERT INTO dbo.NotasCredito
        (IdEmpresa, IdFactura, Motivo, Estado, Subtotal, Impuesto, Descuento, Total, FechaCreacionUtc)
    VALUES
        (@Empresa, @FacturaA, N'Prueba FK valida', N'Borrador', 100, 18, 0, 118, SYSUTCDATETIME());
    SET @NotaA = CONVERT(int, SCOPE_IDENTITY());

    -- Debe ACEPTAR: la nota y la linea original se refieren a Factura A.
    INSERT INTO dbo.DetallesNotaCredito
        (IdEmpresa, IdFactura, IdNotaCredito, IdDetalleFactura, Cantidad, Subtotal, Impuesto, Descuento, Total)
    VALUES
        (@Empresa, @FacturaA, @NotaA, @DetalleA, 1, 100, 18, 0, 118);

    IF NOT EXISTS (
        SELECT 1
        FROM dbo.DetallesNotaCredito AS d
        INNER JOIN dbo.NotasCredito AS n
            ON n.IdEmpresa = d.IdEmpresa
           AND n.IdFactura = d.IdFactura
           AND n.IdNotaCredito = d.IdNotaCredito
        INNER JOIN dbo.DetallesFactura AS v
            ON v.IdEmpresa = d.IdEmpresa
           AND v.IdFactura = d.IdFactura
           AND v.IdDetalle = d.IdDetalleFactura
        WHERE d.IdEmpresa = @Empresa
          AND d.IdFactura = @FacturaA
          AND d.IdNotaCredito = @NotaA
          AND d.IdDetalleFactura = @DetalleA
          AND d.Cantidad = 1
          AND d.Total = 118
    )
        THROW 51417, 'FAIL: SQL Server no guardo la relacion correcta.', 1;

    SELECT N'PASS: linea de la factura correcta aceptada por SQL Server' AS Validacion;

    ROLLBACK TRANSACTION;
    SELECT N'PASS: relacion correcta y ROLLBACK completado' AS Resultado;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    SELECT ERROR_NUMBER() AS NumeroError, ERROR_MESSAGE() AS Descripcion;
    THROW;
END CATCH;

-- Deben mantenerse en cero: no quedan filas de la prueba.
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.Clientes) AS Clientes,
    (SELECT COUNT_BIG(*) FROM dbo.Categorias) AS Categorias,
    (SELECT COUNT_BIG(*) FROM dbo.Productos) AS Productos,
    (SELECT COUNT_BIG(*) FROM dbo.Facturas) AS Facturas,
    (SELECT COUNT_BIG(*) FROM dbo.DetallesFactura) AS DetallesFactura,
    (SELECT COUNT_BIG(*) FROM dbo.NotasCredito) AS NotasCredito,
    (SELECT COUNT_BIG(*) FROM dbo.DetallesNotaCredito) AS DetallesNotaCredito;
