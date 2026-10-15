namespace WinSU.Core.Input;

/// <summary>
/// Transforme des sources brutes (bouton, sens d'un axe, direction d'une croix) en appuis d'actions,
/// avec répétition automatique des directions et des onglets maintenus (depuis M14 : la teinte du sélecteur de couleur
/// tourne tant qu'on garde Onglet précédent ou suivant ; les écrans qui ne veulent pas de répétition l'ignorent). Une action reste enfoncée tant qu'au moins une
/// de ses sources l'est : la croix et le stick peuvent ainsi piloter la même direction sans doublon.
/// </summary>
/// <remarks>Non thread-safe : à utiliser depuis un seul thread (celui des entrées).</remarks>
public sealed class ActionRepeater
{
    private readonly Dictionary<InputAction, HashSet<string>> _activeSources = [];
    private readonly Dictionary<InputAction, long> _nextRepeatAt = [];

    public int RepeatDelayMs { get; set; } = 400;
    public int RepeatIntervalMs { get; set; } = 80;

    public static bool IsDirection(InputAction action) =>
        action is InputAction.Up or InputAction.Down or InputAction.Left or InputAction.Right;

    /// <summary>Actions répétées quand on les maintient : les directions et les onglets.</summary>
    public static bool IsRepeatable(InputAction action) =>
        IsDirection(action) || action is InputAction.PagePrevious or InputAction.PageNext;

    /// <summary>Met à jour l'état d'une source et ajoute à <paramref name="output"/> l'appui éventuel.</summary>
    public void Update(string source, InputAction action, bool active, long nowMs, ICollection<(InputAction Action, bool IsRepeat)> output)
    {
        if (active)
        {
            if (!_activeSources.TryGetValue(action, out var sources))
                _activeSources[action] = sources = [];

            var wasPressed = sources.Count > 0;
            if (sources.Add(source) && !wasPressed)
            {
                output.Add((action, false));
                if (IsRepeatable(action))
                    _nextRepeatAt[action] = nowMs + RepeatDelayMs;
            }
        }
        else if (_activeSources.TryGetValue(action, out var sources) && sources.Remove(source) && sources.Count == 0)
        {
            _nextRepeatAt.Remove(action);
        }
    }

    /// <summary>Émet les répétitions dues. À appeler régulièrement (plusieurs fois par intervalle).</summary>
    public void Tick(long nowMs, ICollection<(InputAction Action, bool IsRepeat)> output)
    {
        foreach (var (action, dueAt) in _nextRepeatAt.ToArray())
        {
            if (nowMs < dueAt)
                continue;

            output.Add((action, true));
            var next = dueAt + RepeatIntervalMs;
            _nextRepeatAt[action] = next > nowMs ? next : nowMs + RepeatIntervalMs; // pas de rafale de rattrapage
        }
    }

    /// <summary>Oublie tout (manette débranchée, capture, changement de profil) sans émettre d'événement.</summary>
    public void ReleaseAll()
    {
        _activeSources.Clear();
        _nextRepeatAt.Clear();
    }

    /// <summary>Relâche toutes les sources dont la clé commence par <paramref name="prefix"/> (ex. une manette).</summary>
    public void ReleaseSources(string prefix)
    {
        foreach (var (action, sources) in _activeSources)
        {
            sources.RemoveWhere(s => s.StartsWith(prefix, StringComparison.Ordinal));
            if (sources.Count == 0)
                _nextRepeatAt.Remove(action);
        }
    }
}
