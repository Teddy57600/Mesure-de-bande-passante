using MiniDebit;
using System.Globalization;

int count = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine($"OK {++count:00} — {description}");
}
var now = DateTimeOffset.Now;
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
var unavailableFont = new RateAppearance { FontFamily = "MiniDebit police absente 123456", FontSizePixels = 18 };
using (var replacement = unavailableFont.CreateFont()) Check(replacement.Name == "Segoe UI", "Une police absente utilise Segoe UI");
File.WriteAllText(path, "{\"Rates\":null}");
Check(Settings.Load(path).Rates is not null, "Une ancienne configuration reçoit les réglages par défaut du bandeau");
File.WriteAllText(path, "{invalide");
Check(Settings.Load(path).IntervalMs == 1000, "Un fichier JSON corrompu restaure les valeurs par défaut");
Check(Settings.Load(Path.Combine(directory, "absent.json")).AdapterId == "auto", "Première ouverture sans configuration");
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
