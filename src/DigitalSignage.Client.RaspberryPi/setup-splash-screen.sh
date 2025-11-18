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
  "loglevel=1"
  "quiet"
  "splash"
  "vt.global_cursor_default=0"
  "plymouth.ignore-serial-consoles"
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
  if ! grep -Eq '^disable_splash=1' "$CONFIG_TXT"; then
    echo "disable_splash=1" >>"$CONFIG_TXT"
    echo "Added disable_splash=1 to $CONFIG_TXT"
  else
    echo "disable_splash=1 already present in $CONFIG_TXT"
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

configure_plymouth() {
  install -m 0644 "$LOGO_PATH" "$PLYMOUTH_THEME_DIR/splash.png"
  echo "Copied logo to $PLYMOUTH_THEME_DIR/splash.png"

  update_pix_script

  # -R sets the theme and rebuilds the initramfs in one step
  plymouth-set-default-theme -R pix
}

main() {
  require_root
  require_logo

  append_config_txt
  patch_cmdline
  install_packages
  configure_plymouth

  echo ""
  echo "Splash screen configured. Reboot to see it:"
  echo "  sudo reboot"
}

main "$@"
