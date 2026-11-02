# NEXORA — Plan de sostenibilidad técnica (noviembre 2026)

## Principio de entrega
Nunca declarar una fase lista para producción solo porque funcionan sus pantallas. Cada cambio debe pasar compilación, tests, verificación SQL, revisión de permisos y un escenario de regresión. El laboratorio `NexoraSecurityLab_Phase8` no es producción.

## Prioridad 0 — Seguridad y consistencia (bloqueante)
- [x] Primera batería de pruebas automatizadas: aislamiento de POST de productos.
- [x] Agregar aislamiento de clientes y matriz de roles (pendiente comprobar ejecución).
- [x] Agregar pruebas de negación sin tenant (pendiente comprobar ejecución).
- [ ] Pruebas HTTP de extremo a extremo: autenticación, antiforgery, membresía revocada, cambio de rol y acceso entre tenants.
- [ ] Pruebas SQL Server reales con base descartable: claves compuestas, filtros de consulta y transacciones; EF Core InMemory no reproduce todas las restricciones SQL.
- [ ] Verificar que cada operación de cambio de stock, anulación, recepción y reembolso sea atómica, idempotente cuando corresponda y auditable.
- [ ] Confirmar que secretos, cadenas de conexión de producción y cuentas administrativas nunca se distribuyan en el repositorio.

## Prioridad 1 — Producto comercial confiable
- [ ] Diseño y flujo de notas de crédito y devoluciones de dinero: no confundir anulación de factura con reembolso.
- [ ] Validación de inventario frente a ventas concurrentes, stock negativo, devoluciones y cambios de precio.
- [ ] Garantías y posventa: reglas claras para plazos y estado de cada producto.
- [ ] Facturas y reportes imprimibles; pruebas con varios tamaños de papel e impuestos configurables.
- [ ] Validación contable/fiscal dominicana y requisitos vigentes de comprobantes, antes de afirmar cumplimiento regulatorio.

## Prioridad 2 — Operación SaaS
- [ ] Alta de empresas, invitaciones, expiración/revocación de permisos y recuperación de cuentas mediante correo real.
- [ ] Observabilidad: logs estructurados sin contraseñas/tokens; identificadores de correlación y alertas.
- [ ] Copias de seguridad, restauración comprobada, políticas de retención y migraciones reversibles.
- [ ] Límites de recursos por empresa, paginación de listados e índices de consultas frecuentes.
- [ ] Documentación de despliegue, configuración por entorno y proceso de respuesta a incidentes.

## Puerta de calidad sugerida para cada cambio
1. PR en rama dedicada sin cambios simultáneos conflictivos.
2. `dotnet restore`, `dotnet build`, `dotnet test tests/NexoraSecurity.Tests/NexoraSecurity.Tests.csproj`.
3. Revisión de permisos y contexto de empresa en todas las rutas nuevas.
4. Pruebas manuales del escenario principal y dos negativos.
5. Evidencia de pruebas SQL en copia descartable, sin tocar datos de desarrollo principal.
6. Revisión de código por ambos integrantes antes de fusionar a master.

## Estado al crear este documento
Las pruebas InMemory agregadas a GitHub y el workflow de CI están **pendientes de ejecución/verificación**. No son certificación de seguridad completa ni de preparación para producción.
