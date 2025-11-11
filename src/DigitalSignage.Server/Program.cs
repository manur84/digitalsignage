using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DigitalSignage.Server.Helpers;

namespace DigitalSignage.Server;

/// <summary>
/// Program entry point - handles URL ACL check BEFORE any WPF initialization
/// </summary>
public class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        const int defaultPort = 8080;

        // VERY FIRST THING: Check if we're in setup mode
        if (args.Contains("--setup-urlacl"))
        {
            // We're running as admin to configure URL ACL
            HandleUrlAclSetup(args);
            return; // Exit after setup
        }

        // SECOND: Check if setup is already completed
        if (IsSetupCompleted())
        {
            // Setup was done before, start app normally
            StartApplication();
            return;
        }

        // THIRD: Check if URL ACL is configured
        if (!UrlAclManager.IsUrlAclConfigured(defaultPort))
        {
            // URL ACL not configured

            if (UrlAclManager.IsRunningAsAdministrator())
            {
                // We're admin but URL ACL still not configured
                // Configure it now SILENTLY
                Console.WriteLine("Running as administrator, configuring URL ACL automatically...");
                if (UrlAclManager.ConfigureUrlAcl(defaultPort))
                {
                    // Mark setup as completed
                    MarkSetupCompleted();

                    // Restart without admin - NO MESSAGE BOX, fully automatic
                    RestartNormally();
                    return;
                }
                else
                {
                    // Only show message if setup FAILED
                    MessageBox.Show(
                        "URL ACL Konfiguration fehlgeschlagen.\n\n" +
                        "Bitte führen Sie setup-urlacl.bat manuell aus.\n\n" +
                        "Siehe urlacl-setup.log für Details.",
                        "Fehler",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                // Not admin and URL ACL not configured
                // DIRECTLY trigger elevation WITHOUT showing message box first
                try
                {
                    if (UrlAclManager.RestartAsAdministrator($"--setup-urlacl {defaultPort}"))
                    {
                        // Success - this instance will exit
                        // The UAC prompt will be shown by Windows automatically
                        return;
                    }
                    else
                    {
                        // Failed to elevate - show error
                        MessageBox.Show(
                            "Fehler beim Neustart mit Administrator-Rechten.\n\n" +
                            "Für den ersten Start benötigt die Anwendung eine\n" +
                            "einmalige Windows-Konfiguration (URL ACL).\n\n" +
                            "Alternativ führen Sie setup-urlacl.bat als Administrator aus.",
                            "Administrator-Rechte erforderlich",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    // User cancelled UAC prompt
                    var retry = MessageBox.Show(
                        "Administrator-Rechte sind für den ersten Start erforderlich.\n\n" +
                        "Klicken Sie auf 'Ja' für einen erneuten Versuch,\n" +
                        "oder 'Nein' um im eingeschränkten Modus fortzufahren\n" +
                        "(Raspberry Pi Clients können sich nicht verbinden).",
                        "Setup abgebrochen",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

                    if (retry == MessageBoxResult.Yes)
                    {
                        UrlAclManager.RestartAsAdministrator($"--setup-urlacl {defaultPort}");
                        return;
                    }

                    // Continue in localhost-only mode
                }
            }
        }
        else
        {
            // URL ACL is configured, mark setup as completed
            MarkSetupCompleted();
        }

        // Start the WPF application normally
        StartApplication();
    }

    private static void HandleUrlAclSetup(string[] args)
    {
        // Redirect console to a string writer so we can capture all output
        var originalOut = Console.Out;
        var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);

        try
        {
            // Extract port from args
            var portArg = args.FirstOrDefault(a => int.TryParse(a, out _));
            var port = portArg != null ? int.Parse(portArg) : 8080;

            var success = UrlAclManager.ConfigureUrlAcl(port);

            // Restore console
            Console.SetOut(originalOut);
            var log = stringWriter.ToString();

            // Always write log to file for debugging
            try
            {
                var logPath = Path.Combine(Environment.CurrentDirectory, "urlacl-setup.log");
                File.WriteAllText(logPath, log);
                Console.WriteLine($"Log written to: {logPath}");
            }
            catch
            {
                // Ignore file write errors
            }

            if (success)
            {
                // Mark setup as completed
                MarkSetupCompleted();

                // Restart normally WITHOUT showing message box
                // Fully automatic restart
                RestartNormally();
            }
            else
            {
                // Show detailed error window with log
                ShowDetailedErrorWindow(log, port);
            }
        }
        catch (Exception ex)
        {
            // Restore console
            Console.SetOut(originalOut);
            var log = stringWriter.ToString();

            MessageBox.Show(
                $"Kritischer Fehler während der URL ACL Konfiguration:\n\n" +
                $"{ex.Message}\n\n" +
                $"Exception Type: {ex.GetType().Name}\n\n" +
                $"Bitte führen Sie setup-urlacl.bat manuell aus.\n\n" +
                $"Detailliertes Log:\n{log}",
                "Kritischer Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void ShowDetailedErrorWindow(string log, int port)
    {
        var errorWindow = new Window
        {
            Title = "URL ACL Setup Fehler - Detailliertes Log",
            Width = 800,
            Height = 600,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Icon = null
        };

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // Header
        var header = new TextBlock
        {
            Text = "URL ACL Konfiguration fehlgeschlagen!",
            FontSize = 16,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Colors.Red),
            Margin = new Thickness(10),
            TextWrapping = TextWrapping.Wrap
        };
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        // Log text box
        var textBox = new TextBox
        {
            Text = "DETAILLIERTES LOG:\n" +
                   "==================\n\n" +
                   log + "\n\n" +
                   "LÖSUNGEN:\n" +
                   "=========\n\n" +
                   "OPTION 1: Manuelle Batch-Datei (EMPFOHLEN)\n" +
                   "-------------------------------------------\n" +
                   "1. Schließen Sie dieses Fenster\n" +
                   "2. Öffnen Sie den Ordner der Anwendung\n" +
                   "3. Rechtsklick auf 'setup-urlacl.bat'\n" +
                   "4. Wählen Sie 'Als Administrator ausführen'\n\n" +
                   "OPTION 2: Manueller Befehl in PowerShell\n" +
                   "-----------------------------------------\n" +
                   "1. Öffnen Sie PowerShell als Administrator\n" +
                   "2. Führen Sie aus:\n" +
                   $"   netsh http add urlacl url=http://+:{port}/ws/ user=Everyone\n" +
                   $"   netsh http add urlacl url=http://+:{port}/ user=Everyone\n\n" +
                   "OPTION 3: HTTP.sys Service prüfen\n" +
                   "----------------------------------\n" +
                   "1. Öffnen Sie PowerShell als Administrator\n" +
                   "2. Prüfen Sie den Service: sc query HTTP\n" +
                   "3. Falls gestoppt, starten Sie: net start HTTP\n\n" +
                   "OPTION 4: Windows neu starten\n" +
                   "------------------------------\n" +
                   "Manchmal hilft ein Neustart von Windows, dann\n" +
                   "die Anwendung erneut als Administrator starten.\n\n" +
                   "SUPPORT:\n" +
                   "--------\n" +
                   "Falls nichts funktioniert, senden Sie bitte die\n" +
                   "Datei 'urlacl-setup.log' an den Support.",
            IsReadOnly = true,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
            FontFamily = new FontFamily("Consolas"),
            FontSize = 12,
            Padding = new Thickness(10),
            Margin = new Thickness(10, 0, 10, 10)
        };
        Grid.SetRow(textBox, 1);
        grid.Children.Add(textBox);

        // Button panel
        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(10)
        };

        var copyButton = new Button
        {
            Content = "Log kopieren",
            Width = 120,
            Height = 30,
            Margin = new Thickness(5)
        };
        copyButton.Click += (s, e) =>
        {
            Clipboard.SetText(textBox.Text);
            MessageBox.Show("Log wurde in die Zwischenablage kopiert!", "Kopiert", MessageBoxButton.OK, MessageBoxImage.Information);
        };

        var closeButton = new Button
        {
            Content = "Schließen",
            Width = 120,
            Height = 30,
            Margin = new Thickness(5)
        };
        closeButton.Click += (s, e) => errorWindow.Close();

        buttonPanel.Children.Add(copyButton);
        buttonPanel.Children.Add(closeButton);

        Grid.SetRow(buttonPanel, 2);
        grid.Children.Add(buttonPanel);

        errorWindow.Content = grid;
        errorWindow.ShowDialog();
    }

    private static void RestartNormally()
    {
        try
        {
            var exePath = Environment.ProcessPath ??
                         Process.GetCurrentProcess().MainModule?.FileName;

            if (!string.IsNullOrEmpty(exePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Environment.CurrentDirectory
                });
            }
        }
        catch
        {
            // Ignore errors on restart
        }
    }

    /// <summary>
    /// Gets the path to the setup completion marker file
    /// </summary>
    private static string GetSetupMarkerPath()
    {
        var appDataPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DigitalSignage.Server");

        Directory.CreateDirectory(appDataPath); // Ensure directory exists
        return Path.Combine(appDataPath, ".setup-completed");
    }

    /// <summary>
    /// Checks if the initial setup has been completed
    /// </summary>
    private static bool IsSetupCompleted()
    {
        try
        {
            return File.Exists(GetSetupMarkerPath());
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Marks the initial setup as completed
    /// </summary>
    private static void MarkSetupCompleted()
    {
        try
        {
            var markerPath = GetSetupMarkerPath();
            File.WriteAllText(markerPath, $"Setup completed at: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"Setup marker created: {markerPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not create setup marker: {ex.Message}");
            // Not critical - app will recheck URL ACL on next start
        }
    }

    /// <summary>
    /// Starts the WPF application
    /// </summary>
    private static void StartApplication()
    {
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
