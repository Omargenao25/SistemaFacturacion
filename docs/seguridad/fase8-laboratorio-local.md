# Nexora Lab — preparación controlada (Fase 8.5)

La rama incluye un perfil **Nexora Lab** en `Properties/launchSettings.json` y una configuración `appsettings.NexoraLab.json`.

- **Entorno:** `NexoraLab` (diferente de `Development`).
- **Base de datos:** `NexoraSecurityLab_Phase8` en LocalDB, distinta de `aspnet-SistemaFacturacion`.
- **Tenancy validada:** `false` inicialmente. No cambiarlo a `true` hasta crear datos de prueba y verificar migraciones y membresías.
- **Autenticación:** la política de confirmación de cuenta se mantiene activa en este entorno, igual que en producción; utilizar cuentas de laboratorio confirmadas.

## Antes de ejecutar
1. Confirmar que se descargaron los cambios y seleccionar el perfil **Nexora Lab** en Visual Studio.
2. **No copiar datos sensibles ni credenciales** de la base real. No sustituir la cadena por la base habitual.
3. Preparar la base de laboratorio: revisar migraciones antes de aplicarlas, y solo ejecutarlas apuntando a `NexoraSecurityLab_Phase8`. El perfil **no crea automáticamente la base**. No ejecutar comandos de borrado ni recreación sobre la base real.
4. Crear empresas A/B y usuarios de prueba con membresías conocidas. Confirmar cuentas de prueba usando un flujo administrativo local seguro, nunca alterando la seguridad global.
5. Solo después, habilitar `Seguridad:ActivarTenancyValidada` en el entorno aislado, realizar los escenarios de `fase8-roles-plan-pruebas.md` y registrar resultados.

## Precauciones
- En este perfil, `NexoraLab` no es `Development`: algunas ayudas de desarrollo, como página de errores de migraciones y permiso temporal de cuentas no confirmadas, **no** están activas.
- Este cambio solo prepara configuración, **no** demuestra migraciones aplicadas ni roles comprobados.
- No activar tenancy SaaS en `appsettings.json` ni `appsettings.Development.json`.
