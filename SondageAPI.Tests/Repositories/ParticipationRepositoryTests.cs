using Microsoft.Extensions.Configuration;
using SondageAPI.Modeles;
using SondageAPI.Repositories;

namespace SondageAPI.Tests.Repositories;

/// <summary>
/// Tests de ParticipationRepository : couvre chaque branche du constructeur
/// (config presente/absente), de GetAllAsync (dossier vide, fichier valide,
/// blanc, ou JSON "null") et de BuildFileName (cle sanitisee vide/non vide).
/// </summary>
public class ParticipationRepositoryTests
{
    private static IConfiguration CreerConfiguration(string dossier) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DataDirectory"] = dossier
            })
            .Build();

    private static string CreerDossierTemp() =>
        Path.Combine(Path.GetTempPath(), "SondageApiTests_Participation_" + Guid.NewGuid().ToString("N"));

    // --- Constructeur : cle de config presente vs absente ---

    [Fact]
    public async Task Constructeur_StorageDataDirectoryConfigure_UtiliseCeDossier()
    {
        // Arrange
        var dossierTemp = CreerDossierTemp();
        var repository = new ParticipationRepository(CreerConfiguration(dossierTemp));

        // Act
        await repository.AddAsync(new Participation { ParticipantCle = "cle1", SondageId = "s1" });

        // Assert
        Assert.True(Directory.Exists(Path.Combine(dossierTemp, "Participations")));
    }

    [Fact]
    public void Constructeur_StorageDataDirectoryAbsent_UtiliseDossierDataParDefaut()
    {
        // Arrange : aucune cle "Storage:DataDirectory" -> le constructeur
        // emprunte la branche "?? Data" et cree "Data/Participations" sous le
        // repertoire courant du processus (dossier de sortie des tests).
        var configurationSansCle = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        // Sous-dossier propre a ce repository : ne supprime que "Participations"
        // au nettoyage, jamais "Data" en entier (partage avec ParticipantRepository).
        var dossierParticipationsParDefaut = Path.Combine(Directory.GetCurrentDirectory(), "Data", "Participations");

        try
        {
            // Act
            _ = new ParticipationRepository(configurationSansCle);

            // Assert
            Assert.True(Directory.Exists(dossierParticipationsParDefaut));
        }
        finally
        {
            if (Directory.Exists(dossierParticipationsParDefaut))
            {
                Directory.Delete(dossierParticipationsParDefaut, recursive: true);
            }
        }
    }

    // --- GetAllAsync : dossier vide / fichier valide / fichier blanc / JSON null ---

    [Fact]
    public async Task GetAllAsync_DossierVide_RetourneListeVide()
    {
        // Arrange
        var repository = new ParticipationRepository(CreerConfiguration(CreerDossierTemp()));

        // Act
        var resultats = await repository.GetAllAsync();

        // Assert
        Assert.Empty(resultats);
    }

    [Fact]
    public async Task GetAllAsync_FichierValide_RetourneLaParticipation()
    {
        // Arrange
        var repository = new ParticipationRepository(CreerConfiguration(CreerDossierTemp()));
        var participation = new Participation { ParticipantCle = "cle-valide", SondageId = "s1" };
        await repository.AddAsync(participation);

        // Act
        var resultats = await repository.GetAllAsync();

        // Assert
        var trouvee = Assert.Single(resultats);
        Assert.Equal(participation.Id, trouvee.Id);
        Assert.Equal("s1", trouvee.SondageId);
    }

    [Fact]
    public async Task GetAllAsync_FichierBlanc_EstIgnore()
    {
        // Arrange
        var dossierTemp = CreerDossierTemp();
        var repository = new ParticipationRepository(CreerConfiguration(dossierTemp));
        var dossierParticipations = Path.Combine(dossierTemp, "Participations");
        await File.WriteAllTextAsync(Path.Combine(dossierParticipations, "vide.txt"), "   ");

        // Act
        var resultats = await repository.GetAllAsync();

        // Assert
        Assert.Empty(resultats);
    }

    [Fact]
    public async Task GetAllAsync_FichierJsonNull_EstIgnore()
    {
        // Arrange
        var dossierTemp = CreerDossierTemp();
        var repository = new ParticipationRepository(CreerConfiguration(dossierTemp));
        var dossierParticipations = Path.Combine(dossierTemp, "Participations");
        await File.WriteAllTextAsync(Path.Combine(dossierParticipations, "nul.txt"), "null");

        // Act
        var resultats = await repository.GetAllAsync();

        // Assert
        Assert.Empty(resultats);
    }

    // --- BuildFileName (privee, testee via AddAsync) : cle sanitisee non vide / vide ---

    [Fact]
    public async Task AddAsync_CleParticipantNonVide_NommeLeFichierDapresLaCle()
    {
        // Arrange
        var dossierTemp = CreerDossierTemp();
        var repository = new ParticipationRepository(CreerConfiguration(dossierTemp));
        var participation = new Participation { ParticipantCle = "cle-abc", SondageId = "s1" };

        // Act
        await repository.AddAsync(participation);

        // Assert
        Assert.True(File.Exists(Path.Combine(dossierTemp, "Participations", "cle-abc.txt")));
    }

    [Fact]
    public async Task AddAsync_CleParticipantVide_NommeLeFichierDapresLId()
    {
        // Arrange : une cle vide se reduit a une chaine vide apres filtrage
        // des caracteres invalides, ce qui declenche le repli sur l'Id.
        var dossierTemp = CreerDossierTemp();
        var repository = new ParticipationRepository(CreerConfiguration(dossierTemp));
        var participation = new Participation { ParticipantCle = "", SondageId = "s1" };

        // Act
        await repository.AddAsync(participation);

        // Assert
        var cheminAttendu = Path.Combine(dossierTemp, "Participations", $"{participation.Id:N}.txt");
        Assert.True(File.Exists(cheminAttendu));
    }
}
