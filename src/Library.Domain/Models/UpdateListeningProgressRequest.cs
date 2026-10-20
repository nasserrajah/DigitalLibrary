namespace Library.Domain.Models;

public sealed record UpdateListeningProgressRequest(
    int LastSegmentIndex,
    double LastPositionSeconds
);