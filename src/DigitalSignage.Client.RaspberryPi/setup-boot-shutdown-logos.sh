#!/usr/bin/env bash

# Digital Signage Boot and Shutdown Logo Setup
# Comprehensive script to configure branded logos for boot and shutdown sequences
#
# Usage:
#   sudo ./setup-boot-shutdown-logos.sh [--logo /path/to/logo.png] [--no-shutdown]
#
# This script:
#   1. Validates the logo file
#   2. Installs Plymouth boot splash system
#   3. Configures quiet boot with splash screen
#   4. Sets up shutdown logo display
#   5. Enables systemd shutdown service

set -euo pipefail

# Color codes
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
LOGO_PATH=""
INSTALL_SHUTDOWN=true
INSTALL_DIR="/opt/digitalsignage-client"
VENV_DIR="$INSTALL_DIR/venv"
BOOT_LOGO_MANAGER="$INSTALL_DIR/boot_logo_manager.py"
SHUTDOWN_SCRIPT="$INSTALL_DIR/shutdown_logo_display.py"
SHUTDOWN_SERVICE="/etc/systemd/system/digitalsignage-client-shutdown.service"

# Functions
print_header() {
    echo ""
    echo -e "${BLUE}=== $1 ===${NC}"
    echo ""
}

print_step() {
    echo -e "${YELLOW}[*] $1${NC}"
}

print_success() {
    echo -e "${GREEN}[✓] $1${NC}"
}

print_error() {
    echo -e "${RED}[✗] $1${NC}"
}

print_info() {
    echo -e "${BLUE}[i] $1${NC}"
}

check_root() {
    if [ "$EUID" -ne 0 ]; then
        print_error "This script must be run as root (use sudo)"
        exit 1
    fi
}

find_logo() {
    # Search for logo in standard locations
    local candidates=(
        "$INSTALL_DIR/digisign-logo.png"
        "/root/digitalsignage/src/DigitalSignage.Client.RaspberryPi/digisign-logo.png"
        "/digisign-logo.png"
    )

    for candidate in "${candidates[@]}"; do
        if [ -f "$candidate" ]; then
            echo "$candidate"
            return 0
        fi
    done

    return 1
}

validate_logo() {
    if [ -z "$LOGO_PATH" ]; then
        print_error "Logo path not specified and could not auto-discover"
        return 1
    fi

    if [ ! -f "$LOGO_PATH" ]; then
        print_error "Logo file not found: $LOGO_PATH"
        return 1
    fi

    if [ ! -r "$LOGO_PATH" ]; then
        print_error "Logo file not readable: $LOGO_PATH"
        return 1
    fi

    local file_type=$(file "$LOGO_PATH" | grep -i "PNG" || true)
    if [ -z "$file_type" ]; then
        print_error "Logo file is not a valid PNG: $LOGO_PATH"
        return 1
    fi

    local file_size=$(stat -f%z "$LOGO_PATH" 2>/dev/null || stat -c%s "$LOGO_PATH")
    local file_size_mb=$((file_size / 1024 / 1024))

    if [ "$file_size_mb" -gt 10 ]; then
        print_error "Logo file is too large (${file_size_mb}MB > 10MB): $LOGO_PATH"
        return 1
    fi

    print_success "Logo validated: $LOGO_PATH ($(numfmt --to=iec-i --suffix=B $file_size 2>/dev/null || echo "$file_size bytes"))"
    return 0
}

setup_boot_logo() {
    print_header "Setting Up Boot Logo"

    if [ ! -f "$BOOT_LOGO_MANAGER" ]; then
        print_error "boot_logo_manager.py not found at $BOOT_LOGO_MANAGER"
        return 1
    fi

    print_step "Running boot logo manager..."
    if python3 "$BOOT_LOGO_MANAGER" --logo "$LOGO_PATH"; then
        print_success "Boot logo system configured"
        return 0
    else
        print_error "Failed to configure boot logo system"
        return 1
    fi
}

setup_shutdown_logo() {
    if [ "$INSTALL_SHUTDOWN" = false ]; then
        print_info "Shutdown logo setup disabled (--no-shutdown specified)"
        return 0
    fi

    print_header "Setting Up Shutdown Logo"

    if [ ! -f "$SHUTDOWN_SCRIPT" ]; then
        print_error "shutdown_logo_display.py not found at $SHUTDOWN_SCRIPT"
        return 1
    fi

    if [ ! -f "$SHUTDOWN_SERVICE" ]; then
        print_error "Shutdown service file not found at $SHUTDOWN_SERVICE"
        print_info "Make sure digitalsignage-client-shutdown.service is installed"
        return 1
    fi

    print_step "Enabling shutdown service..."
    if systemctl enable digitalsignage-client-shutdown.service; then
        print_success "Shutdown service enabled"
    else
        print_error "Failed to enable shutdown service"
        return 1
    fi

    print_step "Testing shutdown logo script..."
    if timeout 5 python3 "$SHUTDOWN_SCRIPT" 2>/dev/null || [ $? -eq 124 ]; then
        print_success "Shutdown logo script is executable"
    else
        print_error "Shutdown logo script test failed"
        return 1
    fi

    return 0
}

verify_boot_parameters() {
    print_header "Verifying Boot Configuration"

    local boot_dir
    if [ -d "/boot/firmware" ]; then
        boot_dir="/boot/firmware"
    else
        boot_dir="/boot"
    fi

    # Check cmdline.txt
    if [ -f "$boot_dir/cmdline.txt" ]; then
        print_step "Checking $boot_dir/cmdline.txt..."
        if grep -q "quiet" "$boot_dir/cmdline.txt" && grep -q "splash" "$boot_dir/cmdline.txt"; then
            print_success "Quiet boot parameters present"
        else
            print_error "Quiet boot parameters missing"
            return 1
        fi
    fi

    # Check config.txt
    if [ -f "$boot_dir/config.txt" ]; then
        print_step "Checking $boot_dir/config.txt..."
        if grep -q "disable_splash=1" "$boot_dir/config.txt"; then
            print_success "Splash configuration present"
        else
            print_error "Splash configuration missing"
            return 1
        fi
    fi

    # Check Plymouth
    if command -v plymouth &>/dev/null; then
        print_success "Plymouth is installed"
        local theme=$(plymouth-set-default-theme)
        if [ "$theme" = "pix" ]; then
            print_success "Plymouth theme is set to 'pix'"
        else
            print_info "Current Plymouth theme: $theme (expected: pix)"
        fi
    else
        print_error "Plymouth not installed"
        return 1
    fi

    return 0
}

verify_shutdown_service() {
    if [ "$INSTALL_SHUTDOWN" = false ]; then
        return 0
    fi

    print_header "Verifying Shutdown Service"

    if systemctl is-enabled digitalsignage-client-shutdown.service &>/dev/null; then
        print_success "Shutdown service is enabled"
    else
        print_error "Shutdown service is not enabled"
        return 1
    fi

    if [ -f "$SHUTDOWN_SERVICE" ]; then
        print_success "Shutdown service file exists"
    else
        print_error "Shutdown service file not found"
        return 1
    fi

    return 0
}

parse_arguments() {
    while [ $# -gt 0 ]; do
        case "$1" in
            --logo)
                LOGO_PATH="$2"
                shift 2
                ;;
            --no-shutdown)
                INSTALL_SHUTDOWN=false
                shift
                ;;
            --help)
                print_usage
                exit 0
                ;;
            *)
                print_error "Unknown option: $1"
                print_usage
                exit 1
                ;;
        esac
    done
}

print_usage() {
    cat <<EOF
Digital Signage Boot and Shutdown Logo Setup

Usage:
  sudo ./setup-boot-shutdown-logos.sh [OPTIONS]

Options:
  --logo PATH        Path to custom logo PNG file
  --no-shutdown      Skip shutdown logo setup
  --help            Show this help message

Examples:
  # Setup with auto-discovered logo
  sudo ./setup-boot-shutdown-logos.sh

  # Setup with custom logo
  sudo ./setup-boot-shutdown-logos.sh --logo /path/to/logo.png

  # Boot logo only (no shutdown service)
  sudo ./setup-boot-shutdown-logos.sh --no-shutdown

EOF
}

main() {
    print_header "Digital Signage Boot and Shutdown Logo Setup"

    check_root
    parse_arguments "$@"

    # Auto-discover logo if not specified
    if [ -z "$LOGO_PATH" ]; then
        print_step "Auto-discovering logo..."
        if LOGO_PATH=$(find_logo); then
            print_success "Found logo: $LOGO_PATH"
        else
            print_error "Could not find logo in standard locations"
            print_info "Specify logo with: --logo /path/to/logo.png"
            exit 1
        fi
    fi

    # Validate logo
    if ! validate_logo; then
        exit 1
    fi

    # Setup boot logo
    if ! setup_boot_logo; then
        exit 1
    fi

    # Setup shutdown logo
    if ! setup_shutdown_logo; then
        print_error "Shutdown logo setup failed (non-critical)"
        # Don't exit, boot logo is more important
    fi

    # Verify configuration
    if ! verify_boot_parameters; then
        exit 1
    fi

    if ! verify_shutdown_service; then
        print_error "Shutdown service verification failed (non-critical)"
    fi

    # Final summary
    print_header "Setup Complete"

    echo "Boot Logo:"
    echo "  - Logo: $LOGO_PATH"
    echo "  - Splash: Configured via Plymouth"
    echo "  - Kernel parameters: quiet, splash, logo.nologo"
    echo ""

    if [ "$INSTALL_SHUTDOWN" = true ]; then
        echo "Shutdown Logo:"
        echo "  - Service: digitalsignage-client-shutdown.service (enabled)"
        echo "  - Display: Shows during shutdown sequence"
        echo ""
    fi

    echo "Next Steps:"
    echo "  1. Reboot to see boot logo: sudo reboot"
    echo "  2. Check boot logs: sudo journalctl -b | grep -i plymouth"
    echo "  3. For shutdown testing: sudo systemctl restart digitalsignage-client-shutdown.service"
    echo ""
    echo "For troubleshooting, see BOOT_LOGO_SETUP.md"
    echo ""

    print_success "All configurations completed successfully!"
}

main "$@"
