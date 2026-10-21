using System;

namespace SistemaFacturacion.Models
{
    /// <summary>
    /// Contrato de las entidades comerciales pertenecientes a una empresa.
    /// No incluye modelos globales de Identity, Empresa ni EmpresaUsuario.
    /// </summary>
    public interface IEntidadEmpresa
    {
        Guid IdEmpresa { get; set; }
    }
}
