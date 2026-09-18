using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using SondageAPI.DTO;
using SondageAPI.Modeles;
using SondageAPI.Repositories;
using SondageAPI.Services;

namespace SondageAPI.Tests.Services;

/// <summary>
/// Tests de ServiceParticipation : couvre CreerParticipationAsync,
/// ObtenirStatutAsync et les 5 issues de SoumettreAsync. Utilise de vraies
/// instances de repository, sauf pour le dernier cas (TryConsumeAsync ->
/// Introuvable), simule via Moq car impossible a provoquer avec un vrai
/// repository -- voir la note sur ce test.
/// </summary>
public class ServiceParticipationTests
{
    private static string CreerDossierTemp()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "SondageApiTests_ServiceParticipation_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        return dossier;
    }

    private const string JsonSondageDeuxQuestions = """
        {
          "titre": "Sondage de test",
          "questions": [
            { "id": "q1", "texte": "Q1?", "options": [ { "cle": "a", "texte": "A" }, { "cle": "b", "texte": "B" } ] },
            { "id": "q2", "texte": "Q2?", "options": [ { "cle": "x", "texte": "X" }, { "cle": "y", "texte": "Y" } ] }
          ]
        }
        """;

    /// <summary>Charge un seul sondage ("sondage-test", 2 questions) depuis un fichier temporaire.</summary>
    private static ServiceSondage CreerServiceSondageAvecUnSondage()
    {
        var dossierSondages = CreerDossierTemp();
        File.WriteAllText(Path.Combine(dossierSondages, "sondage-test.txt"), JsonSondageDeuxQuestions);
        var environnement = new Mock<IHostEnvironment>();
        environnement.SetupGet(e => e.ContentRootPath).Returns("/inutilise");
        var configurationSondages = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:SurveysDirectory"] = dossierSondages })
            .Build();
        return new ServiceSondage(configurationSondages, environnement.Object);
    }

    private static IConfiguration CreerConfigurationStorage() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:DataDirectory"] = CreerDossierTemp() })
            .Build();

    /// <summary>Construit un ServiceParticipation reel, avec des repositories pointant vers un dossier de stockage temporaire.</summary>
    private static ServiceParticipation CreerServiceParticipation(
        out ParticipantRepository participantRepository,
        out ParticipationRepository participationRepository)
    {
        var serviceSondage = CreerServiceSondageAvecUnSondage();
        var configurationStorage = CreerConfigurationStorage();
        participantRepository = new ParticipantRepository(configurationStorage);
        participationRepository = new ParticipationRepository(configurationStorage);

        return new ServiceParticipation(participantRepository, participationRepository, serviceSondage);
    }

    private static SoumissionSondageDto DtoReponsesValides() => new()
    {
        Reponses = new List<ReponseDto>
        {
            new() { QuestionId = "q1", Valeur = "a" },
            new() { QuestionId = "q2", Valeur = "x" }
        }
    };

    // --- CreerParticipationAsync : sondage introuvable / trouve ---

    [Fact]
    public async Task CreerParticipationAsync_SondageIntrouvable_RetourneNull()
    {
        // Arrange
        var service = CreerServiceParticipation(out _, out _);

        // Act
        var participant = await service.CreerParticipationAsync("sondage-inexistant");

        // Assert
        Assert.Null(participant);
    }

    [Fact]
    public async Task CreerParticipationAsync_SondageExistant_RetourneUnParticipantEtLePersiste()
    {
        // Arrange
        var service = CreerServiceParticipation(out var participantRepository, out _);

        // Act
        var participant = await service.CreerParticipationAsync("sondage-test");

        // Assert
        Assert.NotNull(participant);
        Assert.Equal("sondage-test", participant!.SondageId);
        Assert.False(participant.EstUtilisee);
        Assert.False(string.IsNullOrWhiteSpace(participant.Cle));
        var persiste = await participantRepository.FindAsync(participant.Cle);
        Assert.NotNull(persiste);
    }

    // --- ObtenirStatutAsync : introuvable / trouve ---

    [Fact]
    public async Task ObtenirStatutAsync_CleIntrouvable_RetourneStatutVide()
    {
        // Arrange
        var service = CreerServiceParticipation(out _, out _);

        // Act
        var (existe, estUtilisee, sondageId) = await service.ObtenirStatutAsync("cle-inconnue");

        // Assert
        Assert.False(existe);
        Assert.False(estUtilisee);
        Assert.Null(sondageId);
    }

    [Fact]
    public async Task ObtenirStatutAsync_CleExistante_RetourneSonStatut()
    {
        // Arrange
        var service = CreerServiceParticipation(out _, out _);
        var participant = await service.CreerParticipationAsync("sondage-test");

        // Act
        var (existe, estUtilisee, sondageId) = await service.ObtenirStatutAsync(participant!.Cle);

        // Assert
        Assert.True(existe);
        Assert.False(estUtilisee);
        Assert.Equal("sondage-test", sondageId);
    }

    // --- SoumettreAsync : cle introuvable / sondage disparu / reponses invalides / deja soumise / succes / TryConsumeAsync->Introuvable ---

    [Fact]
    public async Task SoumettreAsync_CleIntrouvable_RetourneCleInvalide()
    {
        // Arrange
        var service = CreerServiceParticipation(out _, out _);

        // Act
        var (resultat, participation, erreurs) = await service.SoumettreAsync("cle-inconnue", DtoReponsesValides());

        // Assert
        Assert.Equal(ResultatSoumission.CleInvalide, resultat);
        Assert.Null(participation);
        Assert.Empty(erreurs);
    }

    [Fact]
    public async Task SoumettreAsync_SondageDisparuApresEmissionDeLaCle_RetourneCleInvalide()
    {
        // Arrange : participant ajoute directement au repository, avec un
        // SondageId qui ne correspond a aucun sondage charge -- simule le
        // filet de securite "le sondage a disparu apres l'emission de la cle".
        var service = CreerServiceParticipation(out var participantRepository, out _);
        await participantRepository.AddAsync(new Participant
        {
            Cle = "cle-orpheline",
            SondageId = "sondage-fantome",
            DateEmission = DateTime.UtcNow,
            EstUtilisee = false
        });

        // Act
        var (resultat, participation, erreurs) = await service.SoumettreAsync("cle-orpheline", DtoReponsesValides());

        // Assert
        Assert.Equal(ResultatSoumission.CleInvalide, resultat);
        Assert.Null(participation);
        Assert.Empty(erreurs);
    }

    [Fact]
    public async Task SoumettreAsync_ReponsesInvalides_RetourneReponsesInvalidesAvecErreursEtNeConsommePasLaCle()
    {
        // Arrange
        var service = CreerServiceParticipation(out _, out _);
        var participant = await service.CreerParticipationAsync("sondage-test");
        var dtoInvalide = new SoumissionSondageDto
        {
            // q2 manquante, "z" invalide pour q1.
            Reponses = new List<ReponseDto> { new() { QuestionId = "q1", Valeur = "z" } }
        };

        // Act
        var (resultat, participation, erreurs) = await service.SoumettreAsync(participant!.Cle, dtoInvalide);

        // Assert
        Assert.Equal(ResultatSoumission.ReponsesInvalides, resultat);
        Assert.Null(participation);
        Assert.NotEmpty(erreurs);

        // La validation se fait avant la consommation de la cle : le
        // participant doit donc pouvoir encore soumettre.
        var statutApres = await service.ObtenirStatutAsync(participant.Cle);
        Assert.False(statutApres.EstUtilisee);
    }

    [Fact]
    public async Task SoumettreAsync_CleDejaUtilisee_RetourneDejaSoumise()
    {
        // Arrange
        var service = CreerServiceParticipation(out _, out _);
        var participant = await service.CreerParticipationAsync("sondage-test");
        var premiereSoumission = await service.SoumettreAsync(participant!.Cle, DtoReponsesValides());
        Assert.Equal(ResultatSoumission.Succes, premiereSoumission.Resultat); // pre-condition

        // Act : deuxieme soumission avec la meme cle
        var (resultat, participation, erreurs) = await service.SoumettreAsync(participant.Cle, DtoReponsesValides());

        // Assert
        Assert.Equal(ResultatSoumission.DejaSoumise, resultat);
        Assert.Null(participation);
        Assert.Empty(erreurs);
    }

    [Fact]
    public async Task SoumettreAsync_Succes_RetourneSuccesEtPersisteLaParticipation()
    {
        // Arrange
        var service = CreerServiceParticipation(out var participantRepository, out var participationRepository);
        var participant = await service.CreerParticipationAsync("sondage-test");

        // Act
        var (resultat, participation, erreurs) = await service.SoumettreAsync(participant!.Cle, DtoReponsesValides());

        // Assert
        Assert.Equal(ResultatSoumission.Succes, resultat);
        Assert.NotNull(participation);
        Assert.Equal("sondage-test", participation!.SondageId);
        Assert.Equal(participant.Cle, participation.ParticipantCle);
        Assert.Equal(2, participation.Reponses.Count);
        Assert.Empty(erreurs);

        var participantApres = await participantRepository.FindAsync(participant.Cle);
        Assert.True(participantApres!.EstUtilisee);
        var toutes = await participationRepository.GetAllAsync();
        Assert.Contains(toutes, p => p.Id == participation.Id);
    }

    [Fact]
    public async Task SoumettreAsync_TryConsumeAsyncRetourneIntrouvable_RetourneCleInvalide()
    {
        // Arrange : cas impossible avec un vrai repository (FindAsync vient
        // de retrouver ce meme participant juste avant, et il n'existe aucune
        // suppression) -- TryConsumeAsync est donc simule via Moq pour
        // forcer ce filet de securite defensif.
        var participantRepositoryMock = new Mock<ParticipantRepository>(CreerConfigurationStorage()) { CallBase = true };
        participantRepositoryMock
            .Setup(r => r.TryConsumeAsync(It.IsAny<string>()))
            .ReturnsAsync(ResultatConsommationCle.Introuvable);
        await participantRepositoryMock.Object.AddAsync(new Participant
        {
            Cle = "cle-fantome",
            SondageId = "sondage-test",
            DateEmission = DateTime.UtcNow,
            EstUtilisee = false
        });
        var service = new ServiceParticipation(participantRepositoryMock.Object, new ParticipationRepository(CreerConfigurationStorage()), CreerServiceSondageAvecUnSondage());

        // Act
        var (resultat, participation, erreurs) = await service.SoumettreAsync("cle-fantome", DtoReponsesValides());

        // Assert
        Assert.Equal(ResultatSoumission.CleInvalide, resultat);
        Assert.Null(participation);
        Assert.Empty(erreurs);
    }
}
