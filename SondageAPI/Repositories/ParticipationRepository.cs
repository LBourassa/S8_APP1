using System.Text.Json;
using SondageAPI.Modeles;

namespace SondageAPI.Repositories;

/// <summary>
/// Chaque participation est enregistree dans son propre fichier .txt
/// (contenu au format JSON), nomme d'apres la cle de participant qui l'a
/// soumise. Comme chaque cle est a usage unique, il ne peut jamais y avoir
/// deux participations pour le meme fichier.
/// </summary>
public class ParticipationRepository
{
    private readonly string _directory;
    private static readonly SemaphoreSlim Lock = new(1, 1);

    public ParticipationRepository(IConfiguration configuration)
    {
        var dataDir = configuration["Storage:DataDirectory"] ?? "Data";
        _directory = Path.Combine(dataDir, "Participations");
        Directory.CreateDirectory(_directory);
    }

    public async Task AddAsync(Participation participation)
    {
        await Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var filePath = Path.Combine(_directory, BuildFileName(participation));
            var json = JsonSerializer.Serialize(participation, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task<IReadOnlyList<Participation>> GetAllAsync()
    {
        await Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var results = new List<Participation>();
            foreach (var filePath in Directory.EnumerateFiles(_directory, "*.txt"))
            {
                var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(json))
                {
                    continue;
                }

                var participation = JsonSerializer.Deserialize<Participation>(json);
                if (participation is not null)
                {
                    results.Add(participation);
                }
            }

            return results;
        }
        finally
        {
            Lock.Release();
        }
    }

    private static string BuildFileName(Participation participation)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitizedKey = new string(participation.ParticipantCle.Where(c => !invalidChars.Contains(c)).ToArray());
        if (string.IsNullOrWhiteSpace(sanitizedKey))
        {
            sanitizedKey = participation.Id.ToString("N");
        }

        return $"{sanitizedKey}.txt";
    }
}
