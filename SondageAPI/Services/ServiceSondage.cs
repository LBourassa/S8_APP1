using System.Text.Json;
using SondageAPI.DTO;
using SondageAPI.Modeles;

namespace SondageAPI.Services;

/// <summary>
/// Charge les sondages depuis des fichiers texte (un fichier .txt par
/// sondage, voir le dossier Sondages/) au demarrage de l'application. C'est
/// la reponse au requis "la compagnie fournit les sondages en format
/// texte" : les questions ne sont pas codees en dur dans le controleur,
/// elles viennent d'un fichier que Coup de Sonde peut modifier sans
/// recompiler.
///
/// Le contenu de chaque fichier est du JSON dans un format fichier .txt
///
/// Format attendu du fichier :
///   {
///     "titre": "Mon sondage",
///     "questions": [
///       {
///         "id": "q1",
///         "texte": "Texte de la question?",
///         "options": [
///           { "cle": "a", "texte": "Option A" },
///           { "cle": "b", "texte": "Option B" }
///         ]
///       }
///     ]
///   }
///
/// L'Id du sondage lui-meme vient du nom du fichier (pas du JSON), comme
/// avant : "sondage1.txt" -> Id "sondage1".
/// </summary>
public class ServiceSondage
{
    private static readonly JsonSerializerOptions OptionsJson = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly Dictionary<string, Sondage> _sondagesParId;

    public ServiceSondage(IConfiguration configuration, IHostEnvironment environment)
    {
        var relativeDir = configuration["Storage:SurveysDirectory"] ?? "Sondages";
        var dir = Path.IsPathRooted(relativeDir)
            ? relativeDir
            : Path.Combine(environment.ContentRootPath, relativeDir);

        _sondagesParId = new Dictionary<string, Sondage>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(dir))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(dir, "*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase))
        {
            var sondage = ParseFichierSondage(filePath);
            if (sondage is not null && sondage.Questions.Count > 0)
            {
                _sondagesParId[sondage.Id] = sondage;
            }
        }
    }

    // "virtual" : uniquement pour permettre a Moq de les simuler dans les
    // tests des controleurs (SondagesController), sans reintroduire
    // d'interface. Le comportement reel de la methode n'est pas affecte.
    public virtual IReadOnlyList<Sondage> ObtenirTous() => _sondagesParId.Values.ToList();

    public virtual Sondage? ObtenirParId(string sondageId) =>
        _sondagesParId.TryGetValue(sondageId, out var sondage) ? sondage : null;

    public IReadOnlyList<string> ValiderReponses(Sondage sondage, IEnumerable<ReponseDto> reponses)
    {
        var erreurs = new List<string>();

        var reponsesParQuestion = reponses
            .GroupBy(r => r.QuestionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Valeur, StringComparer.OrdinalIgnoreCase);

        foreach (var question in sondage.Questions)
        {
            if (!reponsesParQuestion.TryGetValue(question.Id, out var valeur) || string.IsNullOrWhiteSpace(valeur))
            {
                erreurs.Add($"Reponse manquante pour la question '{question.Id}'.");
                continue;
            }

            var valeurNormalisee = valeur.Trim().ToLowerInvariant();
            if (!question.Options.Any(o => o.Cle == valeurNormalisee))
            {
                erreurs.Add($"Valeur '{valeur}' invalide pour la question '{question.Id}'.");
            }
        }

        var questionsConnues = new HashSet<string>(sondage.Questions.Select(q => q.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var idInconnu in reponsesParQuestion.Keys.Where(id => !questionsConnues.Contains(id)))
        {
            erreurs.Add($"Question inconnue pour ce sondage : '{idInconnu}'.");
        }

        return erreurs;
    }

    /// <summary>
    /// Deserialise un fichier de sondage JSON. Retourne null si le fichier
    /// est illisible ou mal forme, plutot que de faire planter le demarrage
    /// de l'application pour un seul fichier invalide.
    /// </summary>
    private static Sondage? ParseFichierSondage(string filePath)
    {
        var id = Path.GetFileNameWithoutExtension(filePath);

        try
        {
            var json = File.ReadAllText(filePath);
            var sondage = JsonSerializer.Deserialize<Sondage>(json, OptionsJson);
            if (sondage is null)
            {
                return null;
            }

            sondage.Id = id;
            return sondage;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
