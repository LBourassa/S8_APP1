using Microsoft.Extensions.Configuration;
using SondageAPI.Modeles;
using SondageAPI.Repositories;

namespace SondageAPI.Tests.Repositories;

/// <summary>
/// Tests de ParticipantRepository : couvre chaque branche du constructeur
/// (config presente/absente), de ReadAllAsync (fichier absent, blanc, JSON
/// "null", valide), de FindAsync et des 3 issues de TryConsumeAsync.
/// </summary>
public class ParticipantRepositoryTests
{
    private static IConfiguration CreerConfiguration(string dossier) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Storage:DataDirectory"] = dossier
            })
            .Build();

    private static string CreerDossierTemp()
    {
        var dossier = Path.Combine(Path.GetTempPath(), "SondageApiTests_Participant_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dossier);
        return dossier;
    }

    // --- Constructeur : cle de config presente vs absente ---

    [Fact]
    public async Task Constructeur_StorageDataDirectoryConfigure_UtiliseCeDossier()
    {
        // Arrange
        var dossierTemp = CreerDossierTemp();
        var repository = new ParticipantRepository(CreerConfiguration(dossierTemp));

        // Act
        await repository.AddAsync(new Participant { Cle = "cle1", SondageId = "s1" });

        // Assert
        Assert.True(File.Exists(Path.Combine(dossierTemp, "participants.json")));
    }

    [Fact]
    public void Constructeur_StorageDataDirectoryAbsent_UtiliseDossierDataParDefaut()
    {
        // Arrange : aucune cle "Storage:DataDirectory" -> le constructeur
        // emprunte la branche "?? Data" et cree "Data" sous le repertoire
        // courant du processus (dossier de sortie des tests). Rien a nettoyer
        // ici : le constructeur ne fait que Directory.CreateDirectory, qui est
        // sans effet si "Data" existe deja (partage sans risque avec le test
        // equivalent de ParticipationRepositoryTests).
        var configurationSansCle = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var dossierDataParDefaut = Path.Combine(Directory.GetCurrentDirectory(), "Data");

        // Act
        _ = new ParticipantRepository(configurationSansCle);

        // Assert
        Assert.True(Directory.Exists(dossierDataParDefaut));
    }

    // --- ReadAllAsync (privee, testee via FindAsync) : fichier absent / blanc / JSON null / valide ---

    [Fact]
    public async Task FindAsync_FichierAbsent_RetourneNull()
    {
        // Arrange
        var repository = new ParticipantRepository(CreerConfiguration(CreerDossierTemp()));

        // Act
        var resultat = await repository.FindAsync("nimporte-quoi");

        // Assert
        Assert.Null(resultat);
    }

    [Fact]
    public async Task FindAsync_FichierBlanc_RetourneNull()
    {
        // Arrange
        var dossierTemp = CreerDossierTemp();
        await File.WriteAllTextAsync(Path.Combine(dossierTemp, "participants.json"), "   ");
        var repository = new ParticipantRepository(CreerConfiguration(dossierTemp));

        // Act
        var resultat = await repository.FindAsync("nimporte-quoi");

        // Assert
        Assert.Null(resultat);
    }

    [Fact]
    public async Task FindAsync_FichierJsonNull_RetourneNull()
    {
        // Arrange
        var dossierTemp = CreerDossierTemp();
        await File.WriteAllTextAsync(Path.Combine(dossierTemp, "participants.json"), "null");
        var repository = new ParticipantRepository(CreerConfiguration(dossierTemp));

        // Act
        var resultat = await repository.FindAsync("nimporte-quoi");

        // Assert
        Assert.Null(resultat);
    }

    [Fact]
    public async Task FindAsync_CleExistante_RetourneLeParticipant()
    {
        // Arrange
        var repository = new ParticipantRepository(CreerConfiguration(CreerDossierTemp()));
        await repository.AddAsync(new Participant { Cle = "cle-existante", SondageId = "s1" });

        // Act
        var resultat = await repository.FindAsync("cle-existante");

        // Assert
        Assert.NotNull(resultat);
        Assert.Equal("cle-existante", resultat!.Cle);
    }

    [Fact]
    public async Task FindAsync_CleInexistante_RetourneNull()
    {
        // Arrange
        var repository = new ParticipantRepository(CreerConfiguration(CreerDossierTemp()));
        await repository.AddAsync(new Participant { Cle = "autre-cle", SondageId = "s1" });

        // Act
        var resultat = await repository.FindAsync("cle-absente");

        // Assert
        Assert.Null(resultat);
    }

    // --- TryConsumeAsync : introuvable / deja utilisee / succes ---

    [Fact]
    public async Task TryConsumeAsync_CleIntrouvable_RetourneIntrouvable()
    {
        // Arrange
        var repository = new ParticipantRepository(CreerConfiguration(CreerDossierTemp()));

        // Act
        var resultat = await repository.TryConsumeAsync("inexistante");

        // Assert
        Assert.Equal(ResultatConsommationCle.Introuvable, resultat);
    }

    [Fact]
    public async Task TryConsumeAsync_CleDejaUtilisee_RetourneDejaUtiliseeEtNeLaModifiePas()
    {
        // Arrange
        var repository = new ParticipantRepository(CreerConfiguration(CreerDossierTemp()));
        var dateUtilisationOriginale = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        await repository.AddAsync(new Participant
        {
            Cle = "cle-utilisee",
            SondageId = "s1",
            EstUtilisee = true,
            DateUtilisation = dateUtilisationOriginale
        });

        // Act
        var resultat = await repository.TryConsumeAsync("cle-utilisee");

        // Assert
        Assert.Equal(ResultatConsommationCle.DejaUtilisee, resultat);
        var participant = await repository.FindAsync("cle-utilisee");
        Assert.Equal(dateUtilisationOriginale, participant!.DateUtilisation);
    }

    [Fact]
    public async Task TryConsumeAsync_CleValideEtInutilisee_RetourneSuccesEtMarqueUtilisee()
    {
        // Arrange
        var repository = new ParticipantRepository(CreerConfiguration(CreerDossierTemp()));
        await repository.AddAsync(new Participant { Cle = "cle-fraiche", SondageId = "s1", EstUtilisee = false });

        // Act
        var resultat = await repository.TryConsumeAsync("cle-fraiche");

        // Assert
        Assert.Equal(ResultatConsommationCle.Succes, resultat);
        var participant = await repository.FindAsync("cle-fraiche");
        Assert.NotNull(participant);
        Assert.True(participant!.EstUtilisee);
        Assert.NotNull(participant.DateUtilisation);
    }
}
