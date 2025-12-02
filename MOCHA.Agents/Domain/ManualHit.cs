namespace MOCHA.Agents.Domain;

/// <summary>
/// マニュアル検索のヒット
/// </summary>
public sealed record ManualHit(string Title, string RelativePath, double Score);
