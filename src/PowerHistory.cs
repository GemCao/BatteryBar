using System;
using System.Collections.Generic;

namespace BatteryBar {
public class PowerAverage {
    public double ChargingSeconds, DischargingSeconds, ChargingWattSeconds, DischargingWattSeconds;
    public string Charge { get { return Format(ChargingWattSeconds, ChargingSeconds); } }
    public string Discharge { get { return Format(DischargingWattSeconds, DischargingSeconds); } }
    static string Format(double energy, double seconds) {
        return seconds <= 0 ? "—" : (energy / seconds).ToString("0.00") + " W / " + (seconds < 60 ? Math.Floor(seconds) + "秒" : (seconds / 60).ToString("0.0") + "分");
    }
}
// Monotonic seconds, piecewise constant rate, separate denominators for each direction.
public class PowerHistory {
    class Span { public double Start, End, Watts; public bool Charging; }
    readonly List<Span> spans = new List<Span>();
    double previousTime, previousWatts; int previousDirection; bool hasPrevious;
    public static int Direction(Reading r) {
        if (!r.Available || !r.Present || r.Rate == -1 || r.Rate == int.MinValue || r.Charging == r.Discharging) return 0;
        if (r.Charging && r.Rate >= 0) return 1;
        if (r.Discharging && r.Rate <= 0) return -1;
        return 0;
    }
    public void Break() { hasPrevious = false; }
    public void Add(double now, Reading r, double maxGap) {
        int direction = Direction(r);
        if (hasPrevious && now > previousTime && now - previousTime <= maxGap && direction != 0 && direction == previousDirection)
            spans.Add(new Span { Start = previousTime, End = now, Watts = previousWatts, Charging = direction == 1 });
        previousTime = now; previousWatts = Math.Abs((double)r.Rate) / 1000; previousDirection = direction; hasPrevious = true;
        spans.RemoveAll(s => s.End <= now - 1800);
    }
    public PowerAverage Average(double now, int minutes) {
        var result = new PowerAverage();
        foreach (var s in spans) {
            double duration = Math.Max(0, Math.Min(now, s.End) - Math.Max(now - minutes * 60, s.Start));
            if (s.Charging) { result.ChargingSeconds += duration; result.ChargingWattSeconds += duration * s.Watts; }
            else { result.DischargingSeconds += duration; result.DischargingWattSeconds += duration * s.Watts; }
        }
        return result;
    }
}
}
