using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
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
        try
        {
            // Extract port from args
            var portArg = args.FirstOrDefault(a => int.TryParse(a, out _));
            var port = portArg != null ? int.Parse(portArg) : 8080;

            Console.WriteLine($"Configuring URL ACL for port {port}...");

            if (UrlAclManager.ConfigureUrlAcl(port))
            {
                MessageBox.Show(
                    "URL ACL wurde erfolgreich konfiguriert!\n\n" +
                    "Die Anwendung startet jetzt automatisch normal neu.",
                    "Setup erfolgreich",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                // Restart normally
                RestartNormally();
            }
            else
            {
                MessageBox.Show(
                    "URL ACL Konfiguration fehlgeschlagen.\n\n" +
                    "Bitte führen Sie setup-urlacl.bat manuell aus.\n\n" +
                    "Oder wenden Sie sich an den Support.",
                    "Setup fehlgeschlagen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Fehler während der URL ACL Konfiguration:\n\n{ex.Message}\n\n" +
                $"Bitte führen Sie setup-urlacl.bat manuell aus.",
                "Fehler",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
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
