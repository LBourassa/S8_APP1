namespace SondageAPI.DTO;

public class QuestionDto
{
    public string Id { get; set; } = string.Empty;
    public string Texte { get; set; } = string.Empty;
    public List<OptionDto> Options { get; set; } = new();
}
