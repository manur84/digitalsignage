using System;
using System.Diagnostics;
using Serilog;

namespace DigitalSignage.Server.Helpers;

/// <summary>
/// Manages Windows Firewall rules for Digital Signage Server.
/// Automatically configures required firewall rules for discovery and communication.
/// </summary>
public static class FirewallManager
{
    private const string RulePrefix = "Digital Signage -";

    /// <summary>
    /// Checks if all required firewall rules are configured
    /// </summary>
    public static bool AreFirewallRulesConfigured(int port = 8080)
    {
        try
        {
            var rules = new[]
            {
                $"{RulePrefix} UDP Discovery",
                $"{RulePrefix} mDNS",
                $"{RulePrefix} WebSocket"
            };

            foreach (var ruleName in rules)
            {
                if (!IsFirewallRuleExists(ruleName))
                {
                    Log.Debug($"Firewall rule not found: {ruleName}");
                    return false;
                }
            }

            Log.Debug("All firewall rules are configured");
            return true;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to check firewall rules");
            return false;
        }
    }

    /// <summary>
    /// Checks if a specific firewall rule exists
    /// </summary>
    private static bool IsFirewallRuleExists(string ruleName)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall show rule name=\"{ruleName}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return false;

            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            // If rule exists, netsh returns the rule details
            // If not exists, it returns "No rules match the specified criteria"
            return process.ExitCode == 0 &&
                   !output.Contains("No rules match", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Error checking firewall rule: {ruleName}");
            return false;
        }
    }

    /// <summary>
    /// Configures all required firewall rules (requires admin privileges)
    /// </summary>
    public static bool ConfigureFirewallRules(int port = 8080)
    {
        if (!UrlAclManager.IsRunningAsAdministrator())
        {
            Log.Error("Cannot configure firewall rules - not running as administrator");
            return false;
        }

        Console.WriteLine("====================================");
        Console.WriteLine("Windows Firewall Configuration");
        Console.WriteLine("====================================");
        Console.WriteLine($"WebSocket Port: {port}");
        Console.WriteLine($"UDP Discovery Port: 5555");
        Console.WriteLine($"mDNS Port: 5353");
        Console.WriteLine("====================================");
        Console.WriteLine();

        try
        {
            var rulesConfigured = 0;
            var rulesFailed = 0;

            // Rule 1: UDP Discovery (port 5555)
            Console.WriteLine("[1/3] Configuring UDP Discovery rule (port 5555)...");
            if (ConfigureFirewallRule(
                name: $"{RulePrefix} UDP Discovery",
                protocol: "UDP",
                port: 5555,
                direction: "in",
                action: "allow",
                description: "Allows UDP broadcast discovery for Digital Signage clients"))
            {
                Console.WriteLine("✓ UDP Discovery rule configured");
                rulesConfigured++;
            }
            else
            {
                Console.WriteLine("✗ UDP Discovery rule failed");
                rulesFailed++;
            }

            // Rule 2: mDNS (port 5353)
            Console.WriteLine("[2/3] Configuring mDNS rule (port 5353)...");
            if (ConfigureFirewallRule(
                name: $"{RulePrefix} mDNS",
                protocol: "UDP",
                port: 5353,
                direction: "in",
                action: "allow",
                description: "Allows mDNS/Zeroconf service discovery for Digital Signage"))
            {
                Console.WriteLine("✓ mDNS rule configured");
                rulesConfigured++;
            }
            else
            {
                Console.WriteLine("✗ mDNS rule failed");
                rulesFailed++;
            }

            // Rule 3: WebSocket (port from config, default 8080)
            Console.WriteLine($"[3/3] Configuring WebSocket rule (port {port})...");
            if (ConfigureFirewallRule(
                name: $"{RulePrefix} WebSocket",
                protocol: "TCP",
                port: port,
                direction: "in",
                action: "allow",
                description: "Allows WebSocket connections for Digital Signage communication"))
            {
                Console.WriteLine("✓ WebSocket rule configured");
                rulesConfigured++;
            }
            else
            {
                Console.WriteLine("✗ WebSocket rule failed");
                rulesFailed++;
            }

            Console.WriteLine();
            Console.WriteLine("====================================");
            Console.WriteLine($"Firewall Configuration Complete");
            Console.WriteLine($"✓ {rulesConfigured} rules configured");
            if (rulesFailed > 0)
            {
                Console.WriteLine($"✗ {rulesFailed} rules failed");
            }
            Console.WriteLine("====================================");

            Log.Information($"Firewall configuration complete: {rulesConfigured} rules configured, {rulesFailed} failed");
            return rulesFailed == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"\n[FATAL ERROR] {ex.Message}");
            Log.Error(ex, "Fatal error during firewall configuration");
            return false;
        }
    }

    /// <summary>
    /// Configures a single firewall rule
    /// </summary>
    private static bool ConfigureFirewallRule(
        string name,
        string protocol,
        int port,
        string direction,
        string action,
        string description)
    {
        try
        {
            // Delete existing rule if present
            DeleteFirewallRule(name);

            // Add new rule
            var arguments = $"advfirewall firewall add rule " +
                          $"name=\"{name}\" " +
                          $"protocol={protocol} " +
                          $"localport={port} " +
                          $"dir={direction} " +
                          $"action={action} " +
                          $"description=\"{description}\"";

            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null)
            {
                Log.Error($"Failed to start netsh process for rule: {name}");
                return false;
            }

            var output = process.StandardOutput.ReadToEnd();
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode == 0)
            {
                Log.Information($"Firewall rule configured: {name}");
                return true;
            }
            else
            {
                Log.Error($"Failed to configure firewall rule: {name}. Exit code: {process.ExitCode}, Error: {error}");
                return false;
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, $"Exception configuring firewall rule: {name}");
            return false;
        }
    }

    /// <summary>
    /// Deletes a firewall rule if it exists
    /// </summary>
    private static void DeleteFirewallRule(string name)
    {
        try
        {
            if (!IsFirewallRuleExists(name))
            {
                return; // Rule doesn't exist, nothing to delete
            }

            var psi = new ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = $"advfirewall firewall delete rule name=\"{name}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            process?.WaitForExit();
        }
        catch (Exception ex)
        {
            Log.Debug(ex, $"Error deleting firewall rule: {name}");
        }
    }

    /// <summary>
    /// Removes all Digital Signage firewall rules
    /// </summary>
    public static bool RemoveFirewallRules()
    {
        if (!UrlAclManager.IsRunningAsAdministrator())
        {
            Log.Error("Cannot remove firewall rules - not running as administrator");
            return false;
        }

        Console.WriteLine("Removing Digital Signage firewall rules...");

        var rules = new[]
        {
            $"{RulePrefix} UDP Discovery",
            $"{RulePrefix} mDNS",
            $"{RulePrefix} WebSocket"
        };

        foreach (var rule in rules)
        {
            DeleteFirewallRule(rule);
            Console.WriteLine($"✓ Removed: {rule}");
        }

        Log.Information("All Digital Signage firewall rules removed");
        return true;
    }
}
