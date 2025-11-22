# Digital Signage - Command Communication Debugging Guide

## Problem
Raspberry Pi clients can register with the server successfully, but commands are no longer being sent or received.

## Diagnostic Changes Made

### Server-Side Logging (WebSocketCommunicationService.cs)
Added detailed logging for COMMAND messages:
- Line 318-321: Logs actual JSON content being sent
- Line 326-329: Confirms successful WebSocket send
- Existing debug logs show client ID lookup and connection status

### Client-Side Logging (client.py)  
Added detailed logging for COMMAND messages:
- Line 652: Logs when COMMAND message is received with full data
- Line 767: Logs command name and parameters when handle_command() is called
- Lines 774-805: Logs before and after execution of each command type

## Testing Procedure

### 1. Deploy Updated Code

**Server:**
```bash
cd /path/to/digitalsignage
dotnet build src/DigitalSignage.Server/DigitalSignage.Server.csproj
# Run the server application
```

**Client (Raspberry Pi):**
```bash
cd /opt/digitalsignage-client
git pull  # Or copy updated client.py
sudo systemctl restart digitalsignage-client
```

### 2. Trigger a Command

From the server UI:
1. Open Device Management
2. Select a registered Raspberry Pi client
3. Click "Restart", "Screenshot", or any other command button

### 3. Collect Logs

**Server Logs:**
```bash
# Check if command was sent
grep "DIAGNOSTIC.*COMMAND" /path/to/server/logs/*.log
grep "Sent command" /path/to/server/logs/*.log
```

**Client Logs:**
```bash
# On Raspberry Pi
grep "DIAGNOSTIC.*COMMAND" ~/.digitalsignage/logs/client.log
grep "Executing command" ~/.digitalsignage/logs/client.log
grep "Error executing command" ~/.digitalsignage/logs/client.log
```

## Diagnostic Scenarios

### Scenario 1: Client ID Not Found (Server Log)
```
CRITICAL DEBUG: Looking for client {ClientId} in _clients dictionary: False
Client {ClientId} not found in connections dictionary
```
**Diagnosis:** Client ID mismatch between registration and command sending  
**Fix:** Check client ID mapping in UpdateClientId()

### Scenario 2: Connection Not Open (Server Log)
```
CRITICAL DEBUG: Found connection for {ClientId}, IsConnected=False
CRITICAL ERROR: Cannot send message to client {ClientId}: connection not open!
```
**Diagnosis:** Connection state is invalid  
**Fix:** Investigate why IsConnected becomes false after registration

### Scenario 3: Message Sent But Not Received (Server + Client Logs)
```
Server: DIAGNOSTIC: COMMAND message sent successfully to client {ClientId} via WebSocket
Client: (no logs)
```
**Diagnosis:** WebSocket frame not delivered to client  
**Fix:** Check network connectivity, WebSocket frame processing

### Scenario 4: Message Received But Not Executed (Client Log)
```
Client: DIAGNOSTIC: COMMAND message received! Full data: {...}
Client: (no "Executing command" log)
```
**Diagnosis:** handle_message() or handle_command() not being called  
**Fix:** Check asyncio event loop, coroutine scheduling

### Scenario 5: Command Execution Fails (Client Log)
```
Client: DIAGNOSTIC: handle_command() called with command='RESTART'
Client: Executing command: RESTART
Client: Executing RESTART command...
Client: Error executing command RESTART: ...
```
**Diagnosis:** Command execution logic fails  
**Fix:** Fix specific command implementation

## Expected Successful Flow

**Server logs should show:**
```
[INFO] Sent command RESTART to client raspberry-pi-01
[WARN] DIAGNOSTIC: Sending COMMAND message to client raspberry-pi-01. JSON content: {"Command":"RESTART","Id":"...","Type":"COMMAND","Timestamp":"..."}
[DEBUG] Successfully sent message COMMAND to client raspberry-pi-01
[WARN] DIAGNOSTIC: COMMAND message sent successfully to client raspberry-pi-01 via WebSocket
```

**Client logs should show:**
```
[INFO] RAW MESSAGE RECEIVED FROM SERVER
[INFO] Message Type: TEXT (xxx chars)
[INFO] Parsed Message Type: COMMAND
[WARN] DIAGNOSTIC: COMMAND message received! Full data: {'Type': 'COMMAND', 'Command': 'RESTART', ...}
[WARN] DIAGNOSTIC: handle_command() called with command='RESTART', parameters={}
[INFO] Executing command: RESTART
[INFO] Executing RESTART command...
[INFO] RESTART command completed successfully
[INFO] ✓ Message COMMAND handled successfully
```

## Quick Verification Commands

```bash
# On server - tail logs in real-time
tail -f /path/to/server/logs/*.log | grep -i "command\|diagnostic"

# On Raspberry Pi - tail logs in real-time  
tail -f ~/.digitalsignage/logs/client.log | grep -i "command\|diagnostic"
```

## Next Steps After Log Collection

1. **Identify which scenario matches the log evidence**
2. **Implement targeted fix based on diagnosis**
3. **Test fix with same procedure**
4. **Verify commands work correctly**
5. **Remove diagnostic logging (or reduce to INFO level)**

## Notes

- All diagnostic logging uses `LogWarning` level on server and `logger.warning()` on client to ensure visibility even with default log levels
- The diagnostic logs include full JSON content and execution flow details
- Regular application logs at INFO/DEBUG level provide additional context
