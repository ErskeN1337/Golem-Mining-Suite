using System;
using System.Globalization;
using System.Windows.Data;

namespace Golem_Mining_Suite.Utilities
{
    /// <summary>
    /// Maps a nullable risk score (0..100) into a short bucket string the Route
    /// Optimizer grid uses to pick a badge colour:
    /// <list type="bullet">
    ///   <item><description><c>"off"</c> — no score (null)</description></item>
    ///   <item><description><c>"low"</c> — &lt; 30</description></item>
    ///   <item><description><c>"med"</c> — 30..60</description></item>
    ///   <item><description><c>"high"</c> — 60..80</description></item>
    ///   <item><description><c>"crit"</c> — &gt; 80</description></item>
    /// </list>
    /// Keeping the buckets in a converter (rather than repeated DataTriggers with
    /// comparison parameters) centralises the thresholds so future tuning stays
    /// in one file.
    /// </summary>
    public sealed class RiskScoreBucketConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not double d) return "off";
            if (double.IsNaN(d)) return "off";

            if (d < 30.0) return "low";
            if (d < 60.0) return "med";
            if (d < 80.0) return "high";
            return "crit";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
