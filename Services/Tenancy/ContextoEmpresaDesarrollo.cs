using System;
using Microsoft.Extensions.Hosting;
using SistemaFacturacion.Data;

namespace SistemaFacturacion.Services.Tenancy
{
    /// <summary>
    /// Adaptador temporal para desarrollo mononegocio; nunca acepta IdEmpresa de
    /// peticiones HTTP. En producción mantiene Guid.Empty hasta integrar con
    /// la membresía y roles por empresa de Russell.
    /// NO habilitar segundas empresas con este adaptador.
    /// </summary>
    public sealed class ContextoEmpresaDesarrollo : IContextoEmpresa
    {
        public Guid IdEmpresa { get; }
        public bool TieneEmpresa => IdEmpresa != Guid.Empty;

        public ContextoEmpresaDesarrollo(IHostEnvironment entorno)
        {
            IdEmpresa = entorno.IsDevelopment()
                ? CompatibilidadEmpresaInicial.Id
                : Guid.Empty;
        }
    }
}
