using System;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using ImgTagFanOut.ViewModels;

namespace ImgTagFanOut;

public class ViewLocator : IDataTemplate
{
    public Control Build(object? data)
    {
        string? name = data?.GetType().FullName?.Replace("ViewModel", "View");
        Type? type = name != null ? Type.GetType(name) : null;

        if (type != null)
        {
            return (Control)Activator.CreateInstance(type)!;
        }

        return new TextBlock { Text = "Not Found: " + name };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}
