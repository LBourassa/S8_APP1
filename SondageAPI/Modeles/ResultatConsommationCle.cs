namespace SondageAPI.Modeles;

/// <summary>
/// Resultat de la tentative de consommation atomique d'une cle de
/// participant (verification + marquage comme utilisee, sous un seul
/// verrou). Comme la cle porte elle-meme son SondageId, il n'y a plus de cas
/// "mauvais sondage" a distinguer : une cle ne peut structurellement servir
/// qu'au sondage pour lequel elle a ete emise.
/// </summary>
public enum ResultatConsommationCle
{
    Succes,
    Introuvable,
    DejaUtilisee
}
