namespace SpinTheBottle.Models;

public class ContactItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public bool IsSelected { get; set; } = false;
    public string AvatarColor { get; set; } = "#3b82f6";
}
