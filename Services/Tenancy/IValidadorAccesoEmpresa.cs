using System.Security.Claims;

namespace SistemaFacturacion.Services.Tenancy
{
    /// <summary>
    /// Valida una membresía vigente para un usuario ya autenticado.
    /// El slug es solo un candidato; nunca otorga permisos por sí mismo.
    /// </summary>
    public interface IValidadorAccesoEmpresa
    {
        Task<MembresiaEmpresaValidada?> ValidarAsync(
            ClaimsPrincipal? usuario,
            string? slugCandidato,
            CancellationToken cancellationToken = default);
    }
}
