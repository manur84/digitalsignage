using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DigitalSignage.Core.Interfaces;
using DigitalSignage.Core.Models;
using System.Collections.ObjectModel;

namespace DigitalSignage.Server.ViewModels;

public partial class DataSourceViewModel : ObservableObject
{
    private readonly IDataService _dataService;

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

    public DataSourceViewModel(IDataService dataService)
    {
        _dataService = dataService;
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
    private void SaveDataSource()
    {
        if (SelectedDataSource == null) return;

        // TODO: Persist to database
        TestResult = "Data source saved";
    }

    [RelayCommand]
    private void DeleteDataSource()
    {
        if (SelectedDataSource == null) return;

        DataSources.Remove(SelectedDataSource);
        SelectedDataSource = null;
    }
}
