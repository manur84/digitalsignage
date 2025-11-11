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
        // VERY FIRST THING: Check if we're in setup mode
        if (args.Contains("--setup-urlacl"))
        {
            // We're running as admin to configure URL ACL
            HandleUrlAclSetup(args);
            return; // Exit after setup
        }

        // SECOND: Check if URL ACL is configured
        const int defaultPort = 8080;
        if (!UrlAclManager.IsUrlAclConfigured(defaultPort))
        {
            // URL ACL not configured

            if (UrlAclManager.IsRunningAsAdministrator())
            {
                // We're admin but URL ACL still not configured
                // Configure it now
                Console.WriteLine("Running as administrator, configuring URL ACL...");
                if (UrlAclManager.ConfigureUrlAcl(defaultPort))
                {
                    MessageBox.Show(
                        "URL ACL wurde erfolgreich konfiguriert!\n\n" +
                        "Die Anwendung startet jetzt normal neu.",
                        "Konfiguration erfolgreich",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Restart without admin
                    RestartNormally();
                    return;
                }
                else
                {
                    MessageBox.Show(
                        "URL ACL Konfiguration fehlgeschlagen.\n\n" +
                        "Bitte führen Sie setup-urlacl.bat manuell aus.",
                        "Fehler",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                    return;
                }
            }
            else
            {
                // Not admin and URL ACL not configured
                // Show message and request elevation
                var result = MessageBox.Show(
                    "ERSTMALIGE EINRICHTUNG ERFORDERLICH\n\n" +
                    "Für den ersten Start benötigt die Digital Signage Server App\n" +
                    "eine einmalige Windows-Konfiguration (URL ACL für Port 8080).\n\n" +
                    "Die App wird jetzt mit Administrator-Rechten neu gestartet,\n" +
                    "um diese Konfiguration vorzunehmen.\n\n" +
                    "Sie müssen dies nur einmal bestätigen!\n\n" +
                    "Jetzt mit Administrator-Rechten starten?",
                    "Administrator-Rechte erforderlich",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    // Restart as admin
                    if (UrlAclManager.RestartAsAdministrator($"--setup-urlacl {defaultPort}"))
                    {
                        // Success - this instance will exit
                        return;
                    }
                    else
                    {
                        MessageBox.Show(
                            "Fehler beim Neustart mit Administrator-Rechten.\n\n" +
                            "Bitte führen Sie setup-urlacl.bat manuell als Administrator aus.",
                            "Fehler",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    MessageBox.Show(
                        "URL ACL wurde nicht konfiguriert.\n\n" +
                        "Die App kann nur im localhost-Modus laufen.\n" +
                        "Raspberry Pi Clients können sich nicht verbinden.\n\n" +
                        "Für volle Funktionalität führen Sie bitte\n" +
                        "setup-urlacl.bat als Administrator aus.",
                        "Eingeschränkter Modus",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);

                    // Continue in localhost-only mode
                }
            }
        }

        // URL ACL is configured (or user chose to continue without it)
        // Start the WPF application normally
        var app = new App();
        app.InitializeComponent();
        app.Run();
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
                MessageBox.Show(
                    "URL ACL wurde erfolgreich konfiguriert!\n\n" +
                    "Die Anwendung startet jetzt automatisch normal neu.\n\n" +
                    $"Log wurde gespeichert in: urlacl-setup.log",
                    "Setup erfolgreich",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Restart normally
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
}
