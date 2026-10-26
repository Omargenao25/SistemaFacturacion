-- NEXORA Fase 8.6.1: auditoria SOLO LECTURA del reparto de datos por empresa.
-- Usar UNICAMENTE en NexoraSecurityLab_Phase8; no modifica ningun registro.
SET NOCOUNT ON;
IF DB_NAME() <> N'NexoraSecurityLab_Phase8'
    THROW 51300, 'Base incorrecta: solo NexoraSecurityLab_Phase8.', 1;
IF OBJECT_ID(N'dbo.Empresas',N'U') IS NULL
 OR OBJECT_ID(N'dbo.Clientes',N'U') IS NULL
 OR OBJECT_ID(N'dbo.Productos',N'U') IS NULL
 OR OBJECT_ID(N'dbo.Facturas',N'U') IS NULL
    THROW 51301, 'Faltan tablas comerciales; verificar migraciones.', 1;

SELECT
    e.SlugNormalizado AS Empresa,
    e.Estado AS EstadoEmpresa,
    (SELECT COUNT_BIG(*) FROM dbo.Clientes AS c WHERE c.IdEmpresa = e.IdEmpresa) AS Clientes,
    (SELECT COUNT_BIG(*) FROM dbo.Productos AS p WHERE p.IdEmpresa = e.IdEmpresa) AS Productos,
    (SELECT COUNT_BIG(*) FROM dbo.Facturas AS f WHERE f.IdEmpresa = e.IdEmpresa) AS Facturas
FROM dbo.Empresas AS e
WHERE e.SlugNormalizado IN (N'nexora-lab-a', N'nexora-lab-b')
ORDER BY e.SlugNormalizado;

-- Alerta sobre datos huerfanos o asignados a empresas inexistentes.
SELECT
    N'Clientes' AS Tabla,
    COUNT_BIG(*) AS RegistrosSinEmpresaValida
FROM dbo.Clientes c LEFT JOIN dbo.Empresas e ON e.IdEmpresa=c.IdEmpresa
WHERE e.IdEmpresa IS NULL
UNION ALL
SELECT N'Productos', COUNT_BIG(*)
FROM dbo.Productos p LEFT JOIN dbo.Empresas e ON e.IdEmpresa=p.IdEmpresa
WHERE e.IdEmpresa IS NULL
UNION ALL
SELECT N'Facturas', COUNT_BIG(*)
FROM dbo.Facturas f LEFT JOIN dbo.Empresas e ON e.IdEmpresa=f.IdEmpresa
WHERE e.IdEmpresa IS NULL;
