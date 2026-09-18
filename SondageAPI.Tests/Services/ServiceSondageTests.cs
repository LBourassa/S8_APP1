using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Moq;
using SondageAPI.DTO;
using SondageAPI.Modeles;
using SondageAPI.Services;

namespace SondageAPI.Tests.Services;

/// <summary>
/// Tests de ServiceSondage (lit reellement des .txt sur disque) : couvre
/// chaque branche du constructeur (chemin absolu/relatif, dossier
/// absent/vide/rempli, JSON malforme/"null"/0 question), de ObtenirParId et
/// de ValiderReponses.
/// </summary>
public class ServiceSondageTests
{
    private static Mock<IHostEnvironment> CreerEnvironnement(string contentRootPath)
    {
        var environnement = new Mock<IHostEnvironment>();
        environnement.SetupGet(e => e.ContentRootPath).Returns(contentRootPath);
        return environnement;
    }

    private static string CreerDossierTemp()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "SondageApiTests_Sondage_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        return dossier;
    }

    private static ServiceSondage CreerService(string dossierSondages, string? contentRootPath = null) =>
        new(
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Storage:SurveysDirectory"] = dossierSondages })
                .Build(),
            CreerEnvironnement(contentRootPath ?? "/inutilise").Object);

    private static ServiceSondage CreerServiceSansSondages() =>
        CreerService(Path.Combine(Path.GetTempPath(), "SondageApiTests_Inexistant_" + Guid.NewGuid().ToString("N")));

    private const string JsonUneQuestion = """
        {
          "titre": "Sondage test",
          "questions": [
            { "id": "q1", "texte": "Question 1?", "options": [ { "cle": "a", "texte": "Option A" }, { "cle": "b", "texte": "Option B" } ] }
          ]
        }
        """;

    private const string JsonZeroQuestion = """{ "titre": "Sondage vide", "questions": [] }""";

    // --- Constructeur : cle de config presente (chemin absolu) vs absente (chemin relatif + ContentRootPath) ---

    [Fact]
    public void Constructeur_SurveysDirectoryConfigureCheminAbsolu_ChargeLesSondagesDeCeDossier()
    {
        // Arrange : chemin absolu -> Path.IsPathRooted vrai, ContentRootPath jamais consulte.
        var dossierTemp = CreerDossierTemp();
        File.WriteAllText(Path.Combine(dossierTemp, "sondageA.txt"), JsonUneQuestion);

        // Act
        var service = CreerService(dossierTemp, contentRootPath: "/chemin/jamais/utilise");

        // Assert
        var sondage = Assert.Single(service.ObtenirTous());
        Assert.Equal("sondageA", sondage.Id);
    }

    [Fact]
    public void Constructeur_SurveysDirectoryAbsent_UtiliseSondagesSousContentRootPath()
    {
        // Arrange : aucune cle "Storage:SurveysDirectory" -> repli sur
        // "Sondages" (chemin relatif) -> Path.IsPathRooted faux -> combine
        // avec ContentRootPath.
        var dossierRacine = CreerDossierTemp();
        var dossierSondages = Path.Combine(dossierRacine, "Sondages");
        Directory.CreateDirectory(dossierSondages);
        File.WriteAllText(Path.Combine(dossierSondages, "sondageB.txt"), JsonUneQuestion);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

        // Act
        var service = new ServiceSondage(configuration, CreerEnvironnement(dossierRacine).Object);

        // Assert
        var sondage = Assert.Single(service.ObtenirTous());
        Assert.Equal("sondageB", sondage.Id);
    }

    // --- Constructeur : dossier inexistant / existant mais vide ---

    [Fact]
    public void Constructeur_DossierInexistant_NeChargeAucunSondage()
    {
        // Act
        var service = CreerServiceSansSondages();

        // Assert
        Assert.Empty(service.ObtenirTous());
    }

    [Fact]
    public void Constructeur_DossierExistantMaisVide_NeChargeAucunSondage()
    {
        // Act
        var service = CreerService(CreerDossierTemp());

        // Assert
        Assert.Empty(service.ObtenirTous());
    }

    // --- ParseFichierSondage (privee, testee via le constructeur) ---

    [Fact]
    public void Constructeur_FichierJsonMalforme_IgnoreCeFichierSansPlanter()
    {
        // Arrange : declenche la JsonException interceptee dans le catch de ParseFichierSondage.
        var dossierTemp = CreerDossierTemp();
        File.WriteAllText(Path.Combine(dossierTemp, "invalide.txt"), "ceci n'est pas du JSON {{{");

        // Act
        var service = CreerService(dossierTemp);

        // Assert
        Assert.Empty(service.ObtenirTous());
    }

    [Fact]
    public void Constructeur_FichierJsonNull_IgnoreCeFichier()
    {
        // Arrange : JSON syntaxiquement valide qui se deserialise en null
        // (branche "if (sondage is null) return null;", distincte du catch).
        var dossierTemp = CreerDossierTemp();
        File.WriteAllText(Path.Combine(dossierTemp, "nul.txt"), "null");

        // Act
        var service = CreerService(dossierTemp);

        // Assert
        Assert.Empty(service.ObtenirTous());
    }

    [Fact]
    public void Constructeur_SondageAvecZeroQuestion_EstIgnore()
    {
        // Arrange
        var dossierTemp = CreerDossierTemp();
        File.WriteAllText(Path.Combine(dossierTemp, "vide.txt"), JsonZeroQuestion);

        // Act
        var service = CreerService(dossierTemp);

        // Assert
        Assert.Empty(service.ObtenirTous());
    }

    [Fact]
    public void Constructeur_PlusieursFichiers_NeChargeQueLesSondagesValidesAvecQuestions()
    {
        // Arrange : melange d'un fichier valide, d'un fichier "null", d'un
        // fichier malforme et d'un fichier a 0 question dans le meme dossier
        // -- seul le premier doit survivre.
        var dossierTemp = CreerDossierTemp();
        File.WriteAllText(Path.Combine(dossierTemp, "valide.txt"), JsonUneQuestion);
        File.WriteAllText(Path.Combine(dossierTemp, "nul.txt"), "null");
        File.WriteAllText(Path.Combine(dossierTemp, "malforme.txt"), "{{{");
        File.WriteAllText(Path.Combine(dossierTemp, "zeroquestion.txt"), JsonZeroQuestion);

        // Act
        var service = CreerService(dossierTemp);

        // Assert
        var sondage = Assert.Single(service.ObtenirTous());
        Assert.Equal("valide", sondage.Id);
    }

    // --- ObtenirParId : trouve / introuvable ---

    [Fact]
    public void ObtenirParId_SondageExistant_RetourneLeSondage()
    {
        // Arrange
        var dossierTemp = CreerDossierTemp();
        File.WriteAllText(Path.Combine(dossierTemp, "sondageC.txt"), JsonUneQuestion);
        var service = CreerService(dossierTemp);

        // Act
        var sondage = service.ObtenirParId("sondageC");

        // Assert
        Assert.NotNull(sondage);
        Assert.Equal("sondageC", sondage!.Id);
    }

    [Fact]
    public void ObtenirParId_SondageInexistant_RetourneNull()
    {
        // Arrange
        var service = CreerServiceSansSondages();

        // Act
        var sondage = service.ObtenirParId("inexistant");

        // Assert
        Assert.Null(sondage);
    }

    // --- ValiderReponses ---

    private static Sondage CreerSondageDeuxQuestions() => new()
    {
        Id = "sondage-test",
        Titre = "Sondage de test",
        Questions = new List<Question>
        {
            new() { Id = "q1", Texte = "Q1?", Options = new List<Option> { new() { Cle = "a", Texte = "A" }, new() { Cle = "b", Texte = "B" } } },
            new() { Id = "q2", Texte = "Q2?", Options = new List<Option> { new() { Cle = "x", Texte = "X" }, new() { Cle = "y", Texte = "Y" } } }
        }
    };

    [Fact]
    public void ValiderReponses_ToutesReponsesValides_RetourneAucuneErreur()
    {
        // Arrange
        var service = CreerServiceSansSondages();
        var sondage = CreerSondageDeuxQuestions();
        var reponses = new List<ReponseDto>
        {
            new() { QuestionId = "q1", Valeur = "a" },
            new() { QuestionId = "q2", Valeur = "Y" } // casse differente, geree par Trim().ToLowerInvariant()
        };

        // Act
        var erreurs = service.ValiderReponses(sondage, reponses);

        // Assert
        Assert.Empty(erreurs);
    }

    [Fact]
    public void ValiderReponses_ReponseManquantePourUneQuestion_AjouteErreur()
    {
        // Arrange : q2 totalement absente des reponses.
        var service = CreerServiceSansSondages();
        var sondage = CreerSondageDeuxQuestions();
        var reponses = new List<ReponseDto> { new() { QuestionId = "q1", Valeur = "a" } };

        // Act
        var erreurs = service.ValiderReponses(sondage, reponses);

        // Assert
        Assert.Contains(erreurs, e => e.Contains("Reponse manquante") && e.Contains("q2"));
    }

    [Fact]
    public void ValiderReponses_ReponseValeurBlanche_AjouteErreurReponseManquante()
    {
        // Arrange : q2 presente dans les reponses mais avec une valeur blanche.
        var service = CreerServiceSansSondages();
        var sondage = CreerSondageDeuxQuestions();
        var reponses = new List<ReponseDto>
        {
            new() { QuestionId = "q1", Valeur = "a" },
            new() { QuestionId = "q2", Valeur = "   " }
        };

        // Act
        var erreurs = service.ValiderReponses(sondage, reponses);

        // Assert
        Assert.Contains(erreurs, e => e.Contains("Reponse manquante") && e.Contains("q2"));
    }

    [Fact]
    public void ValiderReponses_ValeurInvalidePourLaQuestion_AjouteErreur()
    {
        // Arrange : "z" n'existe pas parmi les options de q1.
        var service = CreerServiceSansSondages();
        var sondage = CreerSondageDeuxQuestions();
        var reponses = new List<ReponseDto>
        {
            new() { QuestionId = "q1", Valeur = "z" },
            new() { QuestionId = "q2", Valeur = "x" }
        };

        // Act
        var erreurs = service.ValiderReponses(sondage, reponses);

        // Assert
        Assert.Contains(erreurs, e => e.Contains("invalide") && e.Contains("q1"));
    }

    [Fact]
    public void ValiderReponses_QuestionInconnueDansLesReponses_AjouteErreur()
    {
        // Arrange : q99 n'existe pas dans le sondage.
        var service = CreerServiceSansSondages();
        var sondage = CreerSondageDeuxQuestions();
        var reponses = new List<ReponseDto>
        {
            new() { QuestionId = "q1", Valeur = "a" },
            new() { QuestionId = "q2", Valeur = "x" },
            new() { QuestionId = "q99", Valeur = "a" }
        };

        // Act
        var erreurs = service.ValiderReponses(sondage, reponses);

        // Assert
        Assert.Contains(erreurs, e => e.Contains("Question inconnue") && e.Contains("q99"));
    }

    [Fact]
    public void ValiderReponses_SondageSansQuestion_RetourneAucuneErreur()
    {
        // Arrange
        var service = CreerServiceSansSondages();
        var sondage = new Sondage { Id = "vide", Titre = "Vide", Questions = new List<Question>() };

        // Act
        var erreurs = service.ValiderReponses(sondage, new List<ReponseDto>());

        // Assert
        Assert.Empty(erreurs);
    }
}
