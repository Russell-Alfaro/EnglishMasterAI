namespace EnglishMasterAI.Domain.Entities;

/// <summary>
/// Regla de negocio: precio de inscripción según el nivel CEFR inicial.
/// Se calcula siempre en el servidor (nunca se confía en un monto enviado
/// por el cliente) para evitar manipulación del precio.
/// </summary>
public static class LevelPricing
{
    private static readonly Dictionary<string, decimal> Prices = new()
    {
        { "A1", 100m },
        { "A2", 120m },
        { "B1", 140m },
        { "B2", 160m },
        { "C1", 180m },
        { "C2", 200m }
    };

    public static decimal GetPrice(string level)
    {
        var key = (level ?? string.Empty).Trim().ToUpperInvariant();
        if (!Prices.TryGetValue(key, out var price))
            throw new ArgumentException($"Nivel inválido: '{level}'. Los niveles válidos son A1, A2, B1, B2, C1, C2.");

        return price;
    }
}