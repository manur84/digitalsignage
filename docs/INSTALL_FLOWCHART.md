# Install.sh Decision Flow

## Visual Decision Flow

```
┌─────────────────────────────────────┐
│   sudo ./install.sh                 │
│   (User runs installation)          │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   System Detection Phase            │
├─────────────────────────────────────┤
│ 1. Check for existing installation  │
│ 2. Install dependencies             │
│ 3. Create virtual environment       │
│ 4. Copy client files                │
│ 5. Configure service                │
│ 6. Run pre-flight test              │
└──────────────┬──────────────────────┘
               │
               ▼
┌─────────────────────────────────────┐
│   Display Hardware Detection        │
├─────────────────────────────────────┤
│ → detect_display_mode()             │
│   - Check X11 on :0                 │
│   - Set DETECTED_MODE               │
│                                     │
│ → check_hdmi_display()              │
│   - tvservice check                 │
│   - DRM status check                │
│   - xrandr check                    │
│   - Set HDMI_DETECTED               │
└──────────────┬──────────────────────┘
               │
        ┌──────┴──────┐
        │             │
   [HDMI?]       [No HDMI]
        │             │
        │             ▼
        │     ┌──────────────────────┐
        │     │  SCENARIO 3          │
        │     │  Development Mode    │
        │     ├──────────────────────┤
        │     │ Recommendation:      │
        │     │ → DEVELOPMENT MODE   │
        │     │                      │
        │     │ Reason:              │
        │     │ → No display found   │
        │     │                      │
        │     │ Configuration:       │
        │     │ → Use Xvfb           │
        │     │ → No auto-login      │
        │     │ → No reboot          │
        │     └──────┬───────────────┘
        │            │
        ▼            │
   [X11 on :0?]      │
        │            │
   ┌────┴────┐       │
   │         │       │
  YES       NO       │
   │         │       │
   ▼         ▼       │
┌─────┐  ┌─────┐    │
│ S2  │  │ S1  │    │
└──┬──┘  └──┬──┘    │
   │        │        │
   │        │        │
   ▼        ▼        ▼
┌──────────────────────────────────────┐
│   User Selection                     │
├──────────────────────────────────────┤
│                                      │
│ Recommended mode shown with reason   │
│                                      │
│ User can choose:                     │
│   1) PRODUCTION MODE                 │
│   2) DEVELOPMENT MODE                │
│                                      │
│ Default: [Recommended Mode]          │
└──────────────┬───────────────────────┘
               │
        ┌──────┴──────┐
        │             │
[PRODUCTION]    [DEVELOPMENT]
        │             │
        ▼             ▼
┌──────────────┐  ┌──────────────┐
│ Production   │  │ Development  │
│ Config       │  │ Config       │
├──────────────┤  ├──────────────┤
│ Check:       │  │ Configure:   │
│ 1. Auto-login│  │ → Xvfb       │
│ 2. LightDM   │  │ → Service    │
│ 3. .xinitrc  │  │              │
│ 4. Service   │  │ Result:      │
│ 5. Verify    │  │ → Running    │
│              │  │ → No reboot  │
│ Track:       │  └──────────────┘
│ NEEDS_REBOOT │
│              │
│ If changes:  │
│ → Reboot     │
│ Else:        │
│ → No reboot  │
└──────────────┘
```

## Detailed Scenario Breakdown

### Scenario 1: HDMI Connected, X11 Not Running

```
Detection Results:
├─ HDMI_DETECTED = 0 (true)
└─ DISPLAY_DETECTED = 1 (false - X11 not on :0)

Recommendation:
┌────────────────────────────────────────┐
│ PRODUCTION MODE                        │
│                                        │
│ Reason: HDMI detected, X11 not config  │
│                                        │
│ Will configure:                        │
│ ✓ Enable auto-login                    │
│ ✓ Configure LightDM                    │
│ ✓ Create .xinitrc                      │
│ ✓ Disable power management             │
│                                        │
│ Result: NEEDS_REBOOT = true            │
└────────────────────────────────────────┘

Configuration Steps:
[1/5] Checking auto-login...
      → raspi-config: NOT B4
      → Enable B4
      → NEEDS_REBOOT = true

[2/5] Configuring display manager...
      → LightDM: autologin-user not set
      → Set autologin-user
      → NEEDS_REBOOT = true

[3/5] Configuring X11 startup...
      → .xinitrc: missing or incomplete
      → Create .xinitrc
      → (no reboot impact)

[4/5] Service configuration...
      → Using start-with-display.sh
      → (auto-detects display)

[5/5] Verifying...
      → Changes made: NEEDS_REBOOT = true
      → X11 not running
      → Final: NEEDS_REBOOT = true

User Prompt:
┌────────────────────────────────────────┐
│ IMPORTANT: A REBOOT IS REQUIRED        │
│                                        │
│ After reboot:                          │
│ - System auto-login as user            │
│ - X11 starts on HDMI                   │
│ - Client starts automatically          │
│                                        │
│ Reboot now? (y/N):                     │
└────────────────────────────────────────┘
```

### Scenario 2: HDMI Connected, X11 Already Running

```
Detection Results:
├─ HDMI_DETECTED = 0 (true)
└─ DISPLAY_DETECTED = 0 (true - X11 on :0)

Recommendation:
┌────────────────────────────────────────┐
│ PRODUCTION MODE (Already Configured)   │
│                                        │
│ Reason: X11 already running            │
│                                        │
│ Will verify:                           │
│ ✓ Check auto-login                     │
│ ✓ Check LightDM                        │
│ ✓ Check .xinitrc                       │
│ ✓ Ensure power management off          │
│                                        │
│ Result: May not need reboot            │
└────────────────────────────────────────┘

Configuration Steps:
[1/5] Checking auto-login...
      → raspi-config: Already B4
      → Skip
      → NEEDS_REBOOT = false

[2/5] Configuring display manager...
      → LightDM: Already configured
      → Skip
      → NEEDS_REBOOT = false

[3/5] Configuring X11 startup...
      → .xinitrc: Already exists
      → Skip
      → NEEDS_REBOOT = false

[4/5] Service configuration...
      → Using start-with-display.sh
      → (already configured)

[5/5] Verifying...
      → No changes made
      → X11 is running
      → Final: NEEDS_REBOOT = false

User Message:
┌────────────────────────────────────────┐
│ ✓ No reboot required                   │
│                                        │
│ System already configured:             │
│ - X11 running on display               │
│ - Service ready                        │
│ - Production mode active               │
└────────────────────────────────────────┘
```

### Scenario 3: No HDMI Display (Headless)

```
Detection Results:
├─ HDMI_DETECTED = 1 (false)
└─ DISPLAY_DETECTED = 1 (false - no X11)

Recommendation:
┌────────────────────────────────────────┐
│ DEVELOPMENT MODE (Headless)            │
│                                        │
│ Reason: No HDMI display detected       │
│                                        │
│ Will configure:                        │
│ ✓ Use Xvfb virtual display             │
│ ✓ Install service                      │
│ ✓ No auto-login                        │
│ ✓ No X11 configuration                 │
│                                        │
│ Result: No reboot needed               │
└────────────────────────────────────────┘

Configuration Steps:
→ Skip production mode configuration
→ Service uses start-with-display.sh
→ start-with-display.sh detects no :0
→ Automatically starts Xvfb :99
→ Client runs on virtual display

User Message:
┌────────────────────────────────────────┐
│ ✓ DEVELOPMENT MODE selected            │
│                                        │
│ Configuration:                         │
│ - Service configured for headless      │
│ - Uses Xvfb virtual display            │
│ - No auto-login configured             │
│ - No reboot required                   │
│                                        │
│ Service status: RUNNING                │
└────────────────────────────────────────┘
```

## Detection Method Details

### HDMI Detection Priority

```
1. tvservice (Raspberry Pi specific)
   ├─ Command: tvservice -s
   ├─ Check: Output contains "HDMI"
   ├─ Pros: Most reliable on RPi with legacy driver
   └─ Cons: May not work with KMS driver

2. DRM (Direct Rendering Manager)
   ├─ Command: cat /sys/class/drm/*/status
   ├─ Check: Any status == "connected"
   ├─ Pros: Works with KMS driver
   └─ Cons: May not exist on older systems

3. xrandr (X11 RandR)
   ├─ Command: DISPLAY=:0 xrandr
   ├─ Check: Output contains " connected"
   ├─ Pros: Works when X11 running
   └─ Cons: Requires X11 to be active

Result: First successful method determines HDMI_DETECTED
```

### X11 Detection

```
Method: xset query
├─ Command: DISPLAY=:0 xset q
├─ Run as: sudo -u $ACTUAL_USER
├─ Check: Exit code == 0
├─ Pros: Reliable, fast, low overhead
└─ Cons: Only checks :0 display

Fallback checks:
├─ pgrep -x X (check X server process)
├─ pgrep -x Xorg (check Xorg process)
└─ Set DETECTED_MODE accordingly

Modes:
├─ desktop: X11 accessible on :0
├─ console: No X11 running
└─ other: X11 running but not on :0
```

## Configuration Decision Matrix

| HDMI | X11 on :0 | Recommended | Auto-login | .xinitrc | Reboot  | Notes                    |
|------|-----------|-------------|------------|----------|---------|--------------------------|
| YES  | NO        | Production  | Enable     | Create   | YES     | Fresh install, configure |
| YES  | YES       | Production  | Check      | Check    | MAYBE   | Verify existing config   |
| NO   | NO        | Development | Skip       | Skip     | NO      | Headless, use Xvfb       |
| NO   | YES       | Production  | Check      | Check    | NO      | VNC/Remote desktop       |

## Reboot Decision Logic

```python
NEEDS_REBOOT = False

# Check 1: Boot behavior changed?
if boot_behavior_changed:
    NEEDS_REBOOT = True

# Check 2: LightDM config changed?
if lightdm_changed:
    NEEDS_REBOOT = True

# Override: X11 already running?
if x11_running_on_display_0:
    NEEDS_REBOOT = False  # No reboot needed

# Final check: Any changes made?
if no_changes_made:
    NEEDS_REBOOT = False

# User prompt
if NEEDS_REBOOT:
    prompt_for_reboot()
else:
    print("No reboot required")
```

## Installation Timeline

### Without Reboot (Ideal Case)

```
0:00 → Start installation
0:05 → Install dependencies
0:10 → Create venv and install packages
0:15 → Copy files and configure
0:18 → Run pre-flight test
0:20 → Detect display (already configured)
0:21 → Verify settings (all OK)
0:22 → Start service
0:23 → ✓ Done - service running
```

Total: ~23 seconds

### With Reboot (Fresh Install)

```
0:00 → Start installation
0:05 → Install dependencies
0:10 → Create venv and install packages
0:15 → Copy files and configure
0:18 → Run pre-flight test
0:20 → Detect display (needs configuration)
0:21 → Configure auto-login
0:22 → Configure LightDM
0:23 → Create .xinitrc
0:24 → Prompt for reboot
     [User presses Y]
0:25 → Reboot
     [System reboots - ~30 seconds]
0:55 → System boots
1:00 → Auto-login
1:03 → X11 starts
1:05 → Service starts
1:10 → ✓ Client displays content
```

Total: ~1 minute 10 seconds (including reboot)

## Error Handling Flow

```
┌─────────────────────────────────┐
│ Error during installation?      │
└────────────┬────────────────────┘
             │
      ┌──────┴──────┐
      │             │
   [Critical]   [Warning]
      │             │
      ▼             ▼
  Exit with    Continue with
  error code   warning message
      │             │
      │             └─→ Log warning
      │                 Continue
      ▼
  Display error
  Show troubleshooting
  Exit 1

Common errors:
├─ Pre-flight test failed
│  └─→ Check logs, suggest diagnose.sh
├─ PyQt5 import failed
│  └─→ Suggest reinstall dependencies
├─ Service failed to start
│  └─→ Show logs, run diagnostic
└─ Permission denied
   └─→ Check sudo, check user
```

## Success Criteria

Installation is considered successful when:

1. ✓ All files copied to `/opt/digitalsignage-client`
2. ✓ Virtual environment created with dependencies
3. ✓ Pre-flight test passes
4. ✓ Service installed and enabled
5. ✓ Service starts successfully
6. ✓ Display configuration appropriate for hardware
7. ✓ Post-installation verification passes

Optional success indicators:
- If production mode: Auto-login configured
- If production mode: X11 will start on boot
- If development mode: Xvfb configured
- No Python import errors
- No permission errors
