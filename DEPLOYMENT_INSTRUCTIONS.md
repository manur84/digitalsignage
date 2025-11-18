# Digital Signage Client - Cache Manager Schema Migration Fix

## Problem

Raspberry Pi clients installed with older versions of the client software are encountering this error:

```
table layouts has no column named expires_at
```

This occurs when the client tries to save layouts to the cache database using the updated schema that includes the `expires_at` column for TTL (Time-To-Live) functionality.

## Solution

The `cache_manager.py` file has been updated with automatic schema migration code that:

1. Checks if the `expires_at` column exists in the `layouts` table
2. If missing, adds it using `ALTER TABLE` 
3. Does the same for the `layout_data` table
4. Logs the migration process

The migration runs automatically when the `CacheManager` class is initialized, so existing databases will be upgraded seamlessly.

## Deployment Steps

### Option 1: Automatic Deployment via Remote Installer (Recommended)

1. **Copy the updated Python files to the ClientInstaller directory**:
   
   From the repository root, run:
   ```powershell
   # Copy updated Python client files to the server's ClientInstaller directory
   $clientFiles = @(
       "cache_manager.py",
       "client.py",
       "config.py",
       "config_txt_manager.py",
       "discovery.py",
       "device_manager.py",
       "display_renderer.py",
       "watchdog_monitor.py",
       "status_screen.py",
       "web_interface.py",
       "burn_in_protection.py",
       "start-with-display.sh",
       "install.sh"
   )
   
   foreach ($file in $clientFiles) {
       Copy-Item "src\DigitalSignage.Client.RaspberryPi\$file" "src\DigitalSignage.Server\ClientInstaller\" -Force
   }
   
   # Copy templates directory for web interface
   Copy-Item "src\DigitalSignage.Client.RaspberryPi\templates\*" "src\DigitalSignage.Server\ClientInstaller\templates\" -Recurse -Force
   ```

2. **Use the Digital Signage Server's Remote Installer feature**:
   - Open the Digital Signage Server application
   - Navigate to **Tools ? Client Installer**
   - Select the target Raspberry Pi device (or enter IP manually)
   - Enter SSH credentials (default: username `pi`)
   - Click **Install**

   The installer will:
   - Upload all client files including the updated `cache_manager.py`
   - Run `install.sh` which will copy files to `/opt/digitalsignage-client/`
   - Restart the service
   - The schema migration will run automatically on next client startup

3. **Verify the fix**:
   ```bash
   # SSH to the Raspberry Pi
   ssh pi@<raspberry-pi-ip>
   
   # Check the logs for migration messages
   sudo journalctl -u digitalsignage-client -n 100 | grep -i "migrat"
   
   # You should see:
   # "Migrating layouts table: adding expires_at column"
   # "layouts table migration completed"
   ```

### Option 2: Manual Deployment (For Single Device)

1. **Copy the updated `cache_manager.py` to the Raspberry Pi**:
   ```bash
   scp src/DigitalSignage.Client.RaspberryPi/cache_manager.py pi@<raspberry-pi-ip>:/opt/digitalsignage-client/
   ```

2. **Restart the client service**:
   ```bash
   ssh pi@<raspberry-pi-ip>
   sudo systemctl restart digitalsignage-client
   ```

3. **Verify the migration**:
   ```bash
   sudo journalctl -u digitalsignage-client -f
   ```

   Look for these log entries:
   ```
   Migrating layouts table: adding expires_at column
   layouts table migration completed
   Migrating layout_data table: adding expires_at column
   layout_data table migration completed
   Schema migration check completed
   ```

### Option 3: Full Reinstall (Clean Slate)

For a completely fresh installation:

1. SSH to the Raspberry Pi
2. Run the uninstall/cleanup:
   ```bash
   sudo systemctl stop digitalsignage-client
   sudo systemctl disable digitalsignage-client
   sudo rm -rf /opt/digitalsignage-client
   sudo rm /etc/systemd/system/digitalsignage-client.service
   sudo systemctl daemon-reload
   ```

3. Use the Remote Installer from the server (Option 1 above) to perform a fresh install

## Technical Details

### What Changed in `cache_manager.py`

Added the `_migrate_schema()` method that runs after database initialization:

```python
def _migrate_schema(self):
    """Migrate database schema to add missing columns if needed"""
    try:
        conn = sqlite3.connect(str(self.db_path))
        cursor = conn.cursor()

        # Check if expires_at column exists in layouts table
        cursor.execute("PRAGMA table_info(layouts)")
        columns = [row[1] for row in cursor.fetchall()]
        
        if 'expires_at' not in columns:
            logger.info("Migrating layouts table: adding expires_at column")
            cursor.execute("ALTER TABLE layouts ADD COLUMN expires_at TEXT")
            conn.commit()
            logger.info("layouts table migration completed")

        # Check if expires_at column exists in layout_data table
        cursor.execute("PRAGMA table_info(layout_data)")
        columns = [row[1] for row in cursor.fetchall()]
        
        if 'expires_at' not in columns:
            logger.info("Migrating layout_data table: adding expires_at column")
            cursor.execute("ALTER TABLE layout_data ADD COLUMN expires_at TEXT")
            conn.commit()
            logger.info("layout_data table migration completed")

        conn.close()
        logger.debug("Schema migration check completed")
    except Exception as e:
        logger.error(f"Failed to migrate database schema: {e}")
```

### Why This Approach

- **Automatic**: No manual database manipulation required
- **Safe**: Uses `ALTER TABLE ADD COLUMN` which preserves existing data
- **Idempotent**: Can be run multiple times safely (checks if column exists first)
- **Non-blocking**: If migration fails, the client continues (with errors logged)
- **Backward compatible**: New installations create the correct schema immediately

## Rollback Plan

If issues occur after deployment:

1. **View the database schema**:
   ```bash
   sqlite3 ~/.digitalsignage/cache/offline_cache.db "PRAGMA table_info(layouts);"
   ```

2. **Manually remove the column** (if needed):
   ```bash
   # SQLite doesn't support DROP COLUMN in older versions
   # Easier to just delete and recreate the database
   rm ~/.digitalsignage/cache/offline_cache.db
   sudo systemctl restart digitalsignage-client
   ```

3. **Redeploy the old version**:
   - Copy the old `cache_manager.py` back to the device
   - Restart the service

## Testing

After deployment, verify that:

1. The client starts successfully:
   ```bash
   sudo systemctl status digitalsignage-client
   ```

2. No errors appear in the logs:
   ```bash
   sudo journalctl -u digitalsignage-client -n 50 | grep -i error
   ```

3. Layouts can be cached successfully:
   ```bash
   # Check the cache database
   sqlite3 ~/.digitalsignage/cache/offline_cache.db "SELECT * FROM layouts LIMIT 1;"
   ```

4. The `expires_at` column is present:
   ```bash
   sqlite3 ~/.digitalsignage/cache/offline_cache.db "PRAGMA table_info(layouts);" | grep expires_at
   ```

## Support

If issues persist after deployment:

1. Collect diagnostic information:
   ```bash
   # Client logs
   sudo journalctl -u digitalsignage-client -n 200 > client-logs.txt
   
   # Database schema
   sqlite3 ~/.digitalsignage/cache/offline_cache.db ".schema" > db-schema.txt
   
   # System info
   uname -a > system-info.txt
   python3 --version >> system-info.txt
   ```

2. Review the logs for migration-related errors
3. Check if the database file is writable by the client process
4. Verify SQLite version supports ALTER TABLE (should be fine on modern Raspbian/Raspberry Pi OS)

## Future Improvements

For future schema changes, consider:

1. **Version tracking**: Add a schema version table to track applied migrations
2. **Migration scripts**: Create a separate migrations directory with numbered migration files
3. **Rollback support**: Keep backups of database before major migrations
4. **Testing**: Add unit tests for migration code
5. **Documentation**: Maintain a changelog of schema changes

---

**Last Updated**: 2024-11-17  
**Related Files**: 
- `src/DigitalSignage.Client.RaspberryPi/cache_manager.py`
- `src/DigitalSignage.Server/Services/RemoteClientInstallerService.cs`
- `src/DigitalSignage.Client.RaspberryPi/install.sh`
