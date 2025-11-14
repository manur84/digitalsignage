using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.ViewModels;

namespace DigitalSignage.Server.Views;

/// <summary>
/// UserControl for the Designer properties panel showing layout and element properties
/// </summary>
public partial class PropertiesPanel : UserControl
{
    private DesignerViewModel? _viewModel;

    public PropertiesPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Initial load
        UpdateTablePropertiesControl();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        // Unsubscribe from old ViewModel
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        // Subscribe to new ViewModel
        _viewModel = DataContext as DesignerViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        }

        UpdateTablePropertiesControl();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Update TablePropertiesControl when SelectedElement changes
        if (e.PropertyName == nameof(DesignerViewModel.SelectedElement))
        {
            UpdateTablePropertiesControl();
        }
    }

    private void UpdateTablePropertiesControl()
    {
        if (_viewModel?.SelectedElement != null &&
            _viewModel.SelectedElement.Type == "table" &&
            TablePropertiesControl != null)
        {
            TablePropertiesControl.LoadFromElement(_viewModel.SelectedElement);
        }
    }
}
