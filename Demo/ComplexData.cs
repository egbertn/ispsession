//more complex objects
public class ServiceTimeDto
{
    public int Id { get; init; }
    public decimal Price { get; set; }
    public TimeSpan Duration { get; set; }

    public TimeSpan TreatmentTime { get; set; }
}
public class ServiceDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int NumberOfBeds { get; set; }
    public string ImageUri { get; init; } = null!;
    public Dictionary<string, string> Properties = null!;
    public ICollection<ServiceTimeDto> ServiceTimes { get; set; } = null!;
}

public class FeatureDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public decimal Price { get; set; }
    public TimeSpan RequiredTime { get; set; }
}
