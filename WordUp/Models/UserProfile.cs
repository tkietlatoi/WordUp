using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WordUp.Models;

public sealed class UserProfile : INotifyPropertyChanged
{
    private string fullName = "";
    private string email = "";
    private string phone = "";
    private string level = "";
    private string note = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    public string FullName
    {
        get => fullName;
        set
        {
            if (SetProperty(ref fullName, value))
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Initials)));
            }
        }
    }

    public string Email
    {
        get => email;
        set => SetProperty(ref email, value);
    }

    public string Phone
    {
        get => phone;
        set => SetProperty(ref phone, value);
    }

    public string Level
    {
        get => level;
        set => SetProperty(ref level, value);
    }

    public string Note
    {
        get => note;
        set => SetProperty(ref note, value);
    }

    public string Initials
    {
        get
        {
            var parts = FullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return "WU";
            }

            return string.Concat(parts.Take(2).Select(part => char.ToUpperInvariant(part[0])));
        }
    }

    private bool SetProperty(ref string storage, string value, [CallerMemberName] string? propertyName = null)
    {
        if (storage == value)
        {
            return false;
        }

        storage = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
