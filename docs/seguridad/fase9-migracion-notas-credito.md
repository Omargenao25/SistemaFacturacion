# Fase 9.3 — puerta de seguridad para migración de notas de crédito

**Estado:** preparación. No se ha creado ni ejecutado una migración SQL Server.

## Qué existe
- Modelos NotaCredito y DetalleNotaCredito y registro en ApplicationDbContext.
- Filtros globales de empresa y claves foráneas compuestas (IdEmpresa, identificador).
- Estado permitido de nota: Borrador. No hay emisión fiscal, devolución de dinero ni movimiento de inventario.
- Pruebas de modelo para metadatos de claves y control de concurrencia.

## Riesgos antes de crear una migración
1. La FK del detalle a NotaCredito verifica empresa y nota; la FK a DetalleFactura verifica empresa y línea vendida. **No garantiza por sí sola que esa línea pertenezca a la misma factura que la nota.** El futuro servicio debe validar esto en una transacción; valorar endurecimiento SQL.
2. Los importes de cabecera no quedan automáticamente conciliados con los detalles; deben calcularse del lado del servidor.
3. No está implementado el cálculo proporcional por línea de impuestos y descuentos ni las reglas fiscales de RD. El motor de validación existente no constituye un comprobante tributario.
4. Se deben bloquear sobrecréditos por concurrencia y solicitudes repetidas. Un simple SELECT previo no basta.
5. Las migraciones de EF Core necesitan inspección de SQL generado y ensayo en una copia desechable.

## Secuencia antes de aplicar a una base existente
1. Validar `dotnet build` y `dotnet test tests/NexoraSecurity.Tests/NexoraSecurity.Tests.csproj`.
2. Crear migración con nombre propio, sin ejecutar `database update` aún.
3. Revisar `Up` y `Down`, índices, tipos decimal, `RowVersion`, checks y FKs sin cascadas.
4. Generar SQL idempotente para revisión humana; revisar que solo afecte tablas planificadas.
5. Restaurar copia del laboratorio a una base descartable y aplicar la migración allí.
6. Probar SQL Server real: inserción válida dentro del tenant, FKs cruzadas rechazadas, límites de cantidad, transacciones y rollback.
7. Solo después, decidir si actualizar el laboratorio `NexoraSecurityLab_Phase8`. **Nunca ejecutar contra la base principal por defecto.**
8. No habilitar rutas de creación/emisión de notas hasta finalizar autorización, auditoría y requisitos fiscales.

## Comandos de revisión (desde raíz del repositorio)
```powershell
dotnet build
dotnet test tests/NexoraSecurity.Tests/NexoraSecurity.Tests.csproj
```

No se incluyen comandos de `database update` para evitar aplicaciones accidentales.
