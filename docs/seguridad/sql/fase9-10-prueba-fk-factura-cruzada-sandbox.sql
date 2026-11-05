/*
NEXORA 9.10 — Prueba NEGATIVA de FK: linea vendida de otra factura.
Solo para base desechable NexoraMigrationSandbox_Phase9.
NO ejecutar con una transaccion previamente abierta.
Todo dato ficticio se revierte. No requiere cuentas de usuarios.
*/
USE [NexoraMigrationSandbox_Phase9];
GO
SET NOCOUNT ON;
SET XACT_ABORT OFF; -- Capturar error FK 547 y revertir la transaccion explícitamente.

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
DECLARE @FacturaA int, @FacturaB int, @DetalleB int, @NotaA int;
DECLARE @RechazoEsperado bit = 0;

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

    INSERT INTO dbo.Facturas
        (IdEmpresa, IdCliente, NumeroFactura, Fecha, Subtotal, Impuesto, Descuento, Total, Estado)
    VALUES
        (@Empresa, @Cliente, N'FASE9-TEMP-B', GETDATE(), 100, 18, 0, 118, N'Emitida');
    SET @FacturaB = CONVERT(int, SCOPE_IDENTITY());

    INSERT INTO dbo.DetallesFactura
        (IdEmpresa, IdFactura, IdProducto, Cantidad, GarantiaMeses, Precio, CostoUnitario, Subtotal)
    VALUES
        (@Empresa, @FacturaB, @Producto, 1, 0, 100, 60, 100);
    SET @DetalleB = CONVERT(int, SCOPE_IDENTITY());

    INSERT INTO dbo.NotasCredito
        (IdEmpresa, IdFactura, Motivo, Estado, Subtotal, Impuesto, Descuento, Total, FechaCreacionUtc)
    VALUES
        (@Empresa, @FacturaA, N'Prueba FK cruzada', N'Borrador', 100, 18, 0, 118, SYSUTCDATETIME());
    SET @NotaA = CONVERT(int, SCOPE_IDENTITY());

    -- Debe FALLAR: nota de factura A, linea original de factura B.
    BEGIN TRY
        INSERT INTO dbo.DetallesNotaCredito
            (IdEmpresa, IdFactura, IdNotaCredito, IdDetalleFactura, Cantidad, Subtotal, Impuesto, Descuento, Total)
        VALUES
            (@Empresa, @FacturaA, @NotaA, @DetalleB, 1, 100, 18, 0, 118);
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() = 547
        BEGIN
            SET @RechazoEsperado = 1;
            PRINT N'PASS: SQL Server rechazo detalle de otra factura (error FK 547).';
        END
        ELSE
            THROW;
    END CATCH;

    IF @RechazoEsperado <> 1
        THROW 51415, 'FAIL: SQL Server admitio la linea de otra factura.', 1;

    IF XACT_STATE() <> 1
        THROW 51416, 'La transaccion no permanece activa. Revisar estado.', 1;

    ROLLBACK TRANSACTION;
    SELECT N'PASS: rechazo FK y ROLLBACK completado' AS Resultado;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    SELECT ERROR_NUMBER() AS NumeroError, ERROR_MESSAGE() AS Descripcion;
    THROW;
END CATCH;

-- Deben mantenerse en cero: no quedan filas de la prueba.
SELECT
    (SELECT COUNT_BIG(*) FROM dbo.Facturas) AS Facturas,
    (SELECT COUNT_BIG(*) FROM dbo.DetallesFactura) AS DetallesFactura,
    (SELECT COUNT_BIG(*) FROM dbo.NotasCredito) AS NotasCredito,
    (SELECT COUNT_BIG(*) FROM dbo.DetallesNotaCredito) AS DetallesNotaCredito;
