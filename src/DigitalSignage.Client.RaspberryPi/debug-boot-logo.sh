#!/usr/bin/env bash
# Debug script to diagnose boot logo configuration issues

set -u

echo "========================================"
echo "Boot Logo Configuration Diagnostic"
echo "========================================"
echo ""

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

print_status() {
    local status=$1
    local message=$2
    if [ "$status" = "OK" ]; then
        echo -e "${GREEN}✓${NC} $message"
    elif [ "$status" = "WARN" ]; then
        echo -e "${YELLOW}⚠${NC} $message"
    else
        echo -e "${RED}✗${NC} $message"
    fi
}

# Find boot directory
BOOT_DIR=""
if [ -d "/boot/firmware" ] && [ -w "/boot/firmware" ]; then
    BOOT_DIR="/boot/firmware"
elif [ -d "/boot" ] && [ -w "/boot" ]; then
    BOOT_DIR="/boot"
fi

echo "1. Boot Directory"
echo "   Directory: $BOOT_DIR"
if [ -n "$BOOT_DIR" ]; then
    print_status "OK" "Boot directory found"
else
    print_status "FAIL" "No writable boot directory found"
    exit 1
fi
echo ""

# Check config.txt
echo "2. /boot/config.txt"
CONFIG_FILE="$BOOT_DIR/config.txt"
if [ -f "$CONFIG_FILE" ]; then
    print_status "OK" "config.txt exists"

    if grep -q "^disable_splash=1" "$CONFIG_FILE"; then
        print_status "OK" "disable_splash=1 is set"
    else
        print_status "FAIL" "disable_splash=1 is NOT set (rainbow screen will appear)"
        echo "   Run: echo 'disable_splash=1' | sudo tee -a $CONFIG_FILE"
    fi
else
    print_status "FAIL" "config.txt not found"
fi
echo ""

# Check cmdline.txt
echo "3. /boot/cmdline.txt"
CMDLINE_FILE="$BOOT_DIR/cmdline.txt"
if [ -f "$CMDLINE_FILE" ]; then
    print_status "OK" "cmdline.txt exists"

    CMDLINE=$(cat "$CMDLINE_FILE")

    # Check for each required parameter
    for param in "quiet" "splash" "logo.nologo" "loglevel=0" "vt.global_cursor_default=0"; do
        if echo "$CMDLINE" | grep -q "$param"; then
            print_status "OK" "Parameter '$param' is set"
        else
            print_status "WARN" "Parameter '$param' is missing"
        fi
    done
else
    print_status "FAIL" "cmdline.txt not found"
fi
echo ""

# Check for logo file
echo "4. Logo Files"
if [ -f "$BOOT_DIR/splash.png" ]; then
    SIZE=$(stat -c%s "$BOOT_DIR/splash.png" 2>/dev/null || stat -f%z "$BOOT_DIR/splash.png" 2>/dev/null)
    print_status "OK" "Boot splash exists ($BOOT_DIR/splash.png - $SIZE bytes)"
else
    print_status "FAIL" "Boot splash NOT found at $BOOT_DIR/splash.png"
fi

if [ -f "/digisign-logo.png" ]; then
    SIZE=$(stat -c%s "/digisign-logo.png" 2>/dev/null || stat -f%z "/digisign-logo.png" 2>/dev/null)
    print_status "OK" "Logo exists (/digisign-logo.png - $SIZE bytes)"
else
    print_status "WARN" "Logo not found at /digisign-logo.png (needed for Plymouth)"
fi

if [ -f "/opt/digitalsignage-client/digisign-logo.png" ]; then
    SIZE=$(stat -c%s "/opt/digitalsignage-client/digisign-logo.png" 2>/dev/null || stat -f%z "/opt/digitalsignage-client/digisign-logo.png" 2>/dev/null)
    print_status "OK" "Logo in install dir ($SIZE bytes)"
else
    print_status "WARN" "Logo not found in /opt/digitalsignage-client/"
fi
echo ""

# Check Plymouth
echo "5. Plymouth Installation"
if command -v plymouth &> /dev/null; then
    print_status "OK" "Plymouth is installed"

    # Check Plymouth theme
    if command -v plymouth-set-default-theme &> /dev/null; then
        CURRENT_THEME=$(plymouth-set-default-theme 2>/dev/null || echo "unknown")
        echo "   Current theme: $CURRENT_THEME"

        if [ "$CURRENT_THEME" = "pix" ]; then
            print_status "OK" "Plymouth theme is 'pix'"
        else
            print_status "WARN" "Plymouth theme is not 'pix' (currently: $CURRENT_THEME)"
        fi

        # Check pix theme directory
        if [ -d "/usr/share/plymouth/themes/pix" ]; then
            print_status "OK" "Pix theme directory exists"

            if [ -f "/usr/share/plymouth/themes/pix/splash.png" ]; then
                SIZE=$(stat -c%s "/usr/share/plymouth/themes/pix/splash.png" 2>/dev/null || stat -f%z "/usr/share/plymouth/themes/pix/splash.png" 2>/dev/null)
                print_status "OK" "Pix splash.png exists ($SIZE bytes)"
            else
                print_status "FAIL" "Pix splash.png NOT found"
            fi
        else
            print_status "FAIL" "Pix theme directory not found"
        fi
    fi
else
    print_status "WARN" "Plymouth is NOT installed (optional for animated splash)"
fi
echo ""

# Summary
echo "========================================"
echo "SUMMARY & RECOMMENDATIONS"
echo "========================================"
echo ""

ISSUES=0

# Check critical issues
if ! grep -q "^disable_splash=1" "$CONFIG_FILE" 2>/dev/null; then
    echo -e "${RED}CRITICAL:${NC} Rainbow screen will appear"
    echo "   Fix: echo 'disable_splash=1' | sudo tee -a $CONFIG_FILE"
    ISSUES=$((ISSUES + 1))
fi

if [ ! -f "$BOOT_DIR/splash.png" ]; then
    echo -e "${YELLOW}WARNING:${NC} No boot splash image"
    echo "   Fix: sudo cp /opt/digitalsignage-client/digisign-logo.png $BOOT_DIR/splash.png"
    ISSUES=$((ISSUES + 1))
fi

if ! command -v plymouth &> /dev/null; then
    echo -e "${YELLOW}INFO:${NC} Plymouth not installed (optional)"
    echo "   For animated boot: sudo apt-get install -y plymouth plymouth-themes pix-plym-splash"
fi

if [ $ISSUES -eq 0 ]; then
    echo -e "${GREEN}All critical checks passed!${NC}"
    echo ""
    echo "If rainbow screen still appears after reboot:"
    echo "1. Check if $BOOT_DIR/config.txt was actually saved (may need remount)"
    echo "2. Verify you're editing the correct boot partition"
    echo "3. Try: sudo mount -o remount,rw $BOOT_DIR && echo 'disable_splash=1' | sudo tee -a $BOOT_DIR/config.txt"
else
    echo -e "${RED}Found $ISSUES issue(s) that need to be fixed${NC}"
fi

echo ""
echo "Quick fix command:"
echo "sudo bash /opt/digitalsignage-client/setup-splash-screen.sh /opt/digitalsignage-client/digisign-logo.png"
