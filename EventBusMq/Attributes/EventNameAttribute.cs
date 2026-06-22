namespace EventBusMq.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class EventNameAttribute : Attribute
{
    public string Name { get; init; }

    public EventNameAttribute(string name)
    {
        Name = name;
    }
    
}
