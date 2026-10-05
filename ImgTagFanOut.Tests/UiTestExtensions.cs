using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Threading;
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

    /// <summary>
    /// Finds the button whose <see cref="Button.Command"/> is the given command. Used for
    /// buttons that carry no <c>Name</c> in the XAML.
    /// </summary>
    public static Button? FindButtonForCommand(this Control root, ICommand? command)
    {
        return root.GetVisualDescendants().OfType<Button>().FirstOrDefault(b => ReferenceEquals(b.Command, command));
    }

    /// <summary>
    /// Shows the window, pumps the dispatcher and returns a predicate telling whether it is
    /// still open. Windows driven by <c>WhenActivated</c> only react once they are attached
    /// to the visual tree, so they must be shown before their commands are exercised.
    /// </summary>
    public static Func<bool> TrackOpen(this Window window)
    {
        window.Show();
        Dispatcher.UIThread.RunJobs();
        bool closed = false;
        window.Closed += (_, _) => closed = true;
        return () => !closed;
    }
}