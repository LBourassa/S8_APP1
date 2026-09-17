namespace SondageAPI.Modeles;

/// <summary>
/// Une question a choix multiples du sondage. L'Id (ex. "q1") correspond au
/// QuestionId attendu dans ReponseDto.
/// </summary>
public class Question
{
    public string Id { get; set; } = string.Empty;
    public string Texte { get; set; } = string.Empty;
    public List<Option> Options { get; set; } = new();
}
