using System;
using System.Collections.Generic;

namespace ImgTagFanOut.ViewModels;

public class Tag : IEquatable<Tag>
{
    public string Name { get; }

    public Tag(string name)
    {
        Name = name;
    }

    public bool Equals(Tag? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((Tag)obj);
    }

    private sealed class NameEqualityComparer : IEqualityComparer<Tag>
    {
        public bool Equals(Tag x, Tag y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Equals(y);
        }

        public int GetHashCode(Tag obj)
        {
            return obj.GetHashCode();
        }
    }

    public static IEqualityComparer<Tag> Comparer { get; } = new NameEqualityComparer();

    public override int GetHashCode()
    {
        return StringComparer.OrdinalIgnoreCase.GetHashCode(Name);
    }

    public bool Same(string? s)
    {
        return s != null && string.Equals(Name, s, StringComparison.OrdinalIgnoreCase);
    }

    public bool MatchFilter(string? tagFilterInput)
    {
        return tagFilterInput != null && Name.Contains(tagFilterInput, StringComparison.OrdinalIgnoreCase);
    }
}