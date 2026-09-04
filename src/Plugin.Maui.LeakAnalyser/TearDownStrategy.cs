namespace Plugin.Maui.LeakAnalyser;

/// <summary>
/// How <see cref="TearDown"/> treats a view after lifecycle inference says it is finished.
/// </summary>
public enum TearDownStrategy
{
    /// <summary>
    /// Watch only. The visual tree and handlers are left intact.
    /// </summary>
    DetectOnly = 0,

    /// <summary>
    /// Disconnect handlers with a safe tree walk. Managed references stay in place.
    /// This is the default on modern MAUI.
    /// </summary>
    DisconnectHandlers = 1,

    /// <summary>
    /// Clear common managed references, invoke <see cref="TearDown.OnTearDown"/>, then disconnect
    /// the element's handler. Destructive — opt in per page or globally.
    /// </summary>
    Compartmentalize = 2
}
