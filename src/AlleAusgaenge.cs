// ---------------------------------------------------------------
//  Alle Ausgaenge
//
//  Macht beim Serverstart jeden Ausgang jeder Karte immer erreichbar:
//    * Chance auf 100 %        (viele stehen ab Werk auf 50 oder 66)
//    * Einstiegspunkte vereint (sonst zaehlt, auf welcher Seite man
//      gespawnt ist - "Customs" oder "Boiler Tanks" auf dem Zoll)
//    * Zeitfenster geoeffnet   (MinTime/MaxTime auf 0)
//
//  WARUM ZUSAETZLICH ZU SVM: SVM kann das fast, aber nicht ganz.
//    - ChanceExtracts laesst Ausgaenge mit Chance GENAU 0 aus; das
//      sind "Rock Passage" (Ufer) und "E6" (Straßen).
//    - ExtendedExtracts hat fest verdrahtete Zweige fuer sieben
//      Karten. sandbox_high - Ground Zero ab Stufe 20, und genau da
//      steht SandboxAccessLevel - fehlt darin. Seine fuenf Ausgaenge
//      bleiben damit an die Spawn-Seite gebunden.
//    - Zeitfenster fasst SVM gar nicht an.
//  Dieser Mod arbeitet nicht mit Kartennamen, sondern geht ueber
//  alles drueber. Kein Streit mit SVM: beide setzen dieselben Werte
//  in dieselbe Richtung, wer zuletzt laeuft, gewinnt - das Ergebnis
//  ist identisch.
//
//  ZUEGE SIND AUSGENOMMEN. Ihr Zeitfenster gehoert zur Ankunft des
//  Zuges; ohne Zug nuetzt ein offener Ausgang nichts, er waere nur
//  kaputt. Erkannt am Namen ("train" bzw. "EXFIL_Train").
//
//  Einstellbar in ausgaenge.json neben der DLL - ohne Neubau.
// ---------------------------------------------------------------
using System.Text.Json;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Eft.Common;
using SPTarkov.Server.Core.Models.Spt.Mod;
using SPTarkov.Server.Core.Models.Spt.Tables;
using SPTarkov.Common.Models.Logging;

using Pfad = System.IO.Path;

namespace AlleAusgaenge;

public record ModMetadata : IModMetadata
{
    public string ModGuid { get; init; } = "com.kaan.alleausgaenge";
    public string Name { get; init; } = "Alle Ausgaenge";
    public string Author { get; init; } = "Kaan";
    public List<string>? Contributors { get; init; } = new();
    public SemanticVersioning.Version Version { get; init; } = new(1, 0, 0);
    public SemanticVersioning.Range SptVersion { get; init; } = new("~4.1");
    public bool HasPrepatcher { get; init; } = false;
    public List<string>? Incompatibilities { get; init; } = new();
    public Dictionary<string, SemanticVersioning.Range>? ModDependencies { get; init; } = new();
    public string? Url { get; init; } = "";
    public string License { get; init; } = "MIT";
}

[Injectable(InjectionType.Singleton)]
public class AusgangsOeffner(
    ISptLogger<AusgangsOeffner> logger,
    LocationTable karten) : IOnLoad
{
    public Task OnLoadAsync(CancellationToken abbruch)
    {
        try
        {
            var (chance, einstiege, zeitfenster) = EinstellungenLesen();

            int karten_ = 0, ausgaenge = 0;
            int gesetztChance = 0, gesetztEinstieg = 0, gesetztZeit = 0, zuege = 0;

            foreach (var (name, ort) in karten.GetDictionary())
            {
                var liste = ort?.Base?.Exits?.ToList();
                if (liste is null || liste.Count == 0) continue;
                karten_++;

                // Alle auf dieser Karte ueberhaupt vorkommenden
                // Einstiegspunkte einsammeln - daraus wird der gemeinsame
                // Satz, den danach jeder Ausgang bekommt.
                var alle = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in liste)
                {
                    foreach (var teil in (a.EntryPoints ?? "").Split(',',
                                 StringSplitOptions.RemoveEmptyEntries
                                 | StringSplitOptions.TrimEntries))
                    {
                        alle.Add(teil);
                    }
                }
                var vereint = string.Join(",", alle);

                foreach (var a in liste)
                {
                    ausgaenge++;
                    var istZug = (a.Name ?? "").Contains("train",
                                     StringComparison.OrdinalIgnoreCase);
                    if (istZug) zuege++;

                    if (chance && a.Chance is not 100.0)
                    {
                        a.Chance = 100.0;
                        a.ChancePVE = 100.0;
                        gesetztChance++;
                    }

                    if (einstiege && vereint.Length > 0
                        && !string.Equals(a.EntryPoints, vereint,
                               StringComparison.OrdinalIgnoreCase))
                    {
                        a.EntryPoints = vereint;
                        gesetztEinstieg++;
                    }

                    // Zeitfenster nur bei Nicht-Zuegen oeffnen.
                    if (zeitfenster && !istZug
                        && ((a.MinTime ?? 0) != 0 || (a.MaxTime ?? 0) != 0))
                    {
                        a.MinTime = 0; a.MaxTime = 0;
                        a.MinTimePVE = 0; a.MaxTimePVE = 0;
                        gesetztZeit++;
                    }
                }

                ort!.Base.Exits = liste;
            }

            if (ausgaenge == 0)
            {
                logger.Warning("[Alle Ausgaenge] Keine Ausgaenge gefunden - "
                               + "hat sich die Datenbank geaendert?");
                return Task.CompletedTask;
            }

            logger.Success($"[Alle Ausgaenge] {ausgaenge} Ausgaenge auf {karten_} Karten: "
                           + $"Chance {gesetztChance}, Einstiegspunkte {gesetztEinstieg}, "
                           + $"Zeitfenster {gesetztZeit} (Zuege ausgenommen: {zuege}).");
        }
        catch (Exception ex)
        {
            logger.Error("[Alle Ausgaenge] " + ex.Message);
        }

        return Task.CompletedTask;
    }

    private (bool Chance, bool Einstiege, bool Zeitfenster) EinstellungenLesen()
    {
        var standard = (true, true, true);

        var ordner = Pfad.GetDirectoryName(typeof(AusgangsOeffner).Assembly.Location);
        var datei = Pfad.Combine(ordner ?? ".", "ausgaenge.json");
        if (!File.Exists(datei))
        {
            logger.Info("[Alle Ausgaenge] Keine ausgaenge.json - alles an.");
            return standard;
        }

        try
        {
            using var papier = JsonDocument.Parse(File.ReadAllText(datei));
            var w = papier.RootElement;
            bool Lies(string name, bool standardwert)
                => w.TryGetProperty(name, out var v)
                   && (v.ValueKind == JsonValueKind.True || v.ValueKind == JsonValueKind.False)
                   ? v.GetBoolean() : standardwert;

            return (Lies("chance", true), Lies("einstiegspunkte", true),
                    Lies("zeitfenster", true));
        }
        catch (Exception ex)
        {
            logger.Error("[Alle Ausgaenge] ausgaenge.json ließ sich nicht lesen ("
                         + ex.Message + ") - alles an.");
            return standard;
        }
    }
}
