namespace SistemaFacturacion.Security
{
    /// <summary>
    /// Lista cerrada y auditable de permisos para acciones MVC actuales.
    /// Se consulta por ControllerActionDescriptor + método HTTP; NUNCA por
    /// datos que el usuario entregue en query/form/body.
    /// Cualquier acción futura sin mapeo = DENEGAR por defecto.
    /// </summary>
    public static class ReglasAccesoControladores
    {
        public static string? ObtenerPermiso(
            string? controlador, string? accion, string? metodoHttp)
        {
            if (string.IsNullOrWhiteSpace(controlador) ||
                string.IsNullOrWhiteSpace(accion) ||
                string.IsNullOrWhiteSpace(metodoHttp))
                return null;

            var metodo = metodoHttp.ToUpperInvariant();
            // No extender silenciosamente GET a métodos que puedan escribir.
            if (metodo is not ("GET" or "POST"))
                return null;

            return (controlador, accion, metodo) switch
            {
                ("Home", "Index", "GET") =>
                    PermisosNexora.ConsultarOperaciones,
                ("Home", "Privacy", "GET") =>
                    PermisosNexora.ConsultarOperaciones,
                ("Home", "Error", "GET") =>
                    PermisosNexora.ConsultarOperaciones,

                ("Facturas", "Index" or "Details", "GET") =>
                    PermisosNexora.ConsultarOperaciones,
                ("Facturas", "Create", "GET" or "POST") =>
                    PermisosNexora.CrearFacturas,
                ("Facturas", "Anular", "POST") =>
                    PermisosNexora.AnularFacturas,

                ("Clientes", "Index" or "Details", "GET") =>
                    PermisosNexora.ConsultarOperaciones,
                ("Clientes", "Create" or "Edit" or "Delete", "GET" or "POST") =>
                    PermisosNexora.AdministrarClientes,

                ("Productos" or "Categorias", "Index" or "Details", "GET") =>
                    PermisosNexora.ConsultarOperaciones,
                ("Productos" or "Categorias", "Create" or "Edit" or "Delete", "GET" or "POST") =>
                    PermisosNexora.AdministrarInventario,

                ("Posventa", "Index" or "Details", "GET") =>
                    PermisosNexora.ConsultarPosventa,
                ("Posventa", "Create", "GET" or "POST") =>
                    PermisosNexora.RegistrarPosventa,
                ("Posventa", "CambiarEstado", "POST") =>
                    PermisosNexora.ResolverPosventa,

                ("Recepciones", "Index" or "Details", "GET") =>
                    PermisosNexora.ConsultarRecepciones,
                ("ControlAnulaciones", "Index", "GET") =>
                    PermisosNexora.ConsultarReembolsos,
                ("Reportes", "Index", "GET") =>
                    PermisosNexora.ConsultarReportes,

                // Empleados contiene correos/roles; solo el propietario con
                // membresía verificada puede consultar este endpoint.
                // Ningún método POST está autorizado en esta fase.
                ("Equipo", "Index", "GET") =>
                    PermisosNexora.AdministrarUsuarios,

                // La política del controlador es una segunda capa. El filtro
                // nunca permite un método de escritura sobre el diagnóstico.
                ("SeguridadEmpresa", "Estado", "GET") =>
                    PermisosNexora.ConsultarOperaciones,

                _ => null
            };
        }
    }
}
