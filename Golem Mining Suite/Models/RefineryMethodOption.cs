using System.Windows.Media;

namespace Golem_Mining_Suite.Models
{
    /// <summary>
    /// Presentation wrapper around <see cref="RefineryMethod"/> for the Refinery
    /// Calculator's method dropdown. Surfaces yield / cost / speed inline so the
    /// player can compare options at a glance without opening each one.
    /// </summary>
    public sealed class RefineryMethodOption
    {
        public required RefineryMethod Method { get; init; }

        public string Name => Method.Name;

        /// <summary>e.g. <c>"+70%"</c> — positive deltas read as gain.</summary>
        public string YieldText => $"+{Method.YieldBonus:F0}%";

        /// <summary>e.g. <c>"7%"</c> — fee charged on raw value.</summary>
        public string CostText => $"{Method.CostPercent:F0}%";

        /// <summary>e.g. <c>"★★☆"</c> for SpeedRating=2. Always 3 glyphs wide.</summary>
        public string SpeedText => Stars(Method.SpeedRating);

        /// <summary>Green for yield (gain).</summary>
        public Brush YieldBrush { get; } = new SolidColorBrush(Color.FromRgb(0x67, 0xA9, 0x4F));

        /// <summary>Red for cost (loss).</summary>
        public Brush CostBrush { get; } = new SolidColorBrush(Color.FromRgb(0xC1, 0x49, 0x49));

        /// <summary>Blue for speed (neutral indicator).</summary>
        public Brush SpeedBrush { get; } = new SolidColorBrush(Color.FromRgb(0x3F, 0xA8, 0xD1));

        public override string ToString() => Name; // Used by ComboBox text-search

        private static string Stars(int rating)
        {
            var clamped = rating < 0 ? 0 : (rating > 3 ? 3 : rating);
            return new string('★', clamped) + new string('☆', 3 - clamped);
        }
    }
}
