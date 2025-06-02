using System;
using System.Linq;

namespace QuantConnect.DataSource.MetaTraderNews;

public class CalenderItem
{
    public string? EventId { get; set; } // From tr attribute "data-event-id"
    public string? DayDateline { get; set; } // From tr attribute "data-day-dateline" (Unix timestamp for the day)
    public string? Time { get; set; } // e.g., "12:01am", "All Day" [cite: 50, 56]
    public string? Currency { get; set; } // e.g., "GBP", "CNY" [cite: 50]
    public string? ImpactClass { get; set; } // e.g., "icon icon--ff-impact-yel" [cite: 50]
    public string? EventName { get; set; } // e.g., "Rightmove HPI m/m" [cite: 50]
    public string? Actual { get; set; } // e.g., "0.6%" [cite: 50]
    public string? Forecast { get; set; } // e.g., "-0.12%" [cite: 50]
    public string? Previous { get; set; } // e.g., "1.4%" [cite: 50]

    // Optional helper property to convert DayDateline to DateTimeOffset
    public DateTimeOffset? EventDateOffset =>
        long.TryParse(DayDateline, out var seconds) ? DateTimeOffset.FromUnixTimeSeconds(seconds) : null;

    // Optional helper property to extract the specific impact type (e.g., "yel", "ora", "gra")
    public string? ParsedImpact
    {
        get
        {
            if (string.IsNullOrEmpty(ImpactClass))
                return null;
            var parts = ImpactClass.Split(' ');
            return parts.LastOrDefault(p => p.StartsWith("icon--ff-impact-"))?.Replace("icon--ff-impact-", "");
        }
    }
}
