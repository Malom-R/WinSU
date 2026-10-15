namespace WinSU.App.Controls;

/// <summary>
/// Mouvement des gouttes de la lampe à lave (M10) : chaque goutte monte et redescend lentement (sinusoïde verticale de
/// 55 à 110 s, qui s'attarde en haut et en bas comme la cire d'une vraie lampe), dérive un peu à l'horizontale et
/// respire (rayon qui varie de quelques pour cent). Les trajectoires se croisent : les gouttes fusionnent puis se séparent.
/// <para>
/// Unités : la hauteur de la zone vaut 1, sa largeur vaut <c>aspect</c> (largeur / hauteur), pour que les gouttes
/// restent rondes. Les gouttes sortent un peu par le haut et par le bas. Même instant, même image : le mouvement ne
/// dépend que du temps écoulé (arrêté pendant les pauses).
/// </para>
/// </summary>
public static class LavaField
{
    /// <summary>Nombre de gouttes (le shader en attend autant).</summary>
    public const int BlobCount = 7;

    /// <summary>Valeurs par goutte dans le tableau rempli par <see cref="Compute"/> : x, y, rayon, indice de couleur.</summary>
    public const int Stride = 4;

    private readonly record struct Blob(
        float X, float DriftX, float DriftPeriod, float DriftPhase,
        float Rise, float RisePeriod, float RisePhase,
        float Radius, float BreathPeriod, int Color);

    // X et DriftX en fraction de la largeur ; Rise (demi-course verticale autour du milieu) et Radius en fraction de la hauteur.
    // Périodes en secondes (nombres premiers), pour que la figure ne se répète pas avant longtemps.
    private static readonly Blob[] Blobs =
    [
        new(0.10f, 0.05f, 71f, 0.0f, 0.62f, 59f, 0.3f, 0.17f, 23f, 0),
        new(0.29f, 0.07f, 89f, 1.7f, 0.56f, 83f, 2.1f, 0.13f, 29f, 1),
        new(0.50f, 0.06f, 67f, 3.1f, 0.66f, 67f, 4.4f, 0.19f, 31f, 2),
        new(0.69f, 0.08f, 97f, 0.9f, 0.58f, 101f, 1.2f, 0.15f, 19f, 3),
        new(0.88f, 0.05f, 79f, 4.2f, 0.63f, 61f, 5.5f, 0.18f, 37f, 1),
        new(0.40f, 0.11f, 113f, 2.6f, 0.50f, 107f, 3.6f, 0.11f, 17f, 0),
        new(0.78f, 0.09f, 103f, 5.1f, 0.52f, 73f, 0.8f, 0.12f, 41f, 2),
    ];

    /// <summary>
    /// Position des gouttes à l'instant <paramref name="seconds"/>, dans <paramref name="output"/>
    /// (<see cref="BlobCount"/> × <see cref="Stride"/> valeurs : x, y, rayon, indice de couleur de 0 à 3).
    /// </summary>
    public static void Compute(double seconds, float aspect, Span<float> output)
    {
        if (output.Length < BlobCount * Stride)
            throw new ArgumentException($"Il faut {BlobCount * Stride} valeurs.", nameof(output));

        for (var i = 0; i < Blobs.Length; i++)
        {
            var blob = Blobs[i];
            var x = blob.X + blob.DriftX * Wave(seconds, blob.DriftPeriod, blob.DriftPhase);
            var y = 0.5f + blob.Rise * Wave(seconds, blob.RisePeriod, blob.RisePhase);
            var radius = blob.Radius * (1f + 0.06f * Wave(seconds, blob.BreathPeriod, blob.RisePhase));

            var slot = output.Slice(i * Stride, Stride);
            slot[0] = x * aspect;
            slot[1] = y;
            slot[2] = radius;
            slot[3] = blob.Color;
        }
    }

    private static float Wave(double seconds, float period, float phase) =>
        (float)Math.Sin(seconds * (2 * Math.PI / period) + phase);
}
