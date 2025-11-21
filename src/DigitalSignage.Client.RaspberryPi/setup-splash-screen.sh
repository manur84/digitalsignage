#!/usr/bin/env bash

# Idempotent splash/loading screen setup for the Raspberry Pi client.
# - Disables the rainbow screen
# - Hides boot noise (logos/cursor/kernel chatter)
# - Installs Plymouth with the PIX theme and swaps in your logo
# - Shows the splash on both boot and shutdown
#
# Usage:
#   sudo ./setup-splash-screen.sh [/path/to/logo.png]
# Default logo path: /digisign-logo.png (place your logo there before running)

set -euo pipefail

LOGO_PATH="${1:-/digisign-logo.png}"

# Auto-detect boot directory (Raspberry Pi OS changed location in newer versions)
if [ -d "/boot/firmware" ] && [ -w "/boot/firmware" ]; then
  BOOT_DIR="/boot/firmware"
else
  BOOT_DIR="/boot"
fi

CMDLINE_FILE="$BOOT_DIR/cmdline.txt"
CONFIG_TXT="$BOOT_DIR/config.txt"
PLYMOUTH_THEME_DIR="/usr/share/plymouth/themes/pix"
PIX_SCRIPT="$PLYMOUTH_THEME_DIR/pix.script"

echo "Using boot directory: $BOOT_DIR"

CMDLINE_FLAGS=(
  "logo.nologo"
  "loglevel=3"
  "quiet"
  "splash"
  "vt.global_cursor_default=0"
  "plymouth.ignore-serial-consoles"
  # WARNING: fbcon=map controls which framebuffer the console uses
  # Options:
  #   - fbcon=map:0 uses default fb0 (HDMI) - RECOMMENDED
  #   - fbcon=map:10 for dual-display (tries fb1 then falls back to fb0)
  #   - fbcon=map:2 maps to fb2 (may DISABLE CLI if fb2 doesn't exist!)
  #   - fbcon=map:1 to use fb1 explicitly
  # Using fbcon=map:0 for standard HDMI display:
  "fbcon=map:0"
)

require_root() {
  if [[ $EUID -ne 0 ]]; then
    echo "ERROR: please run as root (use sudo)." >&2
    exit 1
  fi
}

require_logo() {
  if [[ ! -f "$LOGO_PATH" ]]; then
    echo "ERROR: logo not found at '$LOGO_PATH'." >&2
    echo "Pass a custom path or place digisign-logo.png at / before running." >&2
    exit 1
  fi
}

append_config_txt() {
  # CRITICAL FIX: REMOVE/COMMENT disable_splash=1 to ENABLE Plymouth splash screen
  # disable_splash=1 DISABLES the splash screen - we want it ENABLED!
  if grep -Eq '^disable_splash=1' "$CONFIG_TXT"; then
    sed -i 's/^disable_splash=1/#disable_splash=1 # Commented by Digital Signage setup/' "$CONFIG_TXT"
    echo "Commented out disable_splash=1 in $CONFIG_TXT (enables splash screen)"
  else
    echo "disable_splash not found or already disabled in $CONFIG_TXT"
  fi
}

patch_cmdline() {
  # cmdline.txt is a single line; keep existing args and append missing ones.
  local current
  current=$(tr '\n' ' ' <"$CMDLINE_FILE" | tr -s ' ')
  for flag in "${CMDLINE_FLAGS[@]}"; do
    if ! grep -qw "$flag" <<<"$current"; then
      current="$current $flag"
    fi
  done
  # Trim leading/trailing spaces
  current=$(echo "$current" | xargs)
  echo "$current" >"$CMDLINE_FILE"
  echo "Updated $CMDLINE_FILE with splash-friendly flags."
}

install_packages() {
  echo "Installing Plymouth packages..."
  apt-get update -y
  apt-get install -y plymouth plymouth-themes pix-plym-splash
}

update_pix_script() {
  cat >"$PIX_SCRIPT" <<'EOF'
screen_width = Window.GetWidth();
screen_height = Window.GetHeight();

theme_image = Image("splash.png");
image_width = theme_image.GetWidth();
image_height = theme_image.GetHeight();

scale_x = image_width / screen_width;
scale_y = image_height / screen_height;

if (scale_x > 1 || scale_y > 1)
{
       if (scale_x > scale_y)
       {
               resized_image = theme_image.Scale (screen_width, image_height / scale_x);
               image_x = 0;
               image_y = (screen_height - ((image_height  * screen_width) / image_width)) / 2;
       }
       else
       {
               resized_image = theme_image.Scale (image_width / scale_y, screen_height);
               image_x = (screen_width - ((image_width  * screen_height) / image_height)) / 2;
               image_y = 0;
       }
}
else
{
        resized_image = theme_image.Scale (image_width, image_height);
        image_x = (screen_width - image_width) / 2;
        image_y = (screen_height - image_height) / 2;
}

sprite = Sprite (resized_image);
sprite.SetPosition (image_x, image_y, -100);
EOF
  echo "Updated $PIX_SCRIPT with center/scale logic."
}

configure_initramfs_modules() {
  # Configure OPTIONAL fbtft modules for special LCD displays (HY28a, ILI9320, etc.)
  # These modules enable support for SPI-based TFT LCD displays that create /dev/fb2
  #
  # ONLY NEEDED IF:
  #   - You have a special SPI TFT LCD display (HY28a, ILI9320, fb2-based displays)
  #   - You want to use fb1 or fb2 instead of the default HDMI fb0
  #
  # References:
  #   - https://github.com/notro/fbtft/wiki
  #   - https://www.kernel.org/doc/Documentation/fb/fbcon.txt
  #
  # NOTE: These modules are NOT required for standard HDMI displays!

  local modules_file="/etc/initramfs-tools/modules"
  local modules_to_add=(
    "# LCD Display Support (OPTIONAL - for HY28a, ILI9320, etc.)"
    "# Uncomment the lines below if you have a special SPI TFT LCD display:"
    "# fbtft"
    "# fbtft_device name=hy28a verbose=0"
    "# fb_ili9320"
  )

  # Check if we've already added these modules
  if grep -q "# LCD Display Support (OPTIONAL" "$modules_file" 2>/dev/null; then
    echo "LCD display modules already configured in $modules_file (skipping)"
    return 0
  fi

  echo "Adding OPTIONAL LCD display module configuration to $modules_file..."
  for module_line in "${modules_to_add[@]}"; do
    echo "$module_line" >> "$modules_file"
  done
  echo "LCD display modules added (currently commented out - uncomment if needed)"
}

enable_framebuffer() {
  # OPTIMIZATION from Ubuntu Users Wiki:
  # Activate framebuffer support for better Plymouth rendering
  # https://wiki.ubuntuusers.de/Plymouth
  #
  # For advanced users: You can specify which framebuffer device to use:
  #   - FRAMEBUFFER=y (default, auto-detect)
  #   - FRAMEBUFFER=/dev/fb0 (HDMI display, most common)
  #   - FRAMEBUFFER=/dev/fb1 (secondary display or special LCD)
  #   - FRAMEBUFFER=/dev/fb2 (tertiary display, e.g., HY28a LCD)
  #
  # To use fb1 instead of default fb0, create /etc/initramfs-tools/conf.d/fb1:
  #   echo "FRAMEBUFFER=/dev/fb1" | sudo tee /etc/initramfs-tools/conf.d/fb1
  #
  local splash_conf="/etc/initramfs-tools/conf.d/splash"

  if [ ! -f "$splash_conf" ] || ! grep -q "FRAMEBUFFER=y" "$splash_conf" 2>/dev/null; then
    mkdir -p /etc/initramfs-tools/conf.d
    echo "FRAMEBUFFER=y" >> "$splash_conf"
    echo "Framebuffer support enabled in $splash_conf"
  else
    echo "Framebuffer support already enabled"
  fi
}

configure_plymouth() {
  install -m 0644 "$LOGO_PATH" "$PLYMOUTH_THEME_DIR/splash.png"
  echo "Copied logo to $PLYMOUTH_THEME_DIR/splash.png"

  update_pix_script

  # WIKI OPTIMIZATION: Enable framebuffer for better rendering
  enable_framebuffer

  # Set theme and rebuild initramfs
  # CRITICAL FIX: Use mkinitramfs instead of plymouth-set-default-theme -R
  # Raspberry Pi requires explicit initramfs rebuild for boot logo to work
  echo "Setting Plymouth theme to pix..."
  plymouth-set-default-theme pix 2>/dev/null || true

  echo "Rebuilding initramfs with Plymouth theme..."
  # Get kernel version
  KERNEL_VERSION=$(uname -r)

  # Rebuild initramfs for current kernel
  # This embeds the splash image in the initramfs so it shows at boot
  # -u = update all kernels, -k = specific kernel
  update-initramfs -u -k "$KERNEL_VERSION" 2>&1 | grep -E "Generating|update-initramfs" || \
    mkinitramfs -o /boot/firmware/initramfs "$KERNEL_VERSION"

  echo "Plymouth initramfs rebuild completed"
  echo ""
  echo "NOTE: System reboot required for changes to take effect"
  echo ""
  echo "=== ADVANCED CONFIGURATION OPTIONS ==="
  echo "For special LCD displays (HY28a, ILI9320, etc.):"
  echo "  1. Edit /etc/initramfs-tools/modules and uncomment the fbtft lines"
  echo "  2. Run: sudo update-initramfs -u -k $KERNEL_VERSION"
  echo "  3. Reboot"
  echo ""
  echo "Framebuffer console mapping (already configured as fbcon=map:0):"
  echo "  - fbcon=map:0 (CURRENT): uses default fb0 (HDMI) - RECOMMENDED"
  echo "  - fbcon=map:10: tries fb1 then falls back to fb0 (for dual-display)"
  echo "  - fbcon=map:2: maps to fb2 (WARNING: may disable CLI if fb2 doesn't exist!)"
  echo "  - fbcon=map:1: uses fb1 explicitly"
}

main() {
  require_root
  require_logo

  append_config_txt
  patch_cmdline
  install_packages

  # Configure initramfs modules BEFORE Plymouth configuration
  # This ensures fbtft modules are available when initramfs is rebuilt
  configure_initramfs_modules

  configure_plymouth

  echo ""
  echo "Splash screen configured. Reboot to see it:"
  echo "  sudo reboot"
}

main "$@"
