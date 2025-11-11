# Template Engine - Variable Replacement Guide

Die Digital Signage System verwendet [Scriban](https://github.com/scriban/scriban), eine leistungsstarke .NET Template Engine, für dynamische Variable-Ersetzung in Text-Elementen.

## Inhaltsverzeichnis

- [Übersicht](#übersicht)
- [Grundlegende Syntax](#grundlegende-syntax)
- [Verfügbare Funktionen](#verfügbare-funktionen)
- [Beispiele](#beispiele)
- [Datenquellen-Integration](#datenquellen-integration)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Übersicht

### Wie funktioniert es?

1. **Datenquelle konfigurieren**: SQL-Abfrage definieren, die Daten liefert
2. **Template im Layout-Element**: Variablen mit `{{VariableName}}` Syntax verwenden
3. **Automatische Verarbeitung**: Server ersetzt Variablen vor dem Senden an Clients
4. **Auto-Refresh**: Templates werden bei Datenaktualisierungen automatisch neu verarbeitet

### Vorteile

- ✅ **Dynamische Inhalte**: Zeigen Sie Echtzeit-Daten aus Datenbanken
- ✅ **Formatierung**: Datums-, Zahlen- und Text-Formatierung
- ✅ **Bedingungen**: If/else Logik für kontextbezogene Anzeige
- ✅ **Schleifen**: Mehrere Datensätze anzeigen
- ✅ **Berechnungen**: Einfache Mathematik und String-Operations

---

## Grundlegende Syntax

### Variable einsetzen

```html
Willkommen {{Benutzername}}!
```

**Datenbankabfrage:**
```sql
SELECT 'Max Mustermann' AS Benutzername
```

**Ergebnis:**
```
Willkommen Max Mustermann!
```

### Mehrere Variablen

```html
Raum: {{RaumName}}
Temperatur: {{Temperatur}}°C
Status: {{Status}}
```

**Datenbankabfrage:**
```sql
SELECT
    'Konferenzraum A' AS RaumName,
    22.5 AS Temperatur,
    'Verfügbar' AS Status
```

**Ergebnis:**
```
Raum: Konferenzraum A
Temperatur: 22.5°C
Status: Verfügbar
```

### Verschachtelte Eigenschaften

```html
{{Data.Wert}}
{{Info.Details.Name}}
```

### Fallback-Werte

```html
{{Benutzername ?? "Gast"}}
{{Wert ?? 0}}
```

Wenn `Benutzername` null oder leer ist, wird "Gast" angezeigt.

---

## Verfügbare Funktionen

### Datums-Formatierung

```html
<!-- Datum formatieren -->
{{date_format Datum "dd.MM.yyyy"}}
{{date_format Zeitstempel "HH:mm:ss"}}
{{date_format DatumZeit "dd.MM.yyyy HH:mm"}}
```

**Beispiele:**
- `dd.MM.yyyy` → 15.12.2024
- `HH:mm` → 14:30
- `dddd, dd. MMMM yyyy` → Montag, 15. Dezember 2024

**Datenbankabfrage:**
```sql
SELECT
    GETDATE() AS Datum,
    CONVERT(VARCHAR, GETDATE(), 108) AS Zeitstempel
```

### Zahlen-Formatierung

```html
<!-- Zahlen formatieren -->
{{number_format Preis "C2"}}        <!-- Währung: 123,45 € -->
{{number_format Prozent "P2"}}      <!-- Prozent: 45,67% -->
{{number_format Anzahl "N0"}}       <!-- Mit Tausender-Trenner: 1.234 -->
{{number_format Dezimal "F2"}}      <!-- Feste 2 Nachkommastellen: 12,34 -->
```

**Format-Codes:**
- `C` oder `C2` - Currency (Währung)
- `P` oder `P2` - Percent (Prozent)
- `N` oder `N2` - Number (Zahl mit Tausender-Trenner)
- `F2` - Fixed-point (Feste Nachkommastellen)

**Datenbankabfrage:**
```sql
SELECT
    1234.56 AS Preis,
    0.4567 AS Prozent,
    1234567 AS Anzahl
```

### Text-Funktionen

```html
<!-- Groß-/Kleinschreibung -->
{{upper Name}}          <!-- MAX MUSTERMANN -->
{{lower Name}}          <!-- max mustermann -->

<!-- Default-Wert -->
{{default Beschreibung "Keine Beschreibung verfügbar"}}
```

### Mathematische Operationen

```html
<!-- Grundrechenarten -->
{{Wert1 + Wert2}}
{{Gesamt - Rabatt}}
{{Anzahl * Preis}}
{{Summe / Anzahl}}

<!-- Beispiel: Prozentuale Auslastung -->
{{(BelegteZimmer / GesamtZimmer) * 100}}%
```

### Bedingungen (If/Else)

```html
{{if Status == "online"}}
    ✓ System läuft
{{else if Status == "wartung"}}
    ⚠ Wartungsmodus
{{else}}
    ✗ System offline
{{end}}
```

**Vergleichsoperatoren:**
- `==` - Gleich
- `!=` - Ungleich
- `>` - Größer
- `<` - Kleiner
- `>=` - Größer oder gleich
- `<=` - Kleiner oder gleich

**Logische Operatoren:**
- `&&` - Und
- `||` - Oder
- `!` - Nicht

### Schleifen

```html
<!-- Liste von Terminen -->
{{for termin in Termine}}
    {{termin.Zeit}} - {{termin.Titel}}
{{end}}

<!-- Mit Index -->
{{for termin in Termine}}
    {{for.index + 1}}. {{termin.Titel}}
{{end}}
```

**Datenbankabfrage:**
```sql
SELECT
    '09:00' AS Zeit, 'Meeting' AS Titel
UNION ALL
SELECT '11:00', 'Präsentation'
UNION ALL
SELECT '14:30', 'Workshop'
```

---

## Beispiele

### Beispiel 1: Raumbelegungsplan

**Text-Element Content:**
```html
=== Raum {{RaumNummer}} ===

{{if Belegt == 1}}
    ■ BELEGT
    Veranstaltung: {{Veranstaltung}}
    Bis: {{date_format EndeZeit "HH:mm"}}
{{else}}
    □ FREI
    Nächste Buchung: {{date_format NaechstesBuchung "HH:mm"}}
{{end}}

Kapazität: {{Kapazitaet}} Personen
```

**SQL-Abfrage:**
```sql
SELECT
    'A101' AS RaumNummer,
    CASE WHEN GETDATE() BETWEEN StartZeit AND EndeZeit THEN 1 ELSE 0 END AS Belegt,
    Veranstaltung,
    EndeZeit,
    NaechstesBuchung,
    50 AS Kapazitaet
FROM Raumbuchungen
WHERE RaumNummer = 'A101'
```

### Beispiel 2: Verkaufsstatistik

**Text-Element Content:**
```html
📊 UMSATZ HEUTE

Verkäufe: {{number_format Verkauefe "N0"}}
Umsatz: {{number_format Umsatz "C2"}}

Durchschnitt: {{number_format (Umsatz / Verkauefe) "C2"}}

{{if Umsatz > 10000}}
    ⭐ Ziel erreicht!
{{else}}
    Noch {{number_format (10000 - Umsatz) "C2"}} bis zum Ziel
{{end}}
```

**SQL-Abfrage:**
```sql
SELECT
    COUNT(*) AS Verkauefe,
    SUM(Betrag) AS Umsatz
FROM Verkauefe
WHERE CAST(Datum AS DATE) = CAST(GETDATE() AS DATE)
```

### Beispiel 3: Mitarbeiter-Anwesenheit

**Text-Element Content:**
```html
👥 ANWESEND ({{AnzahlAnwesend}}/{{AnzahlGesamt}})

{{for mitarbeiter in Mitarbeiter}}
    {{if mitarbeiter.Anwesend}}
        ✓ {{mitarbeiter.Name}} - {{mitarbeiter.Abteilung}}
    {{end}}
{{end}}

Auslastung: {{number_format (AnzahlAnwesend / AnzahlGesamt) "P0"}}
```

**SQL-Abfrage:**
```sql
-- Anwesenheit mit Details
SELECT
    Name,
    Abteilung,
    CASE WHEN Status = 'Anwesend' THEN 1 ELSE 0 END AS Anwesend
FROM Mitarbeiter
WHERE Status IN ('Anwesend', 'Abwesend')
ORDER BY Name

-- Zusätzlich: Zähler
SELECT
    SUM(CASE WHEN Status = 'Anwesend' THEN 1 ELSE 0 END) AS AnzahlAnwesend,
    COUNT(*) AS AnzahlGesamt
FROM Mitarbeiter
```

### Beispiel 4: Temperatur-Monitor

**Text-Element Content:**
```html
🌡️ Raumklima

Temperatur: {{number_format Temperatur "F1"}}°C

{{if Temperatur < 18}}
    ❄️ Zu kalt
{{else if Temperatur > 24}}
    🔥 Zu warm
{{else}}
    ✓ Optimal
{{end}}

Luftfeuchtigkeit: {{number_format Luftfeuchtigkeit "F0"}}%
Letzte Messung: {{date_format Zeitstempel "HH:mm:ss"}}
```

**SQL-Abfrage:**
```sql
SELECT TOP 1
    Temperatur,
    Luftfeuchtigkeit,
    Zeitstempel
FROM Sensordaten
WHERE SensorID = 'RAUM-A101'
ORDER BY Zeitstempel DESC
```

### Beispiel 5: Aufgaben-Liste

**Text-Element Content:**
```html
📋 OFFENE AUFGABEN ({{AnzahlOffen}})

{{for aufgabe in Aufgaben}}
    {{if aufgabe.Prioritaet == "Hoch"}}
        🔴
    {{else if aufgabe.Prioritaet == "Mittel"}}
        🟡
    {{else}}
        🟢
    {{end}}
    {{aufgabe.Titel}}
    Fällig: {{date_format aufgabe.Faellig "dd.MM."}}
{{end}}
```

**SQL-Abfrage:**
```sql
SELECT TOP 5
    Titel,
    Prioritaet,
    Faellig
FROM Aufgaben
WHERE Status = 'Offen'
ORDER BY
    CASE Prioritaet
        WHEN 'Hoch' THEN 1
        WHEN 'Mittel' THEN 2
        ELSE 3
    END,
    Faellig
```

---

## Datenquellen-Integration

### DataSource konfigurieren

1. **Datenquelle erstellen** im Digital Signage Manager
2. **Connection String** zur SQL-Datenbank eingeben
3. **SQL-Abfrage** definieren
4. **Refresh-Intervall** festlegen (z.B. 30 Sekunden)

### Verfügbare Daten im Template

Alle Spalten der SQL-Abfrage werden als Variablen verfügbar:

**SQL:**
```sql
SELECT
    'Max' AS Vorname,
    'Mustermann' AS Nachname,
    30 AS Alter
```

**Template:**
```html
{{Vorname}} {{Nachname}} ({{Alter}} Jahre)
```

### Mehrere DataSources

Wenn Sie mehrere Datenquellen verwenden, werden alle Daten kombiniert:

**DataSource 1: Temperatur**
```sql
SELECT 22.5 AS Temperatur, 45 AS Luftfeuchtigkeit
```

**DataSource 2: Status**
```sql
SELECT 'Online' AS Status, 5 AS Anwesend
```

**Template:**
```html
Temperatur: {{Temperatur}}°C
Luftfeuchtigkeit: {{Luftfeuchtigkeit}}%
Status: {{Status}}
Anwesend: {{Anwesend}}
```

---

## Best Practices

### 1. Verwenden Sie aussagekräftige Spaltennamen

❌ **Schlecht:**
```sql
SELECT value1, value2, value3
```

✅ **Gut:**
```sql
SELECT
    Temperatur,
    Luftfeuchtigkeit,
    Zeitstempel
```

### 2. Behandeln Sie NULL-Werte

```sql
-- In SQL
SELECT ISNULL(Beschreibung, 'Keine Angabe') AS Beschreibung

-- Oder im Template
{{Beschreibung ?? "Keine Angabe"}}
```

### 3. Begrenzen Sie Datensätze

```sql
-- Für Schleifen: Limitieren Sie die Anzahl
SELECT TOP 10 * FROM Termine
ORDER BY Datum DESC
```

### 4. Optimieren Sie SQL-Abfragen

- Verwenden Sie Indizes
- Vermeiden Sie `SELECT *`
- Filtern Sie Daten in SQL, nicht im Template
- Nutzen Sie Views für komplexe Abfragen

### 5. Fehlerbehandlung

```html
<!-- Mit Default-Wert -->
Wert: {{Wert ?? "N/A"}}

<!-- Mit Bedingung -->
{{if Wert}}
    Wert: {{Wert}}
{{else}}
    Kein Wert verfügbar
{{end}}
```

### 6. Lesbarkeit

```html
<!-- Gut strukturiert und lesbar -->
=== {{Titel}} ===

Status: {{Status}}
Aktualisiert: {{date_format Zeitstempel "HH:mm"}}

{{if Details}}
    Details: {{Details}}
{{end}}
```

---

## Troubleshooting

### Problem: Variable wird nicht ersetzt

**Symptom:** `{{MeinVariable}}` wird als Text angezeigt

**Lösungen:**
1. Prüfen Sie den Spaltennamen in der SQL-Abfrage
2. Stellen Sie sicher, dass die Datenquelle enabled ist
3. Überprüfen Sie die SQL-Verbindung
4. Schauen Sie in die Server-Logs

### Problem: Formatierung funktioniert nicht

**Symptom:** `{{date_format Datum "dd.MM.yyyy"}}` zeigt falsches Format

**Lösungen:**
1. Prüfen Sie, ob die Spalte ein gültiges Datum ist
2. Verwenden Sie `CAST` in SQL: `CAST(DatumString AS DATETIME) AS Datum`
3. Verwenden Sie korrekte Format-Strings

### Problem: Schleife zeigt keine Daten

**Symptom:** `{{for item in Items}}` iteriert nicht

**Lösungen:**
1. Die SQL-Abfrage muss mehrere Zeilen zurückgeben
2. Verwenden Sie `SELECT` ohne `TOP 1`
3. Stellen Sie sicher, dass `_rows` in layoutData vorhanden ist

### Problem: Performance-Probleme

**Symptom:** Langsames Laden oder Aktualisieren

**Lösungen:**
1. Optimieren Sie SQL-Abfragen
2. Erhöhen Sie das Refresh-Intervall
3. Reduzieren Sie die Anzahl der Templates pro Layout
4. Verwenden Sie Indizes in der Datenbank

### Problem: Template-Syntax-Fehler

**Symptom:** Layout wird nicht angezeigt

**Lösungen:**
1. Überprüfen Sie geschlossene Tags: `{{if}}...{{end}}`
2. Verwenden Sie `ValidateTemplate()` zum Testen
3. Schauen Sie in die Server-Logs für Parsing-Fehler

---

## Erweiterte Features

### Custom Functions

Custom functions können in `TemplateFunctions.cs` hinzugefügt werden:

```csharp
public static string MyCustomFunction(string value)
{
    return value.ToUpper();
}
```

### Template-Vererbung

```html
<!-- Nicht direkt unterstützt, aber mit Includes möglich -->
{{include "header"}}
{{Content}}
{{include "footer"}}
```

### Caching

Templates werden automatisch gecacht. Bei Änderungen wird der Cache invalidiert.

---

## Weitere Ressourcen

- [Scriban Dokumentation](https://github.com/scriban/scriban/tree/master/doc)
- [Scriban Language Reference](https://github.com/scriban/scriban/blob/master/doc/language.md)
- [C# Format Strings](https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings)
- [SQL Server Datentypen](https://docs.microsoft.com/en-us/sql/t-sql/data-types/data-types-transact-sql)

---

## Support

Bei Fragen oder Problemen:
1. Überprüfen Sie die Server-Logs: `logs/digitalsignage-*.txt`
2. Testen Sie die SQL-Abfrage direkt in SQL Server Management Studio
3. Verwenden Sie `ValidateTemplate()` zum Testen der Template-Syntax
4. Konsultieren Sie die Scriban-Dokumentation für erweiterte Syntax

