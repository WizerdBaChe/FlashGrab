using System.Drawing;
using FlashGrab.Capture;

int fail = 0, pass = 0;
void Check(string name, object got, object want)
{
    if (Equals(got, want)) { pass++; }
    else { fail++; Console.WriteLine($"FAIL {name}: got {got}, want {want}"); }
}

const double dz = 50;
// 8 directions, 100 px out (screen y down)
var dirs = new (string, double, double, int)[]
{
    ("N", 0, -100, 0), ("NE", 70, -70, 1), ("E", 100, 0, 2), ("SE", 70, 70, 3),
    ("S", 0, 100, 4), ("SW", -70, 70, 5), ("W", -100, 0, 6), ("NW", -70, -70, 7),
};
foreach (var (n, dx, dy, want) in dirs) Check("dir " + n, RadialWheel.SectorAt(dx, dy, dz), want);

// dead zone (negative controls)
Check("dead centre", RadialWheel.SectorAt(0, 0, dz), -1);
Check("dead edge", RadialWheel.SectorAt(49, 0, dz), -1);
Check("just outside dead", RadialWheel.SectorAt(51, 0, dz), 2);
// direction only: far beyond the ring still selects
Check("far E", RadialWheel.SectorAt(5000, 10, dz), 2);
Check("far NW", RadialWheel.SectorAt(-4000, -4000, dz), 7);
// boundaries: ±22.5° around N
double r = 100, a1 = 22.4 * Math.PI / 180, a2 = 22.6 * Math.PI / 180;
Check("N at +22.4", RadialWheel.SectorAt(Math.Sin(a1) * r, -Math.Cos(a1) * r, dz), 0);
Check("NE at +22.6", RadialWheel.SectorAt(Math.Sin(a2) * r, -Math.Cos(a2) * r, dz), 1);
Check("N at -22.4", RadialWheel.SectorAt(-Math.Sin(a1) * r, -Math.Cos(a1) * r, dz), 0);
Check("NW at -22.6", RadialWheel.SectorAt(-Math.Sin(a2) * r, -Math.Cos(a2) * r, dz), 7);

// placement: keep ring inside screen
var screen = new Rectangle(0, 0, 1920, 1080);
Check("place middle", RadialWheel.Place(new Point(900, 500), screen, 170), new Point(900, 500));
Check("place top-left", RadialWheel.Place(new Point(5, 5), screen, 170), new Point(186, 186));
Check("place bottom-right", RadialWheel.Place(new Point(1919, 1079), screen, 170), new Point(1734, 894));
var second = new Rectangle(1920, 0, 1280, 1024); // second monitor, offset
Check("place 2nd left edge", RadialWheel.Place(new Point(1925, 500), second, 170), new Point(2106, 500));
Check("place tiny screen", RadialWheel.Place(new Point(10, 10), new Rectangle(0, 0, 300, 300), 170), new Point(150, 150));

// ---- library + loadout ----
var def = RadialWheel.ResolveLoadout(null, out var d0);
Check("default 8 slots", def.Length, 8);
Check("default nothing dropped", d0.Count, 0);
// accepted positions preserved: N copy text (group default), NE AI, E one line, SE copy image, S PNG (group default), SW keep lines
Check("N default action", def[0]!.DefaultAction, WheelAction.CopyText);
Check("N is group", def[0]!.IsGroup, true);
Check("NE AI", def[1]!.DefaultAction, WheelAction.AiText);
Check("E one line", def[2]!.DefaultAction, WheelAction.CopyOneLine);
Check("SE copy image", def[3]!.DefaultAction, WheelAction.CopyImage);
Check("S default PNG", def[4]!.DefaultAction, WheelAction.SaveImage);
Check("SW keep lines", def[5]!.DefaultAction, WheelAction.CopyKeepLines);
Check("W empty", def[6] is null, true);
Check("NW empty", def[7] is null, true);
var custom = RadialWheel.ResolveLoadout(new string?[] { "image.jpg", null, "gone.item", "ai.text", null, null, null, "group.text" }, out var d1);
Check("custom slot0 jpg", custom[0]!.DefaultAction, WheelAction.SaveJpg);
Check("stale id -> empty", custom[2] is null, true);
Check("stale id reported", string.Join(",", d1), "gone.item");
Check("wrong length -> default", RadialWheel.ResolveLoadout(new string?[] { "ai.text" }, out _)[1]!.DefaultAction, WheelAction.AiText);
Check("library ids unique", RadialWheel.Library.Select(i => i.Id).Distinct().Count(), RadialWheel.Library.Count);
Check("every group <= 8 children", RadialWheel.Library.Where(i => i.IsGroup).All(g => g.Children!.Count <= 8), true);

// ---- expansion threshold ----
Check("expand at 1.31x on slot", RadialWheel.ShouldExpand(0, -131, 100, 0), true);
Check("no expand at 1.29x", RadialWheel.ShouldExpand(0, -129, 100, 0), false);
Check("no expand wrong slot", RadialWheel.ShouldExpand(131, 0, 100, 0), false);

// ---- session: two levels ----
string? NoAi(WheelAction a) => a == WheelAction.AiText ? "AI off" : null;
var scr = new Rectangle(0, 0, 1920, 1080);
WheelSession Fresh() { var s = new WheelSession(def, NoAi); s.Open(new Point(900, 500), scr, 170); return s; }
var c0 = new Point(900, 500);
Point At(Point c, double ang, double dist) => new((int)Math.Round(c.X + Math.Sin(ang * Math.PI / 180) * dist), (int)Math.Round(c.Y - Math.Cos(ang * Math.PI / 180) * dist));

var s1 = Fresh();
Check("centre release = Default", s1.Release(c0).Outcome, WheelOutcome.Default);
s1 = Fresh();
var r1 = s1.Release(At(c0, 0, 150));
Check("unexpanded group = default child", (r1.Outcome, r1.Action), (WheelOutcome.Commit, (WheelAction?)WheelAction.CopyText));
s1 = Fresh();
var rFar = s1.Release(At(c0, 90, 5000));
Check("leaf far out still selects (no outer limit)", (rFar.Outcome, rFar.Action, s1.Level), (WheelOutcome.Commit, (WheelAction?)WheelAction.CopyOneLine, 1));
s1 = Fresh();
s1.Move(At(c0, 0, 170 * 1.29));
Check("1.29x on group stays level 1", s1.Level, 1);
s1.Move(At(c0, 0, 170 * 1.31));
Check("1.31x on group -> level 2", s1.Level, 2);
Check("sub centred at crossing", s1.SubCenter, At(c0, 0, 170 * 1.31));
Check("sub radius", s1.SubRadius, 136);
var sc = s1.SubCenter;
var rSub = s1.Release(At(sc, 90, 120)); // children fill slots 0.. in order: N copy, NE one line, E keep lines
Check("sub slot E = 3rd child", (rSub.Outcome, rSub.Action), (WheelOutcome.Commit, (WheelAction?)WheelAction.CopyKeepLines));
s1 = Fresh(); s1.Move(At(c0, 0, 230)); sc = s1.SubCenter;
Check("sub slot NE = 2nd child", s1.Release(At(sc, 45, 120)).Action, (WheelAction?)WheelAction.CopyOneLine);
s1 = Fresh(); s1.Move(At(c0, 0, 230)); sc = s1.SubCenter;
var rSubDead = s1.Release(sc);
Check("sub dead zone = group default", rSubDead.Action, (WheelAction?)WheelAction.CopyText);
s1 = Fresh(); s1.Move(At(c0, 0, 230)); sc = s1.SubCenter;
Check("sub empty slot stays", s1.Release(At(sc, 180, 120)).Outcome, WheelOutcome.Stay);
s1 = Fresh(); s1.Move(At(c0, 0, 230));
Check("Back from level 2", (s1.Back(), s1.Level), (true, 1));
Check("Back at level 1 is false", s1.Back(), false);
s1 = Fresh();
var rAi = s1.Release(At(c0, 45, 150));
Check("unavailable AI stays + reason", (rAi.Outcome, s1.Status), (WheelOutcome.Stay, "AI off"));
s1 = Fresh();
Check("empty level-1 slot stays", s1.Release(At(c0, 270, 150)).Outcome, WheelOutcome.Stay);
s1 = Fresh();
s1.Move(At(c0, 90, 400)); // leaf E far out: must NOT expand
Check("leaf never expands", s1.Level, 1);
// image group (S): expand, pick N child = PNG? children: png, copy, jpg -> slot 2 (E) = jpg
s1 = Fresh(); s1.Move(At(c0, 180, 230)); sc = s1.SubCenter;
Check("image group E child = JPG", s1.Release(At(sc, 90, 100)).Action, (WheelAction?)WheelAction.SaveJpg);
// expansion near screen edge: sub wheel clamped inside
var edge = new WheelSession(def, NoAi); edge.Open(new Point(10, 10), scr, 170);
edge.Move(new Point(edge.Center.X, edge.Center.Y - 230));
Check("sub clamped inside screen", edge.SubCenter.Y >= edge.SubRadius + 16, true);
bool threw = false; try { new WheelSession(new WheelItem?[3], NoAi); } catch (ArgumentException) { threw = true; }
Check("session rejects non-8 slots", threw, true);

// JoinLines
Check("join cjk", RadialWheel.JoinLines("今天天氣\r\n很好"), "今天天氣很好");
Check("join latin", RadialWheel.JoinLines("hello\nworld"), "hello world");
Check("join mixed", RadialWheel.JoinLines("按 Win\n鍵"), "按 Win 鍵");
Check("join blank lines", RadialWheel.JoinLines("a\n\n  \nb"), "a b");

if (args.Contains("control")) Check("control: E must not be N", RadialWheel.SectorAt(100, 0, dz), 0);
Console.WriteLine($"{pass} pass, {fail} fail");
return fail == 0 ? 0 : 1;
