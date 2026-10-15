namespace WinSU.Core.Catalog;

/// <summary>Catalogue en lecture seule des consoles connues de WinSU.</summary>
public interface IConsoleCatalog
{
    /// <summary>Toutes les consoles, triées par identifiant. Vide tant que <see cref="LoadAsync"/> n'a pas réussi.</summary>
    IReadOnlyList<ConsoleDefinition> All { get; }

    /// <summary>
    /// La console de cet identifiant, ou d'un de ses alias : dans ce cas, elle est rendue sous cet identifiant (celui de la
    /// bibliothèque : jeux, déplacements, dossier des images). Null si la console n'est plus au catalogue.
    /// </summary>
    ConsoleDefinition? Find(string id);

    /// <summary>
    /// Charge le catalogue livré avec WinSU. Si le fichier manque ou est illisible, le catalogue reste vide et l'erreur est
    /// journalisée (l'application reste utilisable).
    /// </summary>
    Task LoadAsync(CancellationToken cancellationToken = default);
}
