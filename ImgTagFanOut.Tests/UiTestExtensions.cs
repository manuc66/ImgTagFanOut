using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace ImgTagFanOut.Tests;

public static class UiTestExtensions
{
    public static T? FindByAutomationId<T>(this Control root, string automationId) where T : Control
    {
        foreach (Control descendant in root.GetVisualDescendants().OfType<Control>())
        {
            if (descendant is T && AutomationProperties.GetAutomationId(descendant) == automationId)
            {
                return (T)descendant;
            }
        }

        return null;
    }
}