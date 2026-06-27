namespace Balance.Services.Contracts;

/// <summary>
/// A single dropped statement file for detection: its original <paramref name="FileName"/> (an
/// accelerator for resolving the account anchor when the bank embeds it in the name) and a
/// seekable <paramref name="Content"/> stream. Probes and the eventual import each seek the
/// stream back to the start, so callers must pass a stream that supports seeking (ADR 0034).
/// </summary>
public sealed record ImportFile(string FileName, Stream Content);
