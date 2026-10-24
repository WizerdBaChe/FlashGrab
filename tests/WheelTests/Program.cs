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

// ---- fan geometry (rung 0: example tests at the wedge boundaries) ----
Check("offsets 5", string.Join(",", RadialWheel.FanOffsets(5)), "0,36,-36,72,-72");
Check("offsets capped at 5", RadialWheel.FanOffsets(7).Length, 5);
double Rad(double deg) => deg * Math.PI / 180;
int Hit(double deg, int slot, int n) => RadialWheel.FanHit(Math.Sin(Rad(deg)) * 200, -Math.Cos(Rad(deg)) * 200, slot, n);
Check("fan straight = child 0", Hit(0, 0, 5), 0);
Check("fan +17.9 = child 0", Hit(17.9, 0, 5), 0);
Check("fan +18.1 = child 1 (clockwise)", Hit(18.1, 0, 5), 1);
Check("fan -18.1 = child 2 (counter-clockwise)", Hit(-18.1, 0, 5), 2);
Check("fan +53.9 = child 1", Hit(53.9, 0, 5), 1);
Check("fan +54.1 = child 3", Hit(54.1, 0, 5), 3);
Check("fan -54.1 = child 4", Hit(-54.1, 0, 5), 4);
Check("fan +90.1 outside span", Hit(90.1, 0, 5), -1);
Check("3 children: +54.1 outside span", Hit(54.1, 0, 3), -1);
Check("wrap across N: NW group, N pointer = child 1", Hit(0, 7, 5), 1);

// ---- session: fan open / collapse ----
string? NoAi(WheelAction a) => a == WheelAction.AiText ? "AI off" : null;
var scr = new Rectangle(0, 0, 1920, 1080);
WheelSession Fresh() { var s = new WheelSession(def, NoAi); s.Open(new Point(900, 500), scr, 170); return s; }
var c0 = new Point(900, 500);
Point At(Point c, double ang, double dist) => new((int)Math.Round(c.X + Math.Sin(Rad(ang)) * dist), (int)Math.Round(c.Y - Math.Cos(Rad(ang)) * dist));
(WheelOutcome, WheelAction?) Pick(WheelRelease r) => (r.Outcome, r.Action);
var commit = (WheelAction a) => (WheelOutcome.Commit, (WheelAction?)a);

var s1 = Fresh();
Check("centre release = Default", s1.Release(c0).Outcome, WheelOutcome.Default);
s1 = Fresh();
Check("group below ring = default child", Pick(s1.Release(At(c0, 0, 150))), commit(WheelAction.CopyText));
s1 = Fresh(); s1.Move(At(c0, 0, 170 * 0.99));
Check("0.99R on group: fan closed", s1.FanOpen, false);
s1.Move(At(c0, 0, 170));
Check("1.0R on group: fan open", (s1.FanOpen, s1.FanSlot, s1.FanHover), (true, 0, 0));
s1.Move(At(c0, 0, 170 * 0.95));
Check("0.95R after open: still open, no child hovered", (s1.FanOpen, s1.FanHover), (true, -1));
s1.Move(At(c0, 0, 170 * 0.89));
Check("0.89R: collapsed, back on group slot", (s1.FanOpen, s1.Hover), (false, 0));
s1.Move(At(c0, 0, 200));
Check("reopens after collapse", s1.FanOpen, true);

s1 = Fresh(); s1.Move(At(c0, 0, 200));
Check("fan child 0 straight out", Pick(s1.Release(At(c0, 0, 200))), commit(WheelAction.CopyText));
s1 = Fresh(); s1.Move(At(c0, 0, 200));
Check("fan child 1 at +36", Pick(s1.Release(At(c0, 36, 200))), commit(WheelAction.CopyOneLine));
s1 = Fresh(); s1.Move(At(c0, 0, 200));
Check("fan child 2 at -36", Pick(s1.Release(At(c0, -36, 200))), commit(WheelAction.CopyKeepLines));
s1 = Fresh(); s1.Move(At(c0, 0, 200));
Check("fan child far out (no outer limit)", Pick(s1.Release(At(c0, -36, 900))), commit(WheelAction.CopyKeepLines));
s1 = Fresh(); s1.Move(At(c0, 0, 200));
var rOut = s1.Release(At(c0, 90, 200));
Check("outside fan span = Stay, fan kept", (rOut.Outcome, s1.FanOpen), (WheelOutcome.Stay, true));
s1 = Fresh(); s1.Move(At(c0, 0, 200));
Check("open, release at 0.95R = group default", Pick(s1.Release(At(c0, 0, 170 * 0.95))), commit(WheelAction.CopyText));
s1 = Fresh(); s1.Move(At(c0, 180, 200));
Check("image fan +36 = copy image", Pick(s1.Release(At(c0, 216, 200))), commit(WheelAction.CopyImage));
s1 = Fresh(); s1.Move(At(c0, 180, 200));
Check("image fan -36 = JPG", Pick(s1.Release(At(c0, 144, 200))), commit(WheelAction.SaveJpg));

s1 = Fresh(); s1.Move(At(c0, 90, 170 * 5));
Check("leaf at 5R never opens a fan", s1.FanOpen, false);
Check("leaf at 5R still selects", Pick(s1.Release(At(c0, 90, 170 * 5))), commit(WheelAction.CopyOneLine));
s1 = Fresh();
var rAi = s1.Release(At(c0, 45, 150));
Check("unavailable AI stays + reason", (rAi.Outcome, s1.Status), (WheelOutcome.Stay, "AI off"));
s1 = Fresh();
Check("empty slot stays", s1.Release(At(c0, 270, 150)).Outcome, WheelOutcome.Stay);

// children cap
var leaf = (int i) => new WheelItem("t" + i, "T" + i, "", WheelAction.CopyText);
var big = new WheelItem("big", "Big", "", Children: Enumerable.Range(0, 7).Select(leaf).ToArray());
var bigSlots = new WheelItem?[] { big, null, null, null, null, null, null, null };
var sb = new WheelSession(bigSlots, NoAi); sb.Open(c0, scr, 170); sb.Move(At(c0, 0, 200));
Check("fan shows at most 5 children", sb.FanChildren.Count, 5);

// placement reach: group in loadout -> 1.6R keeps the fan band on screen
var corner = new WheelSession(def, NoAi); corner.Open(new Point(5, 5), scr, 170);
Check("reach 1.6R with group", corner.Center, new Point(288, 288));
Check("fan outer edge on screen", corner.Center.Y - 170 * RadialWheel.FanOuterRatio >= 0, true);
var leafOnly = new WheelItem?[] { def[1], def[2], null, null, null, null, null, null };
var lc = new WheelSession(leafOnly, NoAi); lc.Open(new Point(5, 5), scr, 170);
Check("reach 1R without group", lc.Center, new Point(186, 186));

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
