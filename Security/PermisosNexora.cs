namespace SistemaFacturacion.Security
{
    /// <summary>
    /// Matriz mínima de permisos POR EMPRESA, nunca roles globales de Identity.
    /// Esta fase solo emplea ConsultarOperaciones en un endpoint de diagnóstico;
    /// el resto son contratos para futuras acciones luego de pruebas e integración.
    /// </summary>
    public static class PermisosNexora
    {
        public const string ConsultarOperaciones = "Nexora.Comercial.Consultar";
        public const string CrearFacturas = "Nexora.Facturas.Crear";
        public const string AnularFacturas = "Nexora.Facturas.Anular";
        public const string AdministrarInventario = "Nexora.Inventario.Administrar";
        public const string ConsultarPosventa = "Nexora.Posventa.Consultar";
        public const string ConsultarReembolsos = "Nexora.Reembolsos.Consultar";
        public const string ConsultarReportes = "Nexora.Reportes.Consultar";
        public const string AdministrarUsuarios = "Nexora.Usuarios.Administrar";
        public const string AdministrarRoles = "Nexora.Roles.Administrar";
        public const string AdministrarEmpresa = "Nexora.Empresa.Administrar";
        public const string ConfirmarRecepciones = "Nexora.Recepciones.Confirmar";
        public const string LiberarInventario = "Nexora.Inventario.Liberar";
        public const string ConfirmarReembolsos = "Nexora.Reembolsos.Confirmar";

        public static readonly IReadOnlyList<string> Todos = new[]
        {
            ConsultarOperaciones, CrearFacturas, AnularFacturas,
            AdministrarInventario, ConsultarPosventa, ConsultarReembolsos,
            ConsultarReportes, AdministrarUsuarios, AdministrarRoles,
            AdministrarEmpresa, ConfirmarRecepciones, LiberarInventario,
            ConfirmarReembolsos
        };

        private static readonly IReadOnlyDictionary<string, HashSet<string>> Matriz =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal)
            {
                ["Propietario"] = new HashSet<string>(Todos, StringComparer.Ordinal),
                ["Administrador"] = new HashSet<string>(StringComparer.Ordinal)
                {
                    ConsultarOperaciones, CrearFacturas, AnularFacturas,
                    AdministrarInventario, ConsultarPosventa,
                    ConsultarReembolsos, ConsultarReportes
                },
                ["Cajero"] = new HashSet<string>(StringComparer.Ordinal)
                {
                    ConsultarOperaciones, CrearFacturas, ConsultarPosventa
                }
            };

        public static bool TienePermiso(string? rolValidado, string permiso)
        {
            return rolValidado != null &&
                   !string.IsNullOrWhiteSpace(permiso) &&
                   Matriz.TryGetValue(rolValidado, out var permisos) &&
                   permisos.Contains(permiso);
        }
    }
}
