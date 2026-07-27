using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using GitDeck.App.Views.Settings;
using GitDeck.App.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace GitDeck.App;


public class ViewLocator : IDataTemplate
{
    private readonly IServiceProvider _services;

    public ViewLocator(IServiceProvider services)
    {
        _services = services;
    }

    public Control Build(object? param) 
    {
        var view = param switch
        {
            SettingsViewModel => _services.GetRequiredService<SettingsView>(),
            _ => throw new NotImplementedException($"No view registered for {param?.GetType().FullName ?? "null"}")
        };
    
        view.DataContext = param;

        return view;
    }

    public bool Match(object? data)
    {
        return data is ObservableObject;
    }
}
