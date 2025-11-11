using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using DigitalSignage.Server.Services;
using Serilog;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

public partial class DataSourceViewModel : ObservableObject
{
    private readonly IDataService _dataService;
    private readonly DataSourceRepository _repository;
    private readonly ILogger _logger;

    [ObservableProperty]
    private DataSource? _selectedDataSource;

    [ObservableProperty]
    private string _testResult = string.Empty;

    [ObservableProperty]
    private bool _isTesting = false;

    [ObservableProperty]
    private bool _isStaticDataSource = false;

    [ObservableProperty]
    private string _staticDataJson = string.Empty;

    public ObservableCollection<DataSource> DataSources { get; } = new();

    public DataSourceViewModel(IDataService dataService, DataSourceRepository repository)
    {
        _dataService = dataService;
        _repository = repository;
        _logger = Log.ForContext<DataSourceViewModel>();

        // Load data sources from database
        _ = LoadDataSourcesAsync();
    }

    partial void OnSelectedDataSourceChanged(DataSource? value)
    {
        if (value != null)
        {
            IsStaticDataSource = value.Type == DataSourceType.StaticData;
            StaticDataJson = value.StaticData ?? string.Empty;
        }
        else
        {
            IsStaticDataSource = false;
            StaticDataJson = string.Empty;
        }
    }

    partial void OnIsStaticDataSourceChanged(bool value)
    {
        if (SelectedDataSource != null)
        {
            SelectedDataSource.Type = value ? DataSourceType.StaticData : DataSourceType.SQL;
        }
    }

    partial void OnStaticDataJsonChanged(string value)
    {
        if (SelectedDataSource != null)
        {
            SelectedDataSource.StaticData = value;
        }
    }

    [RelayCommand]
    private void AddDataSource()
    {
        var newDataSource = new DataSource
        {
            Name = "New Data Source",
            Type = DataSourceType.SQL
        };

        DataSources.Add(newDataSource);
        SelectedDataSource = newDataSource;
    }

    [RelayCommand]
    private void AddStaticDataSource()
    {
        var newDataSource = new DataSource
        {
            Name = "New Static Data Source",
            Type = DataSourceType.StaticData,
            StaticData = @"{
  ""room_name"": ""Conference Room A"",
  ""status"": ""Available"",
  ""temperature"": ""22°C""
}"
        };

        DataSources.Add(newDataSource);
        SelectedDataSource = newDataSource;
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        if (SelectedDataSource == null)
        {
            TestResult = "No data source selected";
            return;
        }

        IsTesting = true;
        TestResult = "Testing connection...";

        try
        {
            var success = await _dataService.TestConnectionAsync(SelectedDataSource);
            TestResult = success ? "Connection successful!" : "Connection failed!";
        }
        catch (Exception ex)
        {
            TestResult = $"Error: {ex.Message}";
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    private async Task LoadDataSources()
    {
        await LoadDataSourcesAsync();
    }

    private async Task LoadDataSourcesAsync()
    {
        try
        {
            var dataSources = await _repository.GetAllAsync();

            DataSources.Clear();
            foreach (var ds in dataSources)
            {
                DataSources.Add(ds);
            }

            _logger.Information("Loaded {Count} data sources from database", dataSources.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to load data sources from database");
            TestResult = $"Error loading data sources: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveDataSource()
    {
        if (SelectedDataSource == null) return;

        try
        {
            // Check if it's a new data source (empty/null Id) or existing
            if (string.IsNullOrEmpty(SelectedDataSource.Id) || await _repository.GetByIdAsync(SelectedDataSource.Id) == null)
            {
                // New data source
                await _repository.AddAsync(SelectedDataSource);
                TestResult = "Data source added successfully!";
            }
            else
            {
                // Update existing
                await _repository.UpdateAsync(SelectedDataSource);
                TestResult = "Data source updated successfully!";
            }

            // Reload from database to ensure consistency
            await LoadDataSourcesAsync();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save data source");
            TestResult = $"Error saving: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteDataSource()
    {
        if (SelectedDataSource == null) return;

        try
        {
            if (!string.IsNullOrEmpty(SelectedDataSource.Id))
            {
                await _repository.DeleteAsync(SelectedDataSource.Id);
            }

            DataSources.Remove(SelectedDataSource);
            SelectedDataSource = null;
            TestResult = "Data source deleted successfully!";
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to delete data source");
            TestResult = $"Error deleting: {ex.Message}";
        }
    }
}
