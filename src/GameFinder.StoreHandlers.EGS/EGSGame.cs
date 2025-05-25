using GameFinder.Common;
using JetBrains.Annotations;
using NexusMods.Paths;

namespace GameFinder.StoreHandlers.EGS;

/// <summary>
/// Represents a game installed with the Epic Games Store.
/// </summary>
/// <param name="CatalogItemId"></param>
/// <param name="AppName"></param>
/// <param name="DisplayName"></param>
/// <param name="InstallLocation"></param>
/// <param name="IsApplication"></param>
[PublicAPI]
public record EGSGame(
    EGSGameId CatalogItemId,
    string AppName,
    string DisplayName,
    AbsolutePath InstallLocation,
    bool IsApplication) : IGame;
