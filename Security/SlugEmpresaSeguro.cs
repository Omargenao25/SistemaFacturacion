namespace SistemaFacturacion.Security
{
    /// <summary>
    /// Los slugs son identificadores candidatos, no autorizaciones.
    /// Cualquier empresa elegida DEBE validarse contra SQL en cada petición.
    /// </summary>
    public static class SlugEmpresaSeguro
    {
        public static string? Normalizar(string? candidato)
        {
            var slug = candidato?.Trim().ToLowerInvariant();
            return EsValido(slug) ? slug : null;
        }

        public static bool EsValido(string? slug)
        {
            if (slug == null || slug.Length is < 3 or > 63 ||
                slug[0] == '-' || slug[^1] == '-')
                return false;

            foreach (var caracter in slug)
            {
                if (!((caracter >= 'a' && caracter <= 'z') ||
                      (caracter >= '0' && caracter <= '9') ||
                       caracter == '-'))
                    return false;
            }
            return true;
        }
    }
}
