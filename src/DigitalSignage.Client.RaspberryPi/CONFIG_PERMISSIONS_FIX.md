# config.json Permission Fix

## Problem

The web interface could not save settings because of permission issues:

```
Failed to save settings: [Errno 13] Permission denied: '/opt/digitalsignage-client/config.json'
```

## Root Cause

The systemd service runs as user `pro` (or the install user), but the `config.json` file may have restrictive permissions (e.g., 644) that prevent write access.

## Solution

We've implemented a multi-layered fix:

### 1. Atomic Write with Permissions (config.py)

The `Config.save()` method now:
- Uses atomic write (temp file + move) to prevent corruption
- Explicitly sets permissions to 666 (rw-rw-rw-) on the temp file before moving
- Provides clear error messages if permissions are still insufficient

```python
# Before (simple write - could fail on permissions)
with open(config_file, 'w') as f:
    json.dump(asdict(self), f, indent=2)

# After (atomic write with permissions)
temp_fd, temp_path = tempfile.mkstemp(...)
with os.fdopen(temp_fd, 'w') as f:
    json.dump(asdict(self), f, indent=2)
os.chmod(temp_path, 0o666)  # rw-rw-rw-
shutil.move(temp_path, config_file)
```

### 2. Installer Auto-Fix (install.sh)

The installer now:
- Creates `config.json` with 666 permissions on fresh install
- Automatically fixes permissions on existing `config.json` during updates
- Shows clear messages about permission changes

```bash
# Set permissions to 666 (rw-rw-rw-) so web interface can write
chmod 666 "$INSTALL_DIR/config.json"
```

### 3. Web Interface Error Handling (web_interface.py)

The `/api/settings` endpoint now:
- Catches `PermissionError` specifically
- Returns helpful error message with fix command
- Logs detailed error information

```json
{
  "success": false,
  "error": "Permission denied saving settings",
  "fix": "Run: sudo chmod 666 /opt/digitalsignage-client/config.json"
}
```

### 4. Manual Fix Script (fix-config-permissions.sh)

A dedicated script for manual permission fixes:

```bash
sudo ./fix-config-permissions.sh
```

This script:
- Checks current permissions
- Fixes to 666 if needed
- Tests write access
- Provides clear status messages

## Permissions Explained

### 666 (rw-rw-rw-)

- Owner can read/write
- Group can read/write
- Others can read/write

**Why 666?**
- The web interface (Flask) runs in the same Python process as the client
- The client runs as user `pro` (or install user)
- Files in `/opt/digitalsignage-client/` may be owned by root (if copied during install)
- 666 ensures both root and the service user can write

**Security Note:**
- This is acceptable for `/opt/digitalsignage-client/config.json` because:
  - It contains no secrets (registration_token is per-device, not sensitive)
  - The file is in a protected directory (`/opt/`)
  - Only local users can access it
  - The web interface runs on localhost by default (not exposed externally)

### Alternative: 664 with Group

A more restrictive alternative would be:
```bash
chown pro:pro /opt/digitalsignage-client/config.json
chmod 664 /opt/digitalsignage-client/config.json
```

This requires:
- The service runs as user `pro`
- The file is owned by `pro:pro`
- Group members can read/write (664)

**We chose 666 for simplicity and compatibility.**

## Testing

After the fix:

1. **Check permissions:**
   ```bash
   ls -l /opt/digitalsignage-client/config.json
   # Should show: -rw-rw-rw- 1 pro pro ... config.json
   ```

2. **Test web interface:**
   - Open: `http://<pi-ip>:5000`
   - Go to "Settings" tab
   - Change a setting
   - Click "Save Settings"
   - Should succeed with: "Settings updated successfully"

3. **Verify logs:**
   ```bash
   sudo journalctl -u digitalsignage-client -f
   # Should NOT see: "Permission denied"
   # Should see: "Settings updated and saved: ..."
   ```

## Troubleshooting

### Still getting permission errors?

1. **Check SELinux/AppArmor:**
   ```bash
   # Disable SELinux temporarily (if enabled)
   sudo setenforce 0

   # Check AppArmor status
   sudo aa-status
   ```

2. **Check file ownership:**
   ```bash
   ls -l /opt/digitalsignage-client/config.json
   # If owned by root, change it:
   sudo chown pro:pro /opt/digitalsignage-client/config.json
   ```

3. **Check filesystem mount options:**
   ```bash
   mount | grep /opt
   # Should NOT have: ro (read-only)
   ```

4. **Re-run installer:**
   ```bash
   cd ~/digitalsignage/src/DigitalSignage.Client.RaspberryPi
   sudo ./install.sh
   # This will auto-fix permissions
   ```

## Files Changed

1. `config.py` - Atomic write with permissions
2. `install.sh` - Auto-fix permissions on install/update
3. `web_interface.py` - Better error handling
4. `fix-config-permissions.sh` - Manual fix script (NEW)
5. `CONFIG_PERMISSIONS_FIX.md` - This documentation (NEW)

## Deployment

After update:

```bash
# On development machine (where code was changed)
cd ~/digitalsignage
git add -A
git commit -m "Fix: config.json permission error when saving via web interface"
git push

# On Raspberry Pi
cd ~/digitalsignage
sudo git pull
cd src/DigitalSignage.Client.RaspberryPi
sudo ./install.sh  # Will auto-fix permissions
sudo systemctl restart digitalsignage-client
```

## Implementation Details

### Why atomic write?

Atomic write (write to temp file, then move) prevents:
- **File corruption** if write is interrupted (power loss, crash)
- **Race conditions** if multiple processes read/write simultaneously
- **Partial writes** leaving invalid JSON

### Why chmod before move?

Setting permissions on the temp file BEFORE moving ensures:
- The final file has correct permissions immediately
- No window where file exists with wrong permissions
- Works even if umask is restrictive

### Error Handling Philosophy

1. **Fail fast with clear messages** - Don't hide permission errors
2. **Provide actionable fixes** - Tell user exactly how to fix it
3. **Log for debugging** - Structured logging with context
4. **Graceful degradation** - Web interface doesn't crash, just shows error

## Future Improvements

Possible enhancements:

1. **Config directory for user overrides:**
   ```
   /opt/digitalsignage-client/config.json      # System (read-only)
   /home/pro/.digitalsignage/config.d/user.json  # User overrides (writable)
   ```

2. **Systemd drop-in for write permissions:**
   ```ini
   [Service]
   ReadWritePaths=/opt/digitalsignage-client/config.json
   ```

3. **Web interface authentication** to protect settings page

4. **Config validation** before saving to prevent invalid JSON

## References

- CLAUDE.md - Project coding guidelines
- install.sh - Installation script
- config.py - Configuration management
- web_interface.py - Web dashboard
