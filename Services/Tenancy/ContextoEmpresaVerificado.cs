namespace SistemaFacturacion.Services.Tenancy
{
    /// <summary>
    /// Contexto de un único request. Comienza SIN empresa y solo el middleware
    /// del servidor puede establecer una membresía validada por SQL.
    /// No recibe IdEmpresa ni Rol de formularios, headers o claims no comprobados.
    /// </summary>
    public sealed class ContextoEmpresaVerificado : IContextoEmpresa
    {
        public Guid IdEmpresa { get; private set; } = Guid.Empty;
        public string? RolEmpresa { get; private set; }
        public bool TieneEmpresa => IdEmpresa != Guid.Empty && RolEmpresa != null;

        internal void Establecer(MembresiaEmpresaValidada membresia)
        {
            ArgumentNullException.ThrowIfNull(membresia);

            if (TieneEmpresa)
                throw new InvalidOperationException(
                    "La empresa validada no puede cambiarse durante la petición.");

            if (membresia.IdEmpresa == Guid.Empty ||
                membresia.Rol is not ("Propietario" or "Administrador" or "Cajero"))
                throw new InvalidOperationException(
                    "La validación devolvió una membresía empresarial inválida.");

            IdEmpresa = membresia.IdEmpresa;
            RolEmpresa = membresia.Rol;
        }
    }
}
