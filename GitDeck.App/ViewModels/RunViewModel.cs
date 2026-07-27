using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace GitDeck.App.ViewModels;

public sealed record RunResult(string Title, string Subtitle, string Icon);

public partial class RunViewModel : ObservableObject
{
    private static readonly RunResult[] AllResults =
    [
        new("Visual Studio Code", "Application", "🖥️"),
        new("Notepad", "Application", "📝"),
        new("Calculator", "Application", "🧮"),
        new("Settings", "Application", "⚙️"),
        new("File Explorer", "Application", "📁"),
        new("Windows Terminal", "Application", "⌨️"),
        new("Task Manager", "Application", "📊"),
        new("Paint", "Application", "🎨"),
        new("Spotify", "Application", "🎵"),
        new("Microsoft Edge", "Application", "🌐"),
        new("Snipping Tool", "Application", "✂️"),
        new("Control Panel", "Application", "🛠️"),
        new("GitDeck Settings", "Application", "🗂️"),
    ];

    [ObservableProperty]
    public partial string SearchText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ObservableCollection<RunResult> Results { get; set; } = [];

    [ObservableProperty]
    public partial bool HasResults { get; set; }

    [ObservableProperty]
    public partial int SelectedIndex { get; set; } = -1;

    public void Reset()
    {
        SearchText = string.Empty;
    }

    partial void OnSearchTextChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Results = [];
            HasResults = false;
            SelectedIndex = -1;
            return;
        }

        var matches = AllResults
            .Where(result => result.Title.Contains(value, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();

        Results = new ObservableCollection<RunResult>(matches);
        HasResults = Results.Count > 0;
        SelectedIndex = HasResults ? 0 : -1;
    }
}
