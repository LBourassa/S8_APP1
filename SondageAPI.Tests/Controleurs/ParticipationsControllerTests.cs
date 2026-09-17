using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using SondageAPI.Controleurs;
using SondageAPI.DTO;
using SondageAPI.Modeles;
using SondageAPI.Repositories;
using SondageAPI.Services;

namespace SondageAPI.Tests.Controleurs;

/// <summary>
/// Tests de ParticipationsController. ServiceParticipation est simulee avec
/// Moq (ses methodes sont "virtual" uniquement pour cette raison) afin de
/// tester le controleur seul, sans toucher au systeme de fichiers. Chaque
/// branche des trois actions du controleur est couverte, y compris le cas
/// "resultat inconnu" du switch de SoumettreParticipation (atteignable
/// uniquement en simulant une valeur d'enum hors des cas nommes).
/// </summary>
public class ParticipationsControllerTests
{
    /// <summary>
    /// Construit une Mock&lt;ServiceParticipation&gt; utilisable : comme
    /// ServiceParticipation n'a pas d'interface, son constructeur reel
    /// (ParticipantRepository, ParticipationRepository, ServiceSondage) doit
    /// recevoir de vraies instances pour que Moq puisse creer l'objet, meme
    /// si elles ne sont jamais reellement sollicitees (les methodes qui nous
    /// interessent sont simulees). Chaque appel utilise un dossier temporaire
    /// unique pour eviter toute interference entre tests.
    /// </summary>
    private static Mock<ServiceParticipation> CreerServiceParticipationMock()
    {
        var dossierTemp = Path.Combine(Path.GetTempPath(), "SondageApiTests_" + Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DataDirectory"] = dossierTemp
            })
            .Build();

        var environnement = new Mock<IHostEnvironment>();
        environnement.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

        var participantRepository = new ParticipantRepository(configuration);
        var participationRepository = new ParticipationRepository(configuration);
        var serviceSondage = new ServiceSondage(configuration, environnement.Object);

        return new Mock<ServiceParticipation>(participantRepository, participationRepository, serviceSondage);
    }

    // --- CreerParticipation(sondageId) : deux branches (sondage introuvable / trouve) ---

    [Fact]
    public async Task CreerParticipation_SondageIntrouvable_RetourneNotFound()
    {
        // Arrange
        var serviceParticipation = CreerServiceParticipationMock();
        serviceParticipation.Setup(s => s.CreerParticipationAsync("inexistant")).ReturnsAsync((Participant?)null);
        var controleur = new ParticipationsController(serviceParticipation.Object);

        // Act
        var resultat = await controleur.CreerParticipation("inexistant");

        // Assert
        var resultatNotFound = Assert.IsType<NotFoundObjectResult>(resultat.Result);
        var erreur = Assert.IsType<ErreurDto>(resultatNotFound.Value);
        Assert.Contains("inexistant", erreur.Erreur);
    }

    [Fact]
    public async Task CreerParticipation_SondageExistant_RetourneCreatedAtActionAvecCle()
    {
        // Arrange
        var serviceParticipation = CreerServiceParticipationMock();
        var participant = new Participant
        {
            Cle = "cle-generee",
            SondageId = "sondage1",
            DateEmission = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EstUtilisee = false
        };
        serviceParticipation.Setup(s => s.CreerParticipationAsync("sondage1")).ReturnsAsync(participant);
        var controleur = new ParticipationsController(serviceParticipation.Object);

        // Act
        var resultat = await controleur.CreerParticipation("sondage1");

        // Assert
        var resultatCree = Assert.IsType<CreatedAtActionResult>(resultat.Result);
        var dto = Assert.IsType<CreationSondageDto>(resultatCree.Value);
        Assert.Equal("cle-generee", dto.Cle);
        Assert.Equal("sondage1", dto.SondageId);
        Assert.Equal(nameof(ParticipationsController.ObtenirStatutParticipation), resultatCree.ActionName);
    }

    // --- ObtenirStatutParticipation(cle) : une seule branche, deux scenarios de donnees ---

    [Fact]
    public async Task ObtenirStatutParticipation_CleExistante_RetourneOkAvecStatut()
    {
        // Arrange
        var serviceParticipation = CreerServiceParticipationMock();
        serviceParticipation.Setup(s => s.ObtenirStatutAsync("cle-abc"))
            .ReturnsAsync((true, true, "sondage1"));
        var controleur = new ParticipationsController(serviceParticipation.Object);

        // Act
        var resultat = await controleur.ObtenirStatutParticipation("cle-abc");

        // Assert
        var resultatOk = Assert.IsType<OkObjectResult>(resultat.Result);
        var dto = Assert.IsType<StatutParticipationDto>(resultatOk.Value);
        Assert.Equal("cle-abc", dto.Cle);
        Assert.True(dto.Existe);
        Assert.True(dto.EstUtilisee);
        Assert.Equal("sondage1", dto.SondageId);
    }

    [Fact]
    public async Task ObtenirStatutParticipation_CleInexistante_RetourneOkAvecStatutVide()
    {
        // Arrange
        var serviceParticipation = CreerServiceParticipationMock();
        serviceParticipation.Setup(s => s.ObtenirStatutAsync("cle-inconnue"))
            .ReturnsAsync((false, false, (string?)null));
        var controleur = new ParticipationsController(serviceParticipation.Object);

        // Act
        var resultat = await controleur.ObtenirStatutParticipation("cle-inconnue");

        // Assert
        var resultatOk = Assert.IsType<OkObjectResult>(resultat.Result);
        var dto = Assert.IsType<StatutParticipationDto>(resultatOk.Value);
        Assert.False(dto.Existe);
        Assert.Null(dto.SondageId);
    }

    // --- SoumettreParticipation(cle, dto) : les 5 branches du switch ---

    private static SoumissionSondageDto CreerSoumissionExemple() => new()
    {
        Reponses = new List<ReponseDto> { new() { QuestionId = "q1", Valeur = "a" } }
    };

    [Fact]
    public async Task SoumettreParticipation_Succes_RetourneOkAvecConfirmation()
    {
        // Arrange
        var serviceParticipation = CreerServiceParticipationMock();
        var dateSoumission = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var participation = new Participation { DateSoumission = dateSoumission };
        serviceParticipation.Setup(s => s.SoumettreAsync("cle-valide", It.IsAny<SoumissionSondageDto>()))
            .ReturnsAsync((ResultatSoumission.Succes, participation, (IReadOnlyList<string>)Array.Empty<string>()));
        var controleur = new ParticipationsController(serviceParticipation.Object);

        // Act
        var resultat = await controleur.SoumettreParticipation("cle-valide", CreerSoumissionExemple());

        // Assert
        var resultatOk = Assert.IsType<OkObjectResult>(resultat);
        var dto = Assert.IsType<ConfirmationSoumissionDto>(resultatOk.Value);
        Assert.True(dto.Succes);
        Assert.Equal(dateSoumission, dto.DateSoumission);
    }

    [Fact]
    public async Task SoumettreParticipation_ReponsesInvalides_RetourneBadRequestAvecErreurs()
    {
        // Arrange
        var serviceParticipation = CreerServiceParticipationMock();
        var erreurs = new List<string> { "Reponse manquante pour la question 'q1'." };
        serviceParticipation.Setup(s => s.SoumettreAsync("cle-valide", It.IsAny<SoumissionSondageDto>()))
            .ReturnsAsync((ResultatSoumission.ReponsesInvalides, (Participation?)null, (IReadOnlyList<string>)erreurs));
        var controleur = new ParticipationsController(serviceParticipation.Object);

        // Act
        var resultat = await controleur.SoumettreParticipation("cle-valide", CreerSoumissionExemple());

        // Assert
        var resultatBadRequest = Assert.IsType<BadRequestObjectResult>(resultat);
        var dto = Assert.IsType<ErreurDto>(resultatBadRequest.Value);
        Assert.Contains("Reponse manquante", dto.Erreur);
    }

    [Fact]
    public async Task SoumettreParticipation_DejaSoumise_RetourneConflict()
    {
        // Arrange
        var serviceParticipation = CreerServiceParticipationMock();
        serviceParticipation.Setup(s => s.SoumettreAsync("cle-utilisee", It.IsAny<SoumissionSondageDto>()))
            .ReturnsAsync((ResultatSoumission.DejaSoumise, (Participation?)null, (IReadOnlyList<string>)Array.Empty<string>()));
        var controleur = new ParticipationsController(serviceParticipation.Object);

        // Act
        var resultat = await controleur.SoumettreParticipation("cle-utilisee", CreerSoumissionExemple());

        // Assert
        var resultatConflict = Assert.IsType<ConflictObjectResult>(resultat);
        Assert.IsType<ErreurDto>(resultatConflict.Value);
    }

    [Fact]
    public async Task SoumettreParticipation_CleInvalide_RetourneUnauthorized()
    {
        // Arrange
        var serviceParticipation = CreerServiceParticipationMock();
        serviceParticipation.Setup(s => s.SoumettreAsync("cle-inconnue", It.IsAny<SoumissionSondageDto>()))
            .ReturnsAsync((ResultatSoumission.CleInvalide, (Participation?)null, (IReadOnlyList<string>)Array.Empty<string>()));
        var controleur = new ParticipationsController(serviceParticipation.Object);

        // Act
        var resultat = await controleur.SoumettreParticipation("cle-inconnue", CreerSoumissionExemple());

        // Assert
        var resultatUnauthorized = Assert.IsType<UnauthorizedObjectResult>(resultat);
        Assert.IsType<ErreurDto>(resultatUnauthorized.Value);
    }

    [Fact]
    public async Task SoumettreParticipation_ResultatInconnu_Retourne500()
    {
        // Arrange : aucun des 4 cas nommes de ResultatSoumission ne peut
        // survenir avec le vrai ServiceParticipation ; ce test force cette
        // branche "filet de securite" du switch en simulant une valeur hors
        // de l'enum connu (impossible via l'API reelle, mais couvert quand
        // meme pour la couverture de branche a 100%).
        var serviceParticipation = CreerServiceParticipationMock();
        var resultatHorsEnum = (ResultatSoumission)999;
        serviceParticipation.Setup(s => s.SoumettreAsync("cle-quelconque", It.IsAny<SoumissionSondageDto>()))
            .ReturnsAsync((resultatHorsEnum, (Participation?)null, (IReadOnlyList<string>)Array.Empty<string>()));
        var controleur = new ParticipationsController(serviceParticipation.Object);

        // Act
        var resultat = await controleur.SoumettreParticipation("cle-quelconque", CreerSoumissionExemple());

        // Assert
        var resultatStatut = Assert.IsType<StatusCodeResult>(resultat);
        Assert.Equal(StatusCodes.Status500InternalServerError, resultatStatut.StatusCode);
    }
}
