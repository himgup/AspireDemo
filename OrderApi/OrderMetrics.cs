using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OrderApi;

public sealed class OrderMetrics
{
    public const string MeterName = "AspireDemo.OrderApi";

    private readonly Counter<long> ordersCreated;
    private readonly ObservableGauge<long> ordersPending;
    private readonly Counter<long> orderStatusChanges;
    private readonly Histogram<double> processingDuration;
    private readonly ConcurrentDictionary<int, long> processingStarted = new();
    private long pendingOrderCount;

    public OrderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(MeterName);

        ordersCreated = meter.CreateCounter<long>(
            "aspire.demo.orders.created",
            "{order}",
            "Number of orders created");
        ordersPending = meter.CreateObservableGauge(
            "aspire.demo.orders.pending",
            () => Interlocked.Read(ref pendingOrderCount),
            "{order}",
            "Current number of orders waiting for processing");
        orderStatusChanges = meter.CreateCounter<long>(
            "aspire.demo.orders.status.changes",
            "{change}",
            "Number of order status changes");
        processingDuration = meter.CreateHistogram<double>(
            "aspire.demo.order.processing.duration",
            "s",
            "Time from processing start to shipment");
    }

    public void RecordCreated()
    {
        ordersCreated.Add(1);
        Interlocked.Increment(ref pendingOrderCount);
    }

    public void RecordStatusChange(int orderId, string status)
    {
        orderStatusChanges.Add(1,
            new KeyValuePair<string, object?>("order.status", status));

        if (status.Equals("Processing", StringComparison.OrdinalIgnoreCase))
        {
            Interlocked.Decrement(ref pendingOrderCount);
            processingStarted.TryAdd(orderId, Stopwatch.GetTimestamp());
        }
        else if (status.Equals("Shipped", StringComparison.OrdinalIgnoreCase) &&
                 processingStarted.TryRemove(orderId, out var startedAt))
        {
            processingDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds,
                new KeyValuePair<string, object?>("order.outcome", "Success"));
        }
    }
}