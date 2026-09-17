using System.Text.Json;
using SondageAPI.Modeles;

namespace SondageAPI.Repositories;

/// <summary>
/// Persistance basee sur un fichier JSON local. Toutes les operations
/// passent par le meme verrou (Lock) afin que "verifier que la cle existe et
/// n'est pas utilisee" et "la marquer comme utilisee" soient atomiques.
/// </summary>
public class ParticipantRepository
{
    private readonly string _filePath;
    private static readonly SemaphoreSlim Lock = new(1, 1);

    public ParticipantRepository(IConfiguration configuration)
    {
        var dataDir = configuration["Storage:DataDirectory"] ?? "Data";
        Directory.CreateDirectory(dataDir);
        _filePath = Path.Combine(dataDir, "participants.json");
    }

    public async Task AddAsync(Participant participant)
    {
        await Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var participants = await ReadAllAsync().ConfigureAwait(false);
            participants.Add(participant);
            await WriteAllAsync(participants).ConfigureAwait(false);
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task<Participant?> FindAsync(string cle)
    {
        await Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var participants = await ReadAllAsync().ConfigureAwait(false);
            return participants.FirstOrDefault(p => p.Cle == cle);
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task<ResultatConsommationCle> TryConsumeAsync(string cle)
    {
        await Lock.WaitAsync().ConfigureAwait(false);
        try
        {
            var participants = await ReadAllAsync().ConfigureAwait(false);
            var found = participants.FirstOrDefault(p => p.Cle == cle);

            if (found is null)
            {
                return ResultatConsommationCle.Introuvable;
            }

            if (found.EstUtilisee)
            {
                return ResultatConsommationCle.DejaUtilisee;
            }

            found.EstUtilisee = true;
            found.DateUtilisation = DateTime.UtcNow;
            await WriteAllAsync(participants).ConfigureAwait(false);
            return ResultatConsommationCle.Succes;
        }
        finally
        {
            Lock.Release();
        }
    }

    private async Task<List<Participant>> ReadAllAsync()
    {
        if (!File.Exists(_filePath))
        {
            return new List<Participant>();
        }

        var json = await File.ReadAllTextAsync(_filePath).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Participant>();
        }

        return JsonSerializer.Deserialize<List<Participant>>(json) ?? new List<Participant>();
    }

    private async Task WriteAllAsync(List<Participant> participants)
    {
        var json = JsonSerializer.Serialize(participants, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(_filePath, json).ConfigureAwait(false);
    }
}
