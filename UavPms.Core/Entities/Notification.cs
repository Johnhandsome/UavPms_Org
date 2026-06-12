namespace UavPms.Core.Entities;

public class Notification{
    public Guid Id {get;set;}

    public string UserId {get;set;} = string.Empty;
    public string Title {get;set;} = string.Empty;
    public string Content {get;set;} = string.Empty;
    public DateTime CreatedAt {get;set;} = DateTime.UtcNow;
    public bool IsRead {get;set;} = false;
    public string Type {get;set;} = string.Empty;
}