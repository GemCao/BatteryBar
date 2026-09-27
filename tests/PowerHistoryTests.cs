using System;
using BatteryBar;
class PowerHistoryTests {
    static Reading R(int rate) { return new Reading { Available = true, Present = true, Rate = rate, Charging = rate > 0, Discharging = rate < 0 }; }
    static void Eq(double a, double b, string label) { if (Math.Abs(a-b) > .00001) throw new Exception(label + ": " + a + " != " + b); }
    static void Main() {
        var h = new PowerHistory(); h.Add(0,R(-10000),65); Eq(h.Average(0,1).DischargingSeconds,0,"startup");
        h.Add(10,R(-20000),65); h.Add(30,R(-40000),65);
        var a=h.Average(30,1); Eq(a.DischargingSeconds,30,"coverage"); Eq(a.DischargingWattSeconds/30,50.0/3,"time weighted");
        h.Add(60,R(-10000),65); a=h.Average(70,1); Eq(a.DischargingSeconds,50,"window clipping"); Eq(a.DischargingWattSeconds,1600,"clipped integral");
        h.Add(70,R(30000),65); h.Add(90,R(30000),65); a=h.Average(90,5);
        Eq(a.ChargingSeconds,20,"charging coverage"); Eq(a.ChargingWattSeconds,600,"charging separate"); Eq(a.DischargingSeconds,60,"switch interval excluded");
        h.Add(100,R(-1),65); h.Add(110,R(30000),65); Eq(h.Average(110,5).ChargingSeconds,20,"unknown excluded");
        h.Break(); h.Add(200,R(30000),65); Eq(h.Average(200,5).ChargingSeconds,20,"sleep excluded");
        h.Add(300,R(30000),65); Eq(h.Average(300,5).ChargingSeconds,20,"long gap excluded");
        h.Add(2100,R(10000),65); Eq(h.Average(2100,30).ChargingSeconds,0,"history expires");
        var windows = new PowerHistory(); for(int t=0;t<=1800;t+=30) windows.Add(t,R(-12000),65);
        foreach(int m in new int[]{1,5,10,30}) { a=windows.Average(1800,m); Eq(a.DischargingSeconds,m*60,"full window"); Eq(a.DischargingWattSeconds/a.DischargingSeconds,12,"constant mean"); }
        Console.WriteLine("PASS: weighted averages, 4 windows, clipping, startup, direction changes, unknown data, suspend, delayed reads, history expiry.");
    }
}
