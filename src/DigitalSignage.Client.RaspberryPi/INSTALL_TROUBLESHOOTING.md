# Installation Troubleshooting Guide

## Exit Status 203 - EXEC Failure

**Symptom**: Service fails to start with error:
```
Failed to execute /opt/digitalsignage-client/start-with-display.sh
The error number returned by this process is 2.
exit status is 203
```

**Cause**: Exit status 203 in systemd means EXEC failure - the file either doesn't exist, isn't executable, or has a bad shebang.

### Solution

The install.sh script has been fixed to:
1. Make start-with-display.sh a mandatory file (installation fails if missing)
2. Explicitly set executable permissions with chmod +x
3. Convert line endings from Windows (CRLF) to Unix (LF) if needed
4. Verify that all critical files are present and executable

### Testing the Fix

After running the updated install.sh:

1. **Verify file exists**:
   ```bash
   ls -l /opt/digitalsignage-client/start-with-display.sh
   ```
   Should show: `-rwxr-xr-x` (executable permissions)

2. **Check shebang is correct**:
   ```bash
   head -1 /opt/digitalsignage-client/start-with-display.sh
   ```
   Should show: `#!/bin/bash`

3. **Check line endings**:
   ```bash
   file /opt/digitalsignage-client/start-with-display.sh
   ```
   Should show: `Bourne-Again shell script, ASCII text executable`
   (NOT `with CRLF line terminators`)

4. **Test script manually**:
   ```bash
   /opt/digitalsignage-client/start-with-display.sh
   ```
   Should start Xvfb and attempt to run the client

5. **Test service**:
   ```bash
   sudo systemctl restart digitalsignage-client
   sudo systemctl status digitalsignage-client
   ```
   Service should start successfully

6. **View service logs**:
   ```bash
   sudo journalctl -u digitalsignage-client -f
   ```
   Should show client startup messages

### Manual Fix (If Needed)

If you encounter this issue with an existing installation:

1. **Check if file exists**:
   ```bash
   ls -l /opt/digitalsignage-client/start-with-display.sh
   ```

2. **If missing, copy from source**:
   ```bash
   sudo cp /path/to/source/start-with-display.sh /opt/digitalsignage-client/
   ```

3. **Make executable**:
   ```bash
   sudo chmod +x /opt/digitalsignage-client/start-with-display.sh
   ```

4. **Fix line endings (if needed)**:
   ```bash
   sudo sed -i 's/\r$//' /opt/digitalsignage-client/start-with-display.sh
   ```

5. **Set ownership**:
   ```bash
   sudo chown pi:pi /opt/digitalsignage-client/start-with-display.sh
   ```

6. **Restart service**:
   ```bash
   sudo systemctl restart digitalsignage-client
   ```

### Prevention

The updated install.sh script now includes:

- **Mandatory file check**: Installation fails if start-with-display.sh is missing
- **Automatic line ending conversion**: Converts CRLF to LF if needed
- **Verification step**: Confirms all critical files are present and executable
- **Explicit error messages**: Shows which files are missing

### Common Causes

1. **File not copied**: Old install.sh treated the file as optional
2. **Missing executable permission**: chmod +x not called or failed silently
3. **Windows line endings**: CRLF line endings can break shebang interpretation
4. **Bad shebang**: Incorrect shebang (e.g., `#! /bin/bash` with space) causes exec failure
5. **Missing bash**: /bin/bash doesn't exist (extremely rare)

### Related Files

- `/opt/digitalsignage-client/start-with-display.sh` - Display manager wrapper script
- `/etc/systemd/system/digitalsignage-client.service` - Service configuration
- `/opt/digitalsignage-client/client.py` - Main client application

### Additional Debugging

Enable verbose systemd logging:
```bash
sudo systemctl set-property digitalsignage-client LogLevel=debug
sudo systemctl restart digitalsignage-client
sudo journalctl -u digitalsignage-client -f
```

Check systemd execution errors:
```bash
sudo systemctl status digitalsignage-client --no-pager -l
```

Test script with bash -x (debug mode):
```bash
sudo bash -x /opt/digitalsignage-client/start-with-display.sh
```
