using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using SondageAPI.Controleurs;
using SondageAPI.DTO;
using SondageAPI.Modeles;
using SondageAPI.Services;

namespace SondageAPI.Tests.Controleurs;

/// <summary>
/// Tests de SondagesController. ServiceSondage est simulee avec Moq (ses
/// methodes ObtenirTous/ObtenirParId sont "virtual" uniquement pour cette
/// raison) afin de tester le controleur seul, sans dependre de fichiers sur
/// disque. Chaque branche des deux actions du controleur est couverte.
/// </summary>
public class SondagesControllerTests
{
    /// <summary>
    /// Construit une Mock&lt;ServiceSondage&gt; utilisable : ServiceSondage n'a
    /// pas d'interface, donc son constructeur reel (IConfiguration,
    /// IHostEnvironment) doit recevoir des arguments valides pour que Moq
    /// puisse instancier l'objet, meme si son corps n'est jamais vraiment
    /// exploite (les methodes qui nous interessent sont simulees).
    /// </summary>
    private static Mock<ServiceSondage> CreerServiceSondageMock()
    {
        var configuration = new ConfigurationBuilder().Build();

        var environnement = new Mock<IHostEnvironment>();
        environnement.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

        return new Mock<ServiceSondage>(configuration, environnement.Object);
    }

    // --- ObtenirSondages ---

    [Fact]
    public void ObtenirSondages_PlusieursSondages_RetourneOkAvecResumes()
    {
        // Arrange
        var serviceSondage = CreerServiceSondageMock();
        var sondages = new List<Sondage>
        {
            new()
            {
                Id = "sondage1",
                Titre = "Premier sondage",
                Questions = new List<Question> { new() { Id = "q1" }, new() { Id = "q2" } }
            },
            new()
            {
                Id = "sondage2",
                Titre = "Deuxieme sondage",
                Questions = new List<Question> { new() { Id = "q1" } }
            }
        };
        serviceSondage.Setup(s => s.ObtenirTous()).Returns(sondages);
        var controleur = new SondagesController(serviceSondage.Object);

        // Act
        var resultat = controleur.ObtenirSondages();

        // Assert
        var resultatOk = Assert.IsType<OkObjectResult>(resultat.Result);
        var resumes = Assert.IsAssignableFrom<IEnumerable<SondageResumeDto>>(resultatOk.Value).ToList();
        Assert.Equal(2, resumes.Count);
        Assert.Contains(resumes, r => r.Id == "sondage1" && r.Titre == "Premier sondage" && r.NombreQuestions == 2);
        Assert.Contains(resumes, r => r.Id == "sondage2" && r.Titre == "Deuxieme sondage" && r.NombreQuestions == 1);
    }

    [Fact]
    public void ObtenirSondages_AucunSondage_RetourneOkAvecListeVide()
    {
        // Arrange
        var serviceSondage = CreerServiceSondageMock();
        serviceSondage.Setup(s => s.ObtenirTous()).Returns(new List<Sondage>());
        var controleur = new SondagesController(serviceSondage.Object);

        // Act
        var resultat = controleur.ObtenirSondages();

        // Assert
        var resultatOk = Assert.IsType<OkObjectResult>(resultat.Result);
        var resumes = Assert.IsAssignableFrom<IEnumerable<SondageResumeDto>>(resultatOk.Value);
        Assert.Empty(resumes);
    }

    // --- ObtenirSondage(sondageId) : deux branches (introuvable / trouve) ---

    [Fact]
    public void ObtenirSondage_SondageIntrouvable_RetourneNotFound()
    {
        // Arrange
        var serviceSondage = CreerServiceSondageMock();
        serviceSondage.Setup(s => s.ObtenirParId("inexistant")).Returns((Sondage?)null);
        var controleur = new SondagesController(serviceSondage.Object);

        // Act
        var resultat = controleur.ObtenirSondage("inexistant");

        // Assert
        var resultatNotFound = Assert.IsType<NotFoundObjectResult>(resultat.Result);
        var erreur = Assert.IsType<ErreurDto>(resultatNotFound.Value);
        Assert.Contains("inexistant", erreur.Erreur);
    }

    [Fact]
    public void ObtenirSondage_SondageExistant_RetourneOkAvecDetailComplet()
    {
        // Arrange
        var serviceSondage = CreerServiceSondageMock();
        var sondage = new Sondage
        {
            Id = "sondage1",
            Titre = "Premier sondage",
            Questions = new List<Question>
            {
                new()
                {
                    Id = "q1",
                    Texte = "Quelle est votre couleur preferee ?",
                    Options = new List<Option>
                    {
                        new() { Cle = "a", Texte = "Rouge" },
                        new() { Cle = "b", Texte = "Bleu" }
                    }
                }
            }
        };
        serviceSondage.Setup(s => s.ObtenirParId("sondage1")).Returns(sondage);
        var controleur = new SondagesController(serviceSondage.Object);

        // Act
        var resultat = controleur.ObtenirSondage("sondage1");

        // Assert
        var resultatOk = Assert.IsType<OkObjectResult>(resultat.Result);
        var dto = Assert.IsType<SondageDto>(resultatOk.Value);
        Assert.Equal("sondage1", dto.Id);
        Assert.Equal("Premier sondage", dto.Titre);
        Assert.Single(dto.Questions);
        Assert.Equal("q1", dto.Questions[0].Id);
        Assert.Equal(2, dto.Questions[0].Options.Count);
        Assert.Contains(dto.Questions[0].Options, o => o.Cle == "a" && o.Texte == "Rouge");
    }
}
