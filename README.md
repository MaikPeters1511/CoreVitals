# CoreVitals.Resilience

Eine hochperformante, leichtgewichtige und Dependency-Injection-freundliche Resilience-Bibliothek für .NET, die eine robuste Ausführung von Operationen durch konfigurierbare Retry-Strategien mit exponentiellem Backoff und Jitter ermöglicht.

---

## Funktionsumfang

### 1. Exponentielles Backoff (Exponential Backoff)
Anstatt einen fehlgeschlagenen Aufruf sofort wiederholt abzufeuern (was den Zielserver bei einer Überlastung nur noch weiter in die Knie zwingen würde), wartet die Bibliothek nach jedem Fehlversuch progressiv länger:
- Die Wartezeit berechnet sich dynamisch anhand der Formel: $Delay = InitialDelay \times (BackoffFactor^{Attempt - 1})$
- Beispiel bei 100ms Initial-Delay und Faktor 2.0: **100ms** $\rightarrow$ **200ms** $\rightarrow$ **400ms** $\rightarrow$ **800ms** usw.
- Über die Eigenschaft `MaxDelay` lässt sich eine Obergrenze festlegen, damit die Wartezeiten nicht ins Unendliche wachsen.

### 2. Full Jitter (Verhinderung des "Thundering Herd"-Effekts)
Wenn viele Instanzen eines Services gleichzeitig auf eine ausgefallene Ressource zugreifen und alle denselben statischen Backoff-Algorithmus nutzen, treffen die Wiederholungsversuche alle zur exakt gleichen Zeit auf dem Server ein.
- **Full Jitter** fügt der Wartezeit ein kontrolliertes Zufallsrauschen hinzu. Die tatsächliche Wartezeit wird zufällig zwischen `0` und dem maximal berechneten exponentiellen Delay gestreut.
- Dies entzerrt die Last auf APIs und Datenbanken unter Hochlastbedingungen dramatisch.

### 3. Selektive Fehlerfilterung (`ShouldRetry`)
Nicht jeder Fehler sollte einen Wiederholungsversuch auslösen. Wenn eine API mit einem `401 Unauthorized` oder einer `ArgumentException` antwortet, wird auch ein erneuter Versuch das Problem nicht lösen.
- Über das Prädikat `ShouldRetry` steuern Sie präzise, bei welchen Exceptions ein Retry sinnvoll ist (z. B. nur bei `HttpRequestException` oder `DbException`).
- `OperationCanceledException` (Abbruch durch den Nutzer) wird standardmäßig **nie** wiederholt.

### 4. Volle Unterstützung für asynchrone Programmierung & Cancellation
- Die gesamte Bibliothek ist voll-asynchron (`async`/`await`) und mit `ConfigureAwait(false)` optimiert.
- Jede Methode akzeptiert ein `CancellationToken`. Ein Abbruch wird sowohl **während** der Ausführung Ihrer Methode als auch **während der Wartezeit** (Delay) sofort berücksichtigt, ohne Threads zu blockieren.

### 5. Echtzeit-Monitoring & Logging (`OnRetry`)
- Über den optionalen Callback `OnRetry` erhalten Sie vor jeder Wartezeit Zugriff auf die aufgetretene Exception, die aktuelle Versuchsnummer und das exakt berechnete Delay.
- Optimal für die Integration in strukturiertes Logging (z. B. Serilog, OpenTelemetry, Application Insights).

### 6. Plattformkompatibilität & Performance (Multi-Targeting)
- **.NET 8.0+:** Nutzt modernste und hochperformante APIs wie `Random.Shared` (vollkommen allocationsfrei und thread-sicher).
- **.NET Standard 2.0:** Kompatibel mit älteren Plattformen (z. B. .NET Framework). Die Thread-Sicherheit bei der Jitter-Generierung wird ohne Locks durch ein `[ThreadStatic] Random` realisiert.
- Keine externen Abhängigkeiten (außer DI Abstractions), was das NuGet-Paket extrem schlank hält.

---

## Installation

Fügen Sie das Paket zu Ihrem Projekt hinzu:

```bash
dotnet add package CoreVitals.Resilience
```

---

## Verwendung

### 1. Einfache manuelle Verwendung

Sie können die `RetryPolicy` direkt erstellen und konfigurieren:

```csharp
using CoreVitals.Resilience;

// Konfiguration definieren
var options = new RetryOptions
{
    MaxRetries = 3,
    InitialDelay = TimeSpan.FromMilliseconds(200),
    MaxDelay = TimeSpan.FromSeconds(5),
    BackoffFactor = 2.0,
    UseJitter = true,
    // Nur bei transienten Netzwerkfehlern o.Ä. retryen
    ShouldRetry = ex => ex is HttpRequestException || ex is TimeoutException,
    // Callback vor jedem Delay
    OnRetry = (ex, attempt, delay) => 
    {
        Console.WriteLine($"Versuch {attempt} fehlgeschlagen. Warte {delay.TotalMilliseconds}ms. Fehler: {ex.Message}");
    }
};

// Policy instanziieren
IResiliencePolicy policy = new RetryPolicy(options);

// Ausführen
try
{
    string result = await policy.ExecuteAsync(async cancellationToken =>
    {
        // Ihre I/O oder API-Operation
        return await myHttpClient.GetStringAsync("https://api.example.com/data", cancellationToken);
    }, CancellationToken.None);
    
    Console.WriteLine($"Erfolg: {result}");
}
catch (Exception ex)
{
    Console.WriteLine($"Alle Versuche fehlgeschlagen: {ex.Message}");
}
```

### 2. Integration in Dependency Injection (DI)

Registrieren Sie die Policy in Ihrer `Program.cs` oder Ihrem Startup-Code:

```csharp
using Microsoft.Extensions.DependencyInjection;
using CoreVitals.Resilience;

var services = new ServiceCollection();

// Registrierung im DI-Container
services.AddRetryPolicy(options =>
{
    options.MaxRetries = 5;
    options.InitialDelay = TimeSpan.FromMilliseconds(100);
    options.MaxDelay = TimeSpan.FromSeconds(10);
    options.ShouldRetry = ex => ex is InvalidOperationException;
});

var serviceProvider = services.BuildServiceProvider();
```

Konsumieren Sie die Schnittstelle `IResiliencePolicy` in Ihren Services:

```csharp
public class MyService
{
    private readonly IResiliencePolicy _resiliencePolicy;
    private readonly HttpClient _httpClient;

    public MyService(IResiliencePolicy resiliencePolicy, HttpClient httpClient)
    {
        _resiliencePolicy = resiliencePolicy;
        _httpClient = httpClient;
    }

    public async Task<string> GetDataWithRetryAsync(CancellationToken cancellationToken)
    {
        return await _resiliencePolicy.ExecuteAsync(async ct =>
        {
            var response = await _httpClient.GetAsync("https://api.example.com/v1/resource", ct);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync(ct);
        }, cancellationToken);
    }
}
```

---

## Konfigurationsoptionen (`RetryOptions`)

| Eigenschaft | Typ | Standardwert | Beschreibung |
| :--- | :--- | :--- | :--- |
| `MaxRetries` | `int` | `3` | Maximale Anzahl an Wiederholungsversuchen. |
| `InitialDelay` | `TimeSpan` | `100ms` | Wartezeit vor dem allerersten Wiederholungsversuch. |
| `MaxDelay` | `TimeSpan` | `30s` | Obergrenze für die Verzögerung zwischen Versuchen. |
| `BackoffFactor` | `double` | `2.0` | Der Exponent für die Berechnung der nächsten Verzögerung. |
| `UseJitter` | `bool` | `true` | Bestimmt, ob eine zufällige Abweichung (Full Jitter) auf das Delay addiert wird. |
| `ShouldRetry` | `Func<Exception, bool>` | `_ => true` | Ein Prädikat, das entscheidet, ob eine Exception für einen Retry qualifiziert ist. |
| `OnRetry` | `Action<Exception, int, TimeSpan>?` | `null` | Callback-Aktion, die aufgerufen wird, bevor die Wartezeit vor dem nächsten Retry startet. |

---

## CI/CD & Deployment

Die Bereitstellung bei NuGet.org erfolgt vollautomatisch über GitHub Actions. Der entsprechende Workflow befindet sich unter [publish.yml](file:///.github/workflows/publish.yml).

### Voraussetzungen für den GitHub-Workflow:
1. **NuGet API-Key:** Erstellen Sie einen API-Key auf [NuGet.org](https://www.nuget.org) mit Berechtigungen zum Pushen Ihres Pakets.
2. **Repository Secret:** Hinterlegen Sie diesen API-Key in den Einstellungen Ihres GitHub-Repositorys unter **Settings > Secrets and variables > Actions** mit dem Namen `NUGET_API_KEY`.

### Workflow auslösen:
- **Automatisch:** Der Workflow startet automatisch, sobald ein Tag mit dem Format `v*` (z. B. `v1.0.0`) auf den Repository-Zweig gepusht wird.
- **Manuell:** Sie können den Workflow über die Registerkarte **Actions** in GitHub manuell triggern (`workflow_dispatch`).

