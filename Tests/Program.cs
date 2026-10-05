using MiniDebit;
using System.Globalization;

int count = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine($"OK {++count:00} — {description}");
}
var now = DateTimeOffset.Now;
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
Check(settings.IntervalMs == 250 && settings.HistorySeconds == 600 && settings.OpacityPercent == 35,
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
File.WriteAllText(path, "{invalide");
Check(Settings.Load(path).IntervalMs == 1000, "Un fichier JSON corrompu restaure les valeurs par défaut");
Check(Settings.Load(Path.Combine(directory, "absent.json")).AdapterId == "auto", "Première ouverture sans configuration");
var monitor = new NetworkMonitor();
var live = monitor.Poll("auto");
Check(live.Down >= 0 && live.Up >= 0 && !string.IsNullOrEmpty(monitor.Status), "Lecture des interfaces Windows réelles");
Console.WriteLine($"\n{count} vérifications réussies.");
