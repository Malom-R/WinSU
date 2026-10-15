using CommunityToolkit.Mvvm.ComponentModel;
using WinSU.Core.Audio;

namespace WinSU.App.ViewModels;

/// <summary>Base des écrans. Les ViewModels sont communs à tous les styles d'interface.</summary>
public abstract class ViewModelBase : ObservableObject
{
    /// <summary>Son joué quand on ouvre cet écran (null : aucun).</summary>
    public virtual UiSound? EnterSound => UiSound.Open;

    /// <summary>Son joué quand on quitte cet écran par Retour (null : aucun).</summary>
    public virtual UiSound? ExitSound => UiSound.Close;

    /// <summary>
    /// Nom de l'écran (« Jeux », « Super Nintendo »…), pour un style qui l'affiche (barre du bas du style Switch) ;
    /// null : aucun (l'accueil). Les écrans dont le nom change le signalent par PropertyChanged.
    /// </summary>
    public virtual string? PageTitle => null;

    /// <summary>
    /// Retour ne fait rien sur cet écran : option posée par la vue d'un style où l'on passe d'un écran à l'autre par des
    /// icônes (D-68), qui offre alors un bouton pour revenir. Les fenêtres superposées gardent Retour.
    /// </summary>
    public bool IgnoresBack { get; set; }

    /// <summary>Appelé quand l'écran devient visible (premier affichage ou retour arrière).</summary>
    public virtual void OnNavigatedTo()
    {
    }

    /// <summary>Appelé quand un autre écran le remplace.</summary>
    public virtual void OnNavigatedFrom()
    {
    }
}
