namespace SondageAPI.Modeles;

/// <summary>
/// Un sondage complet, charge depuis un fichier texte (voir ServiceSondage).
/// L'Id correspond au nom du fichier sans extension (ex. "sondage1.txt" -> Id = "sondage1").
/// </summary>
public class Sondage
{
    public string Id { get; set; } = string.Empty;
    public string Titre { get; set; } = string.Empty;
    public List<Question> Questions { get; set; } = new();
}
