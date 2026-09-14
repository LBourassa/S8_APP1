using System.Text.Json;

namespace SondageAPI.Services
{
    // 1. Modèle de données qui représente ce que Postman va envoyer
    public class Submission
    {
        public string UserId { get; set; } = string.Empty;
        public Dictionary<string, string> Answers { get; set; } = new();
    }

    // 2. Le service qui gère le fichier JSON
    public class SondageService
    {
        private readonly string _filePath = "responses.json";
        // Sécurité pour éviter que le fichier plante si 2 personnes votent à la même milliseconde
        private static readonly SemaphoreSlim _lock = new(1, 1);

        public async Task<bool> HasUserSubmittedAsync(string userId)
        {
            var submissions = await GetAllSubmissionsAsync();
            return submissions.Any(s => s.UserId == userId);
        }

        public async Task SaveSubmissionAsync(Submission submission)
        {
            await _lock.WaitAsync();
            try
            {
                var submissions = await GetAllSubmissionsAsync();
                submissions.Add(submission);

                var json = JsonSerializer.Serialize(submissions, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_filePath, json);
            }
            finally
            {
                _lock.Release();
            }
        }

        private async Task<List<Submission>> GetAllSubmissionsAsync()
        {
            if (!File.Exists(_filePath)) return new List<Submission>();
            var json = await File.ReadAllTextAsync(_filePath);
            return JsonSerializer.Deserialize<List<Submission>>(json) ?? new List<Submission>();
        }
    }
}