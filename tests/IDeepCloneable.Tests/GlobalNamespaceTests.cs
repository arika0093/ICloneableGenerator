using IDeepCloneable;

internal partial class SampleClass : IDeepCloneable<SampleClass>
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}