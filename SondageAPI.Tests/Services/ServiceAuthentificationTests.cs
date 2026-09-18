using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using SondageAPI.Services;

namespace SondageAPI.Tests.Services;

/// <summary>
/// Tests de ServiceAuthentification (AuthenticationHandler base sur
/// X-API-KEY), via un handler reel initialise avec InitializeAsync. Couvre
/// en-tete absent/present, cle serveur non configuree/vide/configuree, et
/// cle fournie invalide (longueur differente et meme longueur) / valide.
/// </summary>
public class ServiceAuthentificationTests
{
    private static Mock<IOptionsMonitor<AuthenticationSchemeOptions>> CreerOptionsMonitor()
    {
        var optionsMonitor = new Mock<IOptionsMonitor<AuthenticationSchemeOptions>>();
        optionsMonitor.Setup(o => o.Get(It.IsAny<string>())).Returns(new AuthenticationSchemeOptions());
        return optionsMonitor;
    }

    private static ServiceAuthentification CreerHandler(IConfiguration configuration) =>
        new(CreerOptionsMonitor().Object, NullLoggerFactory.Instance, UrlEncoder.Default, configuration);

    private static AuthenticationScheme CreerScheme() => new(
        ServiceAuthentification.SchemaParDefaut,
        ServiceAuthentification.SchemaParDefaut,
        typeof(ServiceAuthentification));

    private static async Task<AuthenticateResult> AuthentifierAsync(string? cleConfiguree, string? headerFourni)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ApiSettings:ApiKey"] = cleConfiguree })
            .Build();
        var handler = CreerHandler(configuration);

        var httpContext = new DefaultHttpContext();
        if (headerFourni is not null)
        {
            httpContext.Request.Headers[ServiceAuthentification.NomEntete] = headerFourni;
        }

        await handler.InitializeAsync(CreerScheme(), httpContext);
        return await handler.AuthenticateAsync();
    }

    // --- En-tete X-API-KEY absent / present ---

    [Fact]
    public async Task Authentifier_EnTeteAbsent_Echoue()
    {
        // Act
        var resultat = await AuthentifierAsync(cleConfiguree: "cle-serveur", headerFourni: null);

        // Assert
        Assert.False(resultat.Succeeded);
        Assert.Contains("manquant", resultat.Failure!.Message);
    }

    // --- Cle API non configuree cote serveur (absente / vide) ---

    [Fact]
    public async Task Authentifier_CleServeurNonConfiguree_Echoue()
    {
        // Act
        var resultat = await AuthentifierAsync(cleConfiguree: null, headerFourni: "peu-importe");

        // Assert
        Assert.False(resultat.Succeeded);
        Assert.Contains("non configuree", resultat.Failure!.Message);
    }

    [Fact]
    public async Task Authentifier_CleServeurVide_Echoue()
    {
        // Act
        var resultat = await AuthentifierAsync(cleConfiguree: "", headerFourni: "peu-importe");

        // Assert
        Assert.False(resultat.Succeeded);
        Assert.Contains("non configuree", resultat.Failure!.Message);
    }

    // --- Cle fournie invalide (deux chemins d'EgaliteTempsConstant) vs valide ---

    [Fact]
    public async Task Authentifier_CleFournieDeLongueurDifferente_Echoue()
    {
        // Arrange/Act : couvre la branche "longueurs differentes" (retour
        // anticipe, avant meme FixedTimeEquals).
        var resultat = await AuthentifierAsync(cleConfiguree: "cle-serveur-correcte", headerFourni: "trop-courte");

        // Assert
        Assert.False(resultat.Succeeded);
        Assert.Contains("invalide", resultat.Failure!.Message);
    }

    [Fact]
    public async Task Authentifier_CleFournieMemeLongueurMaisDifferente_Echoue()
    {
        // Arrange/Act : meme longueur que la cle serveur, contenu different
        // -- couvre le chemin qui atteint reellement
        // CryptographicOperations.FixedTimeEquals (pas seulement le retour
        // anticipe sur la difference de longueur).
        var resultat = await AuthentifierAsync(cleConfiguree: "abcdefghij", headerFourni: "zzzzzzzzzz");

        // Assert
        Assert.False(resultat.Succeeded);
        Assert.Contains("invalide", resultat.Failure!.Message);
    }

    [Fact]
    public async Task Authentifier_CleFournieValide_Reussit()
    {
        // Act
        var resultat = await AuthentifierAsync(cleConfiguree: "cle-secrete-123", headerFourni: "cle-secrete-123");

        // Assert
        Assert.True(resultat.Succeeded);
        Assert.Equal("ApiClient", resultat.Principal!.Identity!.Name);
    }

    // --- HandleChallengeAsync : pas de branche, mais fait partie du service ---

    [Fact]
    public async Task Challenge_EcritStatut401EtCorpsJson()
    {
        // Arrange
        var configuration = new ConfigurationBuilder().Build();
        var handler = CreerHandler(configuration);
        var httpContext = new DefaultHttpContext { Response = { Body = new MemoryStream() } };
        await handler.InitializeAsync(CreerScheme(), httpContext);

        // Act
        await handler.ChallengeAsync(null);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, httpContext.Response.StatusCode);
        httpContext.Response.Body.Seek(0, SeekOrigin.Begin);
        var corps = await new StreamReader(httpContext.Response.Body).ReadToEndAsync();
        Assert.Contains("Acces non autorise", corps);
    }
}
