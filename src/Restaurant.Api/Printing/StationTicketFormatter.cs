using Restaurant.Domain.Catalog;

namespace Restaurant.Api.Printing;

public record TicketLine(string ProductName, int Quantity);

public record StationTicket(Station Station, string Content);

/// <summary>
/// Pure text formatting — deliberately has no dependency on an actual printer/device.
/// Once a thermal printer is confirmed available, the output of
/// <see cref="Format"/> is what gets sent to an ESC/POS adapter; this class stays
/// unchanged, only the adapter is new (see implementation-notes.md).
/// </summary>
public static class StationTicketFormatter
{
    public static List<StationTicket> Format(Guid orderId, string? tableNumber, IEnumerable<(Station Station, string ProductName, int Quantity)> items)
    {
        return items
            .GroupBy(i => i.Station)
            .Select(group => new StationTicket(group.Key, FormatTicket(orderId, tableNumber, group.Key, group)))
            .ToList();
    }

    private static string FormatTicket(Guid orderId, string? tableNumber, Station station, IEnumerable<(Station Station, string ProductName, int Quantity)> items)
    {
        var lines = new List<string>
        {
            $"=== {station.ToString().ToUpperInvariant()} TICKET ===",
            $"Order: {orderId.ToString()[..8]}",
            $"Table: {tableNumber ?? "Takeaway"}",
            $"Time: {DateTimeOffset.Now:HH:mm:ss}",
            new string('-', 24)
        };

        lines.AddRange(items.Select(i => $"{i.Quantity}x {i.ProductName}"));
        lines.Add(new string('-', 24));

        return string.Join('\n', lines);
    }
}
