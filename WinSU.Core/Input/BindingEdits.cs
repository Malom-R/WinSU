namespace WinSU.Core.Input;

public enum BindingEditStatus
{
    /// <summary>Modification faite (ou rien à changer).</summary>
    Done,

    /// <summary>L'entrée est déjà associée à cette action.</summary>
    AlreadyBound,

    /// <summary>L'entrée est déjà associée à une autre action (<see cref="BindingEdit{T}.Owner"/>) : refusé.</summary>
    UsedByOther,

    /// <summary>Retirer cette liaison laisserait une action essentielle sans aucune liaison : refusé.</summary>
    LastEssential,

    /// <summary>L'action a déjà <see cref="BindingEdits.MaxPerAction"/> liaisons : refusé (en remplacer une).</summary>
    Full,
}

/// <summary>Résultat d'une modification : la nouvelle table (inchangée si refusée) et ce qui s'est passé.</summary>
public readonly record struct BindingEdit<T>(
    IReadOnlyDictionary<InputAction, IReadOnlyList<T>> Bindings,
    BindingEditStatus Status,
    InputAction? Owner = null);

/// <summary>
/// Modifications d'une table « action → liaisons » (manette : <see cref="InputBinding"/> ; clavier : noms de touches),
/// avec les garde-fous de M11 : une entrée ne sert qu'à une action, et une action essentielle
/// (<see cref="InputDefaults.IsEssential"/>) garde au moins une liaison ; depuis l'étape 32 (D-130), deux liaisons au plus
/// par action. Les tables ne sont jamais modifiées en place.
/// </summary>
public static class BindingEdits
{
    /// <summary>Liaisons au plus par action (étape 32, demande de l'utilisateur). Une table qui en a plus reste lue telle quelle.</summary>
    public const int MaxPerAction = 2;

    /// <summary>Action à laquelle l'entrée est déjà associée, ou null.</summary>
    public static InputAction? OwnerOf<T>(IReadOnlyDictionary<InputAction, IReadOnlyList<T>> bindings, T binding)
    {
        foreach (var (action, list) in bindings)
        {
            if (list.Contains(binding))
                return action;
        }

        return null;
    }

    /// <summary>Ajoute une liaison à la fin de celles de l'action, s'il en a moins de <see cref="MaxPerAction"/>.</summary>
    public static BindingEdit<T> Add<T>(IReadOnlyDictionary<InputAction, IReadOnlyList<T>> bindings, InputAction action, T binding)
    {
        if (OwnerOf(bindings, binding) is { } owner)
            return new(bindings, owner == action ? BindingEditStatus.AlreadyBound : BindingEditStatus.UsedByOther, owner);

        var list = ListOf(bindings, action);
        if (list.Count >= MaxPerAction)
            return new(bindings, BindingEditStatus.Full, action);

        return new(With(bindings, action, [.. list, binding]), BindingEditStatus.Done);
    }

    /// <summary>Remplace la liaison n° <paramref name="index"/> de l'action (même place dans la liste).</summary>
    public static BindingEdit<T> Replace<T>(IReadOnlyDictionary<InputAction, IReadOnlyList<T>> bindings, InputAction action, int index, T binding)
    {
        var list = ListOf(bindings, action);
        if (index < 0 || index >= list.Count)
            return Add(bindings, action, binding);
        if (EqualityComparer<T>.Default.Equals(list[index], binding))
            return new(bindings, BindingEditStatus.Done);
        if (OwnerOf(bindings, binding) is { } owner)
            return new(bindings, owner == action ? BindingEditStatus.AlreadyBound : BindingEditStatus.UsedByOther, owner);

        var replaced = list.ToList();
        replaced[index] = binding;
        return new(With(bindings, action, replaced), BindingEditStatus.Done);
    }

    /// <summary>Retire la liaison n° <paramref name="index"/> de l'action, sauf si c'était la dernière d'une action essentielle.</summary>
    public static BindingEdit<T> Remove<T>(IReadOnlyDictionary<InputAction, IReadOnlyList<T>> bindings, InputAction action, int index)
    {
        var list = ListOf(bindings, action);
        if (index < 0 || index >= list.Count)
            return new(bindings, BindingEditStatus.Done);
        if (list.Count == 1 && InputDefaults.IsEssential(action))
            return new(bindings, BindingEditStatus.LastEssential, action);

        return new(With(bindings, action, [.. list.Where((_, i) => i != index)]), BindingEditStatus.Done);
    }

    /// <summary>
    /// Résultat de l'assistant (étape Mapping) : l'entrée capturée devient la première liaison de chaque action, à la
    /// place de l'ancienne première ; les autres liaisons restent (le stick à côté de la croix, par exemple), sauf celles
    /// que l'assistant vient de donner à une action.
    /// </summary>
    public static IReadOnlyDictionary<InputAction, IReadOnlyList<T>> WithPrimaries<T>(
        IReadOnlyDictionary<InputAction, IReadOnlyList<T>> bindings,
        IReadOnlyDictionary<InputAction, T> primaries)
    {
        var taken = primaries.Values.ToHashSet();
        var result = new Dictionary<InputAction, IReadOnlyList<T>>();
        foreach (var action in bindings.Keys.Union(primaries.Keys))
        {
            var list = ListOf(bindings, action);
            result[action] = primaries.TryGetValue(action, out var primary)
                ? [primary, .. list.Skip(1).Where(b => !taken.Contains(b))]
                : [.. list.Where(b => !taken.Contains(b))];
        }

        return result;
    }

    private static IReadOnlyList<T> ListOf<T>(IReadOnlyDictionary<InputAction, IReadOnlyList<T>> bindings, InputAction action) =>
        bindings.TryGetValue(action, out var list) ? list : [];

    private static Dictionary<InputAction, IReadOnlyList<T>> With<T>(
        IReadOnlyDictionary<InputAction, IReadOnlyList<T>> bindings, InputAction action, IReadOnlyList<T> list) =>
        new(bindings) { [action] = list };
}
