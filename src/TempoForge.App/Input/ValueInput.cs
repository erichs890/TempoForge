using System.Globalization;

namespace TempoForge.App.Input;

/// <summary>Interpreta valores digitados nos mostradores (velocidade e equalizador).</summary>
public static class ValueInput
{
    /// <summary>
    /// Lê um número com vírgula ou ponto decimal, ignorando unidade e sinais tipográficos
    /// ("1,25×", "+2.37 dB", "−3"). Devolve falso se não houver um número válido.
    /// </summary>
    public static bool TryParseNumber(string? text, out double value, out bool isPercent)
    {
        value = 0;
        isPercent = false;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string t = text.Trim().ToLowerInvariant().Replace('−', '-').Replace(',', '.');
        isPercent = t.Contains('%');
        foreach (string unit in new[] { "db", "×", "x", "%", " " }) t = t.Replace(unit, "");
        return double.TryParse(t, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
               && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    /// <summary>Velocidade: "0,75", "1.25x" ou "75%" (percentual do andamento original).</summary>
    public static bool TryParseSpeed(string? text, double min, double max, out double speed)
    {
        speed = 0;
        if (!TryParseNumber(text, out double value, out bool isPercent)) return false;
        if (isPercent) value /= 100;
        if (value <= 0) return false;
        speed = Math.Round(Math.Clamp(value, min, max), 2);
        return true;
    }

    /// <summary>Ganho em dB, limitado à faixa do controle e arredondado a duas casas.</summary>
    public static bool TryParseDecibels(string? text, double min, double max, out double db)
    {
        db = 0;
        if (!TryParseNumber(text, out double value, out _)) return false;
        db = Math.Round(Math.Clamp(value, min, max), 2);
        return true;
    }
}
