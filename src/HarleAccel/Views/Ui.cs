// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace HarleAccel.Views
{
    /// <summary>Pequenos ajudantes de interface usados pelas telas.</summary>
    public static class Ui
    {
        public static bool TryNum(string? text, out double v)
        {
            v = 0;
            if (string.IsNullOrWhiteSpace(text)) return false;
            return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out v);
        }

        public static string N(double v, string fmt = "0.###") =>
            double.IsNaN(v) || double.IsInfinity(v) ? "—" : v.ToString(fmt, CultureInfo.InvariantCulture);

        public static Brush Res(string key) => (Brush)Application.Current.FindResource(key);

        public static TextBlock Text(string s, double size = 13, string brush = "Silver", FontWeight? weight = null)
            => new()
            {
                Text = s,
                FontSize = size,
                Foreground = Res(brush),
                FontWeight = weight ?? FontWeights.Normal,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
            };

        public static void SetStatus(TextBlock tb, string msg, bool ok)
        {
            tb.Text = msg;
            tb.Foreground = Res(ok ? "Ok" : "Warn");
        }
    }
}
