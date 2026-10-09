using MiniDebit;
using System.Globalization;

int count = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine($"OK {++count:00} — {description}");
}
var now = DateTimeOffset.Now;
var smooth = new RateSmoother();
smooth.Update(new(now, 100, 50, 1), 1, true);
Check(smooth.Down == 100 && smooth.Up == 50, "Le premier débit affiché ne part pas artificiellement de zéro");
smooth.Update(new(now, 110, 55, 1), 1, true);
Check(smooth.Down > 100 && smooth.Down < 110 && smooth.Up > 50 && smooth.Up < 55,
    "Le lissage léger atténue les petites variations des deux débits");
var fastRefresh = new RateSmoother(); fastRefresh.Update(new(now, 100, 50, 1), 1, true);
for (int i = 0; i < 4; i++) fastRefresh.Update(new(now, 110, 55, .25), 1, true);
Check(Math.Abs(fastRefresh.Down - smooth.Down) < .000001,
    "Le lissage suit le temps réel : quatre mesures de 250 ms équivalent à une seconde");
var strong = new RateSmoother(); strong.Update(new(now, 100, 50, 1), 3, true);
strong.Update(new(now, 110, 55, 1), 3, true);
Check(strong.Down < smooth.Down, "Le niveau fort stabilise davantage les petites transitions");
strong.Update(new(now, 1000, 500, 1), 3, true);
Check(strong.Down < 300 && strong.Up < 150, "Les sauts relatifs à faible débit ne court-circuitent plus le lissage fort");
var adaptive = new RateSmoother(); adaptive.Update(new(now, 2_000_000, 100_000, 1), 3, true);
adaptive.Update(new(now, 4_000_000, 200_000, 1), 3, true);
Check(adaptive.Down > 3_400_000 && adaptive.Up < 110_000,
    "Au-dessus de 1 Mo/s la réception réagit vite, sans accélérer un envoi faible");
adaptive.Update(new(now, 0, 200_000, 1), 3, true);
Check(adaptive.Down < 1_000_000, "La fin d’un gros transfert revient rapidement sous 1 Mo/s");
var highFast = new RateSmoother(); highFast.Update(new(now, 2_000_000, 100_000, 1), 3, true);
for (int i = 0; i < 4; i++) highFast.Update(new(now, 4_000_000, 200_000, .25), 3, true);
Check(Math.Abs(highFast.Down - (2_000_000 + (1 - Math.Exp(-1 / .75)) * 2_000_000)) < .000001,
    "La réactivité au-dessus du seuil reste indépendante de la fréquence des relevés");
strong.Update(new(now, 800, 400, 1), 0, true);
Check(strong.Down == 800 && strong.Up == 400, "Désactiver restitue immédiatement les mesures exactes");
strong.Update(new(now, 800, 400, 1), 1, false);
Check(strong.Down == 0 && strong.Up == 0, "Une interface indisponible remet les deux valeurs immédiatement à zéro");
strong.Update(new(now, 30, 10, 1), 1, true);
Check(strong.Down == 30 && strong.Up == 10, "La reconnexion reprend sans reliquat de l’ancienne interface");
var weightedAverage = TrafficAverage.InWindow([new(now.AddSeconds(-3), 100, 20, 1), new(now, 300, 60, 3)], now, 60);
Check(weightedAverage.Down == 250 && weightedAverage.Up == 50 && weightedAverage.Seconds == 4,
    "La moyenne pondère les débits par la durée réelle des mesures");
var clippedAverage = TrafficAverage.InWindow([new(now.AddSeconds(-2), 100, 50, 3), new(now, 400, 200, 2),
    new(now.AddSeconds(-10), 999999, 999999, 1)], now, 4);
Check(clippedAverage.Down == 250 && clippedAverage.Up == 125 && clippedAverage.Seconds == 4,
    "La moyenne exclut les anciennes mesures et limite l’intervalle à la période visible");
Check(TrafficAverage.InWindow([new(now.AddSeconds(-1), 0, 0), new(now, 100, 50, 1)], now, 300).Down == 100 &&
    TrafficAverage.InWindow([], now, 300).Seconds == 0,
    "Au démarrage, la moyenne utilise la durée observée sans ajouter de zéros fictifs");
Check(PublicIpv4.Parse("203.0.113.42\n") == "203.0.113.42" && PublicIpv4.Parse("2001:db8::1") is null &&
    PublicIpv4.Parse("192.168.1.2") is null && PublicIpv4.Parse("<html>Erreur</html>") is null,
    "La réponse externe doit contenir une IPv4, jamais une IP locale ou une page d’erreur");
int ipCalls = 0;
var ipAnswer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
using var ipStarted = new ManualResetEventSlim();
var ipTarget = new PublicIpv4Target("test", System.Net.IPAddress.Parse("192.168.1.2"), 1);
using (var resolver = new PublicIpv4((_, _) => { Interlocked.Increment(ref ipCalls); ipStarted.Set(); return ipAnswer.Task; }))
{
    resolver.Update(ipTarget, false);
    Check(ipCalls == 0 && resolver.Address is null, "L’IPv4 publique désactivée ne lance aucune requête");
    resolver.Update(ipTarget, true);
    Check(ipStarted.Wait(2000) && resolver.Address is null, "Une requête inachevée ne bloque pas le minuteur");
    ipAnswer.SetResult("203.0.113.42");
    Check(SpinWait.SpinUntil(() => { resolver.Update(ipTarget, true); return resolver.Address is not null; }, 2000),
        "Le résultat asynchrone devient disponible pour le bandeau");
    for (int i = 0; i < 100; i++) resolver.Update(ipTarget, true);
    Check(ipCalls == 1, "Le cache évite une requête Internet à chaque mesure");
    resolver.Update(ipTarget, false);
    Check(resolver.Address is null, "Désactiver masque immédiatement l’adresse mémorisée");
}
var oldAnswer = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
using var oldStarted = new ManualResetEventSlim();
using (var resolver = new PublicIpv4((_, _) => { oldStarted.Set(); return oldAnswer.Task; }))
{
    resolver.Update(ipTarget, true); oldStarted.Wait(2000);
    resolver.Update(null, true); oldAnswer.SetResult("203.0.113.42");
    resolver.Update(null, true);
    Check(resolver.Address is null, "Une réponse tardive d’une ancienne carte ne peut pas être affichée");
}
using var errorStarted = new ManualResetEventSlim();
int errorCalls = 0;
using (var resolver = new PublicIpv4((_, _) => { Interlocked.Increment(ref errorCalls); errorStarted.Set(); throw new System.Net.Http.HttpRequestException("test"); }))
{
    resolver.Update(ipTarget, true); errorStarted.Wait(2000);
    Thread.Sleep(20); resolver.Update(ipTarget, true);
    Check(resolver.Address is null && errorCalls == 1, "Une erreur Internet laisse les mesures fonctionner sans réessai immédiat");
}
var desktop = new System.Drawing.Rectangle(0, 0, 1920, 1040);
var slowDrag = new WindowDrag();
var dragged = new System.Drawing.Rectangle(0, 0, 340, 176);
slowDrag.Begin(new(80, 40), dragged);
for (int pixel = 1; pixel <= 20; pixel++)
{
    var incremental = new System.Drawing.Rectangle(dragged.Left + 1, dragged.Top + 1, dragged.Width, dragged.Height);
    dragged = WindowPlacement.Snap(slowDrag.BoundsAt(new(80 + pixel, 40 + pixel), incremental), desktop, 8);
}
Check(dragged.Location == new System.Drawing.Point(20, 20),
    "Un déplacement lent pixel par pixel détache la fenêtre du coin sans perdre les mouvements");
dragged = new(1580, 864, 340, 176);
slowDrag.Begin(new(1660, 904), dragged);
for (int pixel = 1; pixel <= 20; pixel++)
{
    var incremental = new System.Drawing.Rectangle(dragged.Left - 1, dragged.Top - 1, dragged.Width, dragged.Height);
    dragged = WindowPlacement.Snap(slowDrag.BoundsAt(new(1660 - pixel, 904 - pixel), incremental), desktop, 8);
}
Check(dragged.Location == new System.Drawing.Point(1560, 844),
    "Le détachement lent fonctionne aussi au-dessus de la barre des tâches et à droite");
slowDrag.End();
Check(slowDrag.BoundsAt(new(100, 100), new(500, 500, 340, 176)).Location == new System.Drawing.Point(500, 500),
    "La fin du déplacement libère le point de prise pour le prochain déplacement");
Check(WindowPlacement.Snap(new(8, 7, 340, 176), desktop, 12).Location == new System.Drawing.Point(0, 0),
    "Le widget accroche simultanément aux bords supérieur et gauche");
Check(WindowPlacement.Snap(new(1572, 859, 340, 176), desktop, 12).Location == new System.Drawing.Point(1580, 864),
    "Le bord inférieur s’arrête au-dessus de la barre des tâches");
Check(WindowPlacement.Snap(new(900, 1000, 340, 176), desktop, 12).Bottom == desktop.Bottom,
    "Le déplacement ne permet pas de recouvrir la barre des tâches");
Check(WindowPlacement.Snap(new(700, 400, 340, 176), desktop, 12).Location == new System.Drawing.Point(700, 400),
    "Un déplacement loin des bords reste libre");
var secondary = new System.Drawing.Rectangle(-1920, 40, 1920, 1040);
Check(WindowPlacement.Snap(new(-1912, 48, 340, 176), secondary, 12).Location == new System.Drawing.Point(-1920, 40),
    "Les écrans aux coordonnées négatives et une barre des tâches en haut sont respectés");
Check(WindowPlacement.Snap(new(44, 500, 340, 176), new(40, 0, 1880, 1080), 12).Left == 40,
    "Une barre des tâches à gauche est respectée");
Exception? movementFailure = null;
var movementThread = new Thread(() =>
{
    try
    {
        var movementSettings = new Settings { Storage = new HistorySettings { Enabled = false } };
        using var widget = new Widget(movementSettings, diagnostic: true);
        var work = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position).WorkingArea;
        var proposed = new MovingProbe.Rect { Left = work.Left + 2, Top = work.Top + 2,
            Right = work.Left + 342, Bottom = work.Top + 178 };
        var memory = System.Runtime.InteropServices.Marshal.AllocHGlobal(16);
        try
        {
            System.Runtime.InteropServices.Marshal.StructureToPtr(proposed, memory, false);
            MovingProbe.Send(widget.Handle, 0x0216, 0, memory);
            var actual = System.Runtime.InteropServices.Marshal.PtrToStructure<MovingProbe.Rect>(memory);
            Check(actual.Left == work.Left && actual.Top == work.Top,
                "Le message Windows de déplacement applique réellement l’accrochage");
            movementSettings.SnapToEdges = false;
            System.Runtime.InteropServices.Marshal.StructureToPtr(proposed, memory, false);
            MovingProbe.Send(widget.Handle, 0x0216, 0, memory);
            actual = System.Runtime.InteropServices.Marshal.PtrToStructure<MovingProbe.Rect>(memory);
            Check(actual.Left == proposed.Left && actual.Top == proposed.Top,
                "Désactiver l’accrochage conserve le déplacement Windows habituel");
        }
        finally { System.Runtime.InteropServices.Marshal.FreeHGlobal(memory); }
    }
    catch (Exception ex) { movementFailure = ex; }
});
movementThread.SetApartmentState(ApartmentState.STA);
movementThread.Start(); movementThread.Join();
if (movementFailure is not null) throw movementFailure;
Sample[] peakSamples = [new(now.AddSeconds(-61), 900000, 0), new(now.AddSeconds(-55), 12000, 8000), new(now.AddSeconds(-1), 5000, 20000)];
var peakNow = TrafficPeak.InWindow(peakSamples, now, 60);
Check(peakNow.Rate == 20000 && !peakNow.Reception, "Le maximum couvre uniquement la période visible, réception et envoi comparés");
var peakLater = TrafficPeak.InWindow(peakSamples, now.AddSeconds(60), 60);
Check(peakLater.Rate == 0, "Les anciens pics disparaissent du maximum lorsque leur période expire");
var rates = new RateAccumulator();
var first = rates.Update([new("a", 10000, 20000)], 1, now);
Check(first.Down == 0 && first.Up == 0, "Le premier relevé est une référence, pas un débit");
var second = rates.Update([new("a", 12000, 21000)], 2, now.AddSeconds(2));
Check(second.Down == 1000 && second.Up == 500, "Les débits utilisent le temps écoulé réel");
Check(rates.TotalDown == 2000 && rates.TotalUp == 1000, "Volumes de session en octets");
var added = rates.Update([new("a", 13000, 21500), new("b", 9000000, 8000000)], 1, now);
Check(added.Down == 1000 && added.Up == 500, "Ajouter une carte ne crée aucun faux pic");
var aggregate = rates.Update([new("a", 14000, 22000), new("b", 9002000, 8000500)], 1, now);
Check(aggregate.Down == 3000 && aggregate.Up == 1000, "Le cumul additionne les différences par carte");
var removed = rates.Update([new("b", 9003000, 8001000)], 1, now);
Check(removed.Down == 1000 && removed.Up == 500, "Retirer une carte ne fausse pas les différences");
var reset = rates.Update([new("b", 10, 20)], 1, now);
Check(reset.Down == 0 && reset.Up == 0, "Réinitialiser les compteurs ne crée pas de débit négatif");
var afterReset = rates.Update([new("b", 110, 220)], .5, now);
Check(afterReset.Down == 200 && afterReset.Up == 400, "La mesure reprend après un reset");
var disconnected = rates.Update([], 1, now);
Check(disconnected.Down == 0 && disconnected.Up == 0, "Une déconnexion donne zéro");
var reconnected = rates.Update([new("b", 99999, 99999)], 1, now);
Check(reconnected.Down == 0 && reconnected.Up == 0, "Une reconnexion établit une nouvelle référence");
var invalid = rates.Update([new("b", 100000, 100000)], 0, now);
Check(invalid.Down == 0 && double.IsFinite(invalid.Up), "Aucune division par zéro");
rates.Reset();
Check(rates.TotalDown == 0 && rates.TotalUp == 0, "Remise à zéro des volumes");
CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
Check(Units.Rate(125000, true) == "1,0 Mbit/s", "Conversion octets vers Mbit/s");
Check(Units.Rate(125000, false) == "125,0 Ko/s", "Unités décimales en octets/s");
Check(Units.Volume(1500000) == "1,5 Mo", "Volumes en unités décimales");
var settings = new Settings { IntervalMs = -1, HistorySeconds = 100000, OpacityPercent = 0,
    FixedScaleMbps = double.NaN, Width = 1, Height = 9999 };
settings.Normalize();
Check(settings.IntervalMs == 250 && settings.HistorySeconds == 3600 && settings.OpacityPercent == 35,
    "Les réglages hors limites sont bornés");
Check(settings.FixedScaleMbps == 0 && settings.Width == 270 && settings.Height == 600,
    "Échelle et dimensions invalides corrigées");
var directory = Path.GetFullPath(args.Length > 0 ? args[0] : "artifacts/test-settings");
Directory.CreateDirectory(directory);
var path = Path.Combine(directory, "settings.json");
settings.AdapterId = "test-adapter";
settings.Save(path);
var loaded = Settings.Load(path);
Check(loaded.AdapterId == settings.AdapterId && loaded.IntervalMs == settings.IntervalMs,
    "Persistance JSON et relecture des paramètres");
File.WriteAllText(path, "{\"HistorySeconds\":60,\"IntervalMs\":1750}");
var migrated = Settings.Load(path);
Check(migrated.HistorySeconds == 300 && migrated.IntervalMs == 1750,
    "Ancienne période par défaut portée à cinq minutes sans changer la fréquence");
File.WriteAllText(path, "{\"HistorySeconds\":120,\"IntervalMs\":1000}");
Check(Settings.Load(path).HistorySeconds == 120, "Une période personnalisée existante est conservée");
var independent = new Settings { ConfigurationVersion = 1, HistorySeconds = 60, IntervalMs = 1000 };
independent.Save(path);
Check(Settings.Load(path).HistorySeconds == 60, "Une minute reste sélectionnable après la mise à jour");
independent.HistorySeconds = 3600;
independent.Save(path);
var longHistory = Settings.Load(path);
Check(longHistory.HistorySeconds == 3600 && longHistory.IntervalMs == 1000,
    "Une heure de graphique conserve les mesures toutes les 1000 ms");
Check(Units.Duration(15) == "15 s" && Units.Duration(300) == "5 min" && Units.Duration(3600) == "1 h",
    "Affichage lisible des périodes en secondes, minutes et heures");
File.WriteAllText(path, "{\"ConfigurationVersion\":1,\"HistorySeconds\":300,\"IntervalMs\":1000}");
var legacyAppearance = Settings.Load(path);
Check(legacyAppearance.Graph.DownColor == GraphAppearance.DefaultDown && legacyAppearance.Graph.LineWidth == 1.4,
    "Une configuration existante conserve l’apparence d’origine");
legacyAppearance.Graph = new GraphAppearance { DownColor = "#C040D0", UpColor = "#208020", BackgroundColor = "#FFFFFF",
    GridColor = "#777777", LineWidth = 3.5, LineStyle = 2, GridStyle = 0, ShowGrid = false, FillArea = false, FillOpacityPercent = 42 };
legacyAppearance.Save(path);
var styled = Settings.Load(path);
Check(styled.Graph.DownColor == "#C040D0" && styled.Graph.BackgroundColor == "#FFFFFF" && styled.Graph.LineWidth == 3.5 &&
    styled.Graph.LineStyle == 2 && !styled.Graph.ShowGrid && !styled.Graph.FillArea && styled.Graph.FillOpacityPercent == 42 && styled.IntervalMs == 1000,
    "Les couleurs et styles sont mémorisés indépendamment des mesures");
File.WriteAllText(path, "{\"Graph\":{\"DownColor\":\"invalide\",\"BackgroundColor\":\"oops\",\"LineWidth\":100,\"FillOpacityPercent\":-10}}");
var repaired = Settings.Load(path);
Check(repaired.Graph.DownColor == GraphAppearance.DefaultDown && repaired.Graph.BackgroundColor == "" &&
    repaired.Graph.LineWidth == 6 && repaired.Graph.FillOpacityPercent == 0,
    "Une apparence JSON invalide est corrigée sans bloquer le widget");
File.WriteAllText(path, "{\"Graph\":null}");
Check(Settings.Load(path).Graph is not null, "Une apparence absente restaure les valeurs par défaut");
var rateDefaults = new Settings();
rateDefaults.Graph.BackgroundColor = "#112233";
Check(rateDefaults.Rates.Background(rateDefaults.Graph, false).ToArgb() == System.Drawing.Color.FromArgb(17, 34, 51).ToArgb(),
    "Le fond du bandeau suit le graphique par défaut");
rateDefaults.Graph.BackgroundColor = "#AABBCC";
Check(rateDefaults.Rates.Background(rateDefaults.Graph, true).ToArgb() == System.Drawing.Color.FromArgb(170, 187, 204).ToArgb(),
    "Le fond automatique suit les changements de couleur du graphique");
rateDefaults.Rates = new RateAppearance { BackgroundColor = "#102030", TextColor = "#EEDDCC", FontFamily = "Consolas", FontSizePixels = 20, Style = 3 };
rateDefaults.Save(path);
var rateSaved = Settings.Load(path);
Check(rateSaved.Rates.BackgroundColor == "#102030" && rateSaved.Rates.TextColor == "#EEDDCC" &&
    rateSaved.Rates.FontFamily == "Consolas" && rateSaved.Rates.FontSizePixels == 20 && rateSaved.Rates.Style == 3 && rateSaved.IntervalMs == 1000,
    "Le fond, la couleur du texte et la police sont mémorisés indépendamment des mesures");
Check(rateSaved.Rates.Background(rateSaved.Graph, true).ToArgb() == System.Drawing.Color.FromArgb(16, 32, 48).ToArgb(),
    "Une couleur explicite du bandeau reste indépendante du graphique");
rateSaved.Rates.SmoothingLevel = 3; rateSaved.Save(path);
Check(Settings.Load(path).Rates.SmoothingLevel == 3 && Settings.CreateDefaults().Rates.SmoothingLevel == 1,
    "Le niveau choisi est mémorisé, avec le lissage léger par défaut pour les nouvelles installations");
File.WriteAllText(path, "{\"Rates\":{\"Compact\":true}}");
Check(Settings.Load(path).Rates.SmoothingLevel == 1 && Settings.Load(path).Rates.Compact,
    "Une ancienne configuration active le lissage léger sans perdre ses réglages du bandeau");
var unavailableFont = new RateAppearance { FontFamily = "MiniDebit police absente 123456", FontSizePixels = 18 };
using (var replacement = unavailableFont.CreateFont()) Check(replacement.Name == "Segoe UI", "Une police absente utilise Segoe UI");
File.WriteAllText(path, "{\"Rates\":null}");
Check(Settings.Load(path).Rates is not null, "Une ancienne configuration reçoit les réglages par défaut du bandeau");
File.WriteAllText(path, "{invalide");
Check(Settings.Load(path).IntervalMs == 1000, "Un fichier JSON corrompu restaure les valeurs par défaut");
Check(Settings.Load(Path.Combine(directory, "absent.json")).AdapterId == "auto", "Première ouverture sans configuration");
var firstInstall = Settings.Load(Path.Combine(directory, "absent.json"));
Check(firstInstall.HistorySeconds == 180 && firstInstall.IntervalMs == 1000 && firstInstall.OpacityPercent == 90 &&
    firstInstall.Width == 322 && firstInstall.Height == 174 && firstInstall.Rates.Compact && firstInstall.Rates.FontSizePixels == 16 &&
    firstInstall.Graph.MaximumText.FontSizePixels == 11 && firstInstall.Graph.MarginPixels == 1 &&
    firstInstall.X is null && firstInstall.Y is null,
    "Une nouvelle installation utilise le profil capturé, sans position ni carte propres à ce PC");
firstInstall.Graph.DownColor = "#112233";
Check(Settings.CreateDefaults().Graph.DownColor == "#0078FF",
    "Modifier une configuration ne modifie pas le profil intégré des futures installations");
var monitor = new NetworkMonitor();
var live = monitor.Poll("auto");
Check(live.Down >= 0 && live.Up >= 0 && !string.IsNullOrEmpty(monitor.Status), "Lecture des interfaces Windows réelles");
var databasePath = Path.Combine(directory, $"history-{Guid.NewGuid():N}.sqlite");
var databaseNow = DateTimeOffset.UtcNow;
using (var database = new TrafficDatabase(databasePath))
{
    var recorded = new HistoryRow(new Sample(databaseNow, 1200, 600, 2, 2400, 1200), "adapter-test", "Éthernet ' test", 1000);
    database.Insert([recorded, recorded with { Sample = new Sample(databaseNow.AddDays(-8), 10, 20, 1, 10, 20) }], "session-test");
    Check(database.Scalar("SELECT COUNT(*) FROM measurements") == 2 && database.Scalar("SELECT SUM(received_bytes) FROM measurements") == 2410,
        "SQLite stocke les mesures individuelles et les volumes réels");
    Check(database.ScalarText("PRAGMA integrity_check") == "ok", "L’intégrité SQLite est vérifiée après transaction");
    Check(database.ScalarText("SELECT adapter_name FROM measurements LIMIT 1") == "Éthernet ' test", "Le nom Unicode de la carte est conservé sans interprétation SQL");
    bool rolledBack = false;
    try { database.Insert([recorded, recorded with { AdapterName = null! }], "bad-session"); }
    catch (InvalidOperationException) { rolledBack = true; }
    Check(rolledBack && database.Scalar("SELECT COUNT(*) FROM measurements") == 2, "Un lot invalide est entièrement annulé");
    database.Prune(databaseNow.AddDays(-7).ToUnixTimeMilliseconds());
    Check(database.Scalar("SELECT COUNT(*) FROM measurements") == 1, "La purge conserve les données récentes");
}
var asynchronousPath = Path.Combine(directory, $"async-{Guid.NewGuid():N}.sqlite");
using (var writer = new HistoryRecorder(asynchronousPath, 50))
{
    writer.Configure(new HistorySettings());
    for (int i = 0; i < 10; i++) writer.Record(new Sample(databaseNow.AddMilliseconds(i * 1000), 100, 50, 1, 100, 50), "adapter", "test", 1000);
}
using (var database = new TrafficDatabase(asynchronousPath))
    Check(database.Scalar("SELECT COUNT(*) FROM measurements") == 10, "La fermeture écrit le dernier lot en attente");
var disabledPath = Path.Combine(directory, $"disabled-{Guid.NewGuid():N}.sqlite");
using (var disabled = new HistoryRecorder(disabledPath)) disabled.Configure(new HistorySettings { Enabled = false });
Check(!File.Exists(disabledPath), "L’historique désactivé ne crée aucune base");
using (var failed = new HistoryRecorder(directory))
{
    failed.Configure(new HistorySettings());
    failed.Record(new Sample(databaseNow, 100, 100, 1, 100, 100), "adapter", "test", 1000);
    failed.Dispose();
    Check(failed.Error is not null, "Une erreur de stockage est signalée sans arrêter les mesures");
}
Console.WriteLine($"\n{count} vérifications réussies.");

static class MovingProbe
{
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }
    [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SendMessageW")]
    public static extern nint Send(nint window, uint message, nint wparam, nint lparam);
}
