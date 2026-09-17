using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace SondageAPI.Services;

/// <summary>
/// Authentifie les applications clientes via la cle d'API statique fournie
/// dans l'en-tete X-API-KEY.
///
/// Techniquement, cette classe reste un AuthenticationHandler ASP.NET Core
/// simplement placee dans Services/ et renomme pour rester coherent avec le reste du projet.
/// </summary>
public class ServiceAuthentification : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemaParDefaut = "ApiKey";
    public const string NomEntete = "X-API-KEY";

    private readonly IConfiguration _configuration;

    public ServiceAuthentification(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(NomEntete, out var cleFournie))
        {
            return Task.FromResult(AuthenticateResult.Fail("En-tete X-API-KEY manquant."));
        }

        var cleAttendue = _configuration["ApiSettings:ApiKey"];
        if (string.IsNullOrEmpty(cleAttendue))
        {
            // Refuser par defaut si la cle n'est pas configuree cote serveur,
            // plutot que d'accepter n'importe quelle valeur.
            return Task.FromResult(AuthenticateResult.Fail("Cle d'API non configuree cote serveur."));
        }

        if (!EgaliteTempsConstant(cleFournie.ToString(), cleAttendue))
        {
            return Task.FromResult(AuthenticateResult.Fail("Cle d'API invalide."));
        }

        var claims = new[] { new Claim(ClaimTypes.Name, "ApiClient") };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.StatusCode = StatusCodes.Status401Unauthorized;
        Response.ContentType = "application/json";
        return Response.WriteAsync("{\"erreur\":\"Acces non autorise. Cle d'API manquante ou invalide.\"}");
    }

    /// <summary>
    /// Comparaison a temps constant pour limiter les attaques par mesure du
    /// temps de reponse (timing attack) sur la cle d'API.
    /// </summary>
    private static bool EgaliteTempsConstant(string fournie, string attendue)
    {
        var octetsFournis = Encoding.UTF8.GetBytes(fournie);
        var octetsAttendus = Encoding.UTF8.GetBytes(attendue);
        if (octetsFournis.Length != octetsAttendus.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(octetsFournis, octetsAttendus);
    }
}
