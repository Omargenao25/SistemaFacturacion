-- NEXORA Fase 8.6.13: identificar IDs para pruebas POST multiempresa.
-- SOLO LECTURA. Ejecutar únicamente en NexoraSecurityLab_Phase8.
SET NOCOUNT ON;
IF DB_NAME() <> N'NexoraSecurityLab_Phase8'
    THROW 51310, 'Base incorrecta: requiere NexoraSecurityLab_Phase8.', 1;

SELECT e.SlugNormalizado AS Empresa, c.IdCliente, c.Nombre, c.Apellido
FROM dbo.Clientes c
JOIN dbo.Empresas e ON e.IdEmpresa = c.IdEmpresa
WHERE e.SlugNormalizado IN (N'nexora-lab-a', N'nexora-lab-b')
  AND c.Nombre = N'Cliente'
  AND c.Apellido IN (N'Prueba A', N'Prueba B')
ORDER BY e.SlugNormalizado, c.IdCliente;

SELECT e.SlugNormalizado AS Empresa, p.IdProducto, p.Codigo, p.Nombre, p.Stock
FROM dbo.Productos p
JOIN dbo.Empresas e ON e.IdEmpresa = p.IdEmpresa
WHERE e.SlugNormalizado IN (N'nexora-lab-a', N'nexora-lab-b')
  AND p.Codigo IN (N'LAB-A-001', N'LAB-B-001')
ORDER BY e.SlugNormalizado, p.IdProducto;

SELECT e.SlugNormalizado AS Empresa, COUNT_BIG(*) AS TotalFacturas
FROM dbo.Facturas f
JOIN dbo.Empresas e ON e.IdEmpresa = f.IdEmpresa
WHERE e.SlugNormalizado IN (N'nexora-lab-a', N'nexora-lab-b')
GROUP BY e.SlugNormalizado
ORDER BY e.SlugNormalizado;
