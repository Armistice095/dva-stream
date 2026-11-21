namespace DvaStream.Core.Errors;

/// <summary>
/// Erreur métier : sa nature (<see cref="Kind"/>) décide du message montré à l'utilisateur ; les détails
/// techniques (sortie du moteur, code de sortie) ne servent qu'au journal et à « Copier les détails » (FR-42).
/// </summary>
/// <param name="Kind">Nature de l'erreur.</param>
/// <param name="TechnicalDetails">Détails pour le journal et le diagnostic, jamais affichés tels quels.</param>
/// <param name="Inner">Exception d'origine, s'il y en a une.</param>
public sealed record AppError(ErrorKind Kind, string TechnicalDetails, Exception? Inner = null);
