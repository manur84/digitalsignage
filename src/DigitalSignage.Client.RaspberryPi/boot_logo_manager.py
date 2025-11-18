#!/usr/bin/env python3
"""
Boot and Startup Logo Manager for Digital Signage Client

Handles:
- Plymouth splash screen configuration
- Boot logo setup in /boot
- cmdline.txt kernel parameters for quiet boot with splash
- Logo scaling and positioning
- Shutdown screen configuration
"""

import os
import sys
import shutil
import subprocess
import logging
from pathlib import Path
from typing import Optional, Tuple

logger = logging.getLogger(__name__)


class BootLogoManager:
    """Manages boot and shutdown logos for Raspberry Pi Digital Signage client"""

    # Plymouth theme directory
    PLYMOUTH_THEME_DIR = "/usr/share/plymouth/themes/pix"
    PLYMOUTH_SPLASH = f"{PLYMOUTH_THEME_DIR}/splash.png"
    PLYMOUTH_SCRIPT = f"{PLYMOUTH_THEME_DIR}/pix.script"

    # Boot directories (try both old and new locations)
    BOOT_DIRS = ["/boot", "/boot/firmware"]

    # Kernel command line flags for quiet boot
    BOOT_CMDLINE_FLAGS = [
        "logo.nologo",           # Hide kernel logo
        "loglevel=0",            # Suppress kernel messages
        "quiet",                 # No boot messages
        "splash",                # Show splash screen
        "vt.global_cursor_default=0",  # Hide cursor
        "plymouth.ignore-serial-consoles",  # Don't show splash on serial console
    ]

    def __init__(self, logo_path: Optional[str] = None):
        """
        Initialize Boot Logo Manager

        Args:
            logo_path: Path to custom logo PNG (defaults to digisign-logo.png)
        """
        self.logo_path = logo_path or self._find_logo()
        self.boot_dir = self._find_boot_dir()

    @staticmethod
    def _find_logo() -> Optional[str]:
        """Find digisign-logo.png in common locations"""
        candidates = [
            "/opt/digitalsignage-client/digisign-logo.png",
            "/root/digitalsignage/src/DigitalSignage.Client.RaspberryPi/digisign-logo.png",
            Path.home() / "digitalsignage/src/DigitalSignage.Client.RaspberryPi/digisign-logo.png",
            Path.home() / ".digitalsignage/digisign-logo.png",
            "/digisign-logo.png",
        ]

        for candidate in candidates:
            candidate_path = Path(candidate)
            if candidate_path.exists():
                logger.info(f"Found logo at {candidate_path}")
                return str(candidate_path)

        return None

    @staticmethod
    def _find_boot_dir() -> str:
        """Find the correct /boot directory"""
        for boot_dir in BootLogoManager.BOOT_DIRS:
            if os.path.exists(boot_dir) and os.access(boot_dir, os.W_OK):
                logger.info(f"Using boot directory: {boot_dir}")
                return boot_dir
        return "/boot"

    def require_root(self) -> bool:
        """Check if running as root"""
        if os.geteuid() != 0:
            logger.error("This script requires root privileges (use sudo)")
            return False
        return True

    def validate_logo(self) -> bool:
        """Validate that logo file exists and is readable"""
        if not self.logo_path:
            logger.error("Logo file not found in any standard location")
            return False

        logo_file = Path(self.logo_path)
        if not logo_file.exists():
            logger.error(f"Logo file does not exist: {self.logo_path}")
            return False

        if not logo_file.is_file():
            logger.error(f"Logo path is not a file: {self.logo_path}")
            return False

        logger.info(f"Logo validated: {self.logo_path} ({logo_file.stat().st_size} bytes)")
        return True

    def setup_boot_splash(self) -> bool:
        """
        Setup boot splash screen in /boot

        Returns:
            True if successful, False otherwise
        """
        try:
            if not self.validate_logo():
                return False

            splash_path = os.path.join(self.boot_dir, "splash.png")

            # Copy logo to boot directory
            shutil.copy2(self.logo_path, splash_path)
            logger.info(f"Boot splash copied to {splash_path}")

            return True

        except Exception as e:
            logger.error(f"Failed to setup boot splash: {e}")
            return False

    def configure_cmdline(self) -> bool:
        """
        Configure /boot/cmdline.txt for quiet boot with splash

        Returns:
            True if successful, False otherwise
        """
        try:
            cmdline_path = os.path.join(self.boot_dir, "cmdline.txt")

            if not os.path.exists(cmdline_path):
                logger.warning(f"cmdline.txt not found at {cmdline_path}")
                return False

            # Read current cmdline
            with open(cmdline_path, 'r') as f:
                cmdline = f.read().strip()

            # Split into tokens
            tokens = cmdline.split()

            # Add missing flags
            for flag in self.BOOT_CMDLINE_FLAGS:
                if flag not in tokens:
                    tokens.append(flag)
                    logger.info(f"Added kernel parameter: {flag}")

            # Write back
            updated_cmdline = " ".join(tokens) + "\n"
            with open(cmdline_path, 'w') as f:
                f.write(updated_cmdline)

            logger.info(f"Updated {cmdline_path}")
            return True

        except Exception as e:
            logger.error(f"Failed to configure cmdline.txt: {e}")
            return False

    def configure_config_txt(self) -> bool:
        """
        Configure /boot/config.txt for boot logo

        Returns:
            True if successful, False otherwise
        """
        try:
            config_path = os.path.join(self.boot_dir, "config.txt")

            if not os.path.exists(config_path):
                logger.warning(f"config.txt not found at {config_path}")
                return False

            # Read current config
            with open(config_path, 'r') as f:
                config_lines = f.readlines()

            # Check for disable_splash setting
            has_disable_splash = any("disable_splash" in line for line in config_lines)

            # Add or update disable_splash setting
            if not has_disable_splash:
                config_lines.append("# Disable rainbow splash (use custom logo instead)\n")
                config_lines.append("disable_splash=1\n")
                logger.info("Added disable_splash=1 to config.txt")

            # Write back
            with open(config_path, 'w') as f:
                f.writelines(config_lines)

            logger.info(f"Updated {config_path}")
            return True

        except Exception as e:
            logger.error(f"Failed to configure config.txt: {e}")
            return False

    def install_plymouth(self) -> bool:
        """
        Install Plymouth boot splash system

        Returns:
            True if successful, False otherwise
        """
        try:
            logger.info("Checking Plymouth installation...")

            # Check if Plymouth is already installed
            if shutil.which("plymouth"):
                logger.info("Plymouth is already installed")
                return True

            # Install Plymouth
            logger.info("Installing Plymouth...")
            result = subprocess.run(
                ["apt-get", "install", "-y", "plymouth", "plymouth-themes", "pix-plym-splash"],
                capture_output=True,
                text=True,
                timeout=300
            )

            if result.returncode != 0:
                logger.error(f"Failed to install Plymouth: {result.stderr}")
                return False

            logger.info("Plymouth installed successfully")
            return True

        except subprocess.TimeoutExpired:
            logger.error("Plymouth installation timed out")
            return False
        except Exception as e:
            logger.error(f"Failed to install Plymouth: {e}")
            return False

    def configure_plymouth_theme(self) -> bool:
        """
        Configure Plymouth theme with custom logo

        Returns:
            True if successful, False otherwise
        """
        try:
            if not self.validate_logo():
                return False

            logger.info("Configuring Plymouth theme...")

            # Ensure Plymouth theme directory exists
            Path(self.PLYMOUTH_THEME_DIR).mkdir(parents=True, exist_ok=True)

            # Copy logo to Plymouth theme directory
            shutil.copy2(self.logo_path, self.PLYMOUTH_SPLASH)
            logger.info(f"Logo copied to {self.PLYMOUTH_SPLASH}")

            # Create/update pix.script with intelligent scaling
            self._write_plymouth_script()

            # Set Plymouth theme and rebuild initramfs
            result = subprocess.run(
                ["plymouth-set-default-theme", "-R", "pix"],
                capture_output=True,
                text=True,
                timeout=120
            )

            if result.returncode != 0:
                logger.error(f"Failed to set Plymouth theme: {result.stderr}")
                return False

            logger.info("Plymouth theme configured successfully")
            return True

        except Exception as e:
            logger.error(f"Failed to configure Plymouth theme: {e}")
            return False

    def _write_plymouth_script(self) -> None:
        """Write Plymouth script with intelligent image scaling"""
        script_content = """# Plymouth theme script for Digital Signage
# Intelligently scales and centers logo image

screen_width = Window.GetWidth();
screen_height = Window.GetHeight();

# Load splash image
theme_image = Image("splash.png");
image_width = theme_image.GetWidth();
image_height = theme_image.GetHeight();

# Calculate scaling factors
scale_x = image_width / screen_width;
scale_y = image_height / screen_height;

# Scale image to fit screen while maintaining aspect ratio
if (scale_x > 1 || scale_y > 1)
{
    if (scale_x > scale_y)
    {
        # Image too wide - scale by width
        resized_image = theme_image.Scale(screen_width, image_height / scale_x);
        image_x = 0;
        image_y = (screen_height - (image_height / scale_x)) / 2;
    }
    else
    {
        # Image too tall - scale by height
        resized_image = theme_image.Scale(image_width / scale_y, screen_height);
        image_x = (screen_width - (image_width / scale_y)) / 2;
        image_y = 0;
    }
}
else
{
    # Image fits - no scaling needed
    resized_image = theme_image.Scale(image_width, image_height);
    image_x = (screen_width - image_width) / 2;
    image_y = (screen_height - image_height) / 2;
}

# Display centered image
sprite = Sprite(resized_image);
sprite.SetPosition(image_x, image_y, -100);
"""

        try:
            Path(self.PLYMOUTH_SCRIPT).parent.mkdir(parents=True, exist_ok=True)
            with open(self.PLYMOUTH_SCRIPT, 'w') as f:
                f.write(script_content)
            logger.info(f"Plymouth script written to {self.PLYMOUTH_SCRIPT}")
        except Exception as e:
            logger.error(f"Failed to write Plymouth script: {e}")

    def setup_all(self) -> bool:
        """
        Setup complete boot logo system

        Returns:
            True if all steps successful, False otherwise
        """
        logger.info("Setting up boot logo system...")

        steps = [
            ("Configuring boot splash", self.setup_boot_splash),
            ("Configuring cmdline.txt", self.configure_cmdline),
            ("Configuring config.txt", self.configure_config_txt),
            ("Installing Plymouth", self.install_plymouth),
            ("Configuring Plymouth theme", self.configure_plymouth_theme),
        ]

        for step_name, step_func in steps:
            logger.info(f"\n--- {step_name} ---")
            if not step_func():
                logger.error(f"Failed: {step_name}")
                return False
            logger.info(f"Success: {step_name}")

        logger.info("\n=== Boot logo system configured successfully ===")
        logger.info("System will show the boot logo on next reboot")
        return True


def main():
    """Main entry point"""
    import argparse

    parser = argparse.ArgumentParser(
        description="Setup boot and shutdown logos for Digital Signage client"
    )
    parser.add_argument(
        "--logo",
        type=str,
        help="Path to custom logo PNG file"
    )
    parser.add_argument(
        "--boot-only",
        action="store_true",
        help="Setup boot splash only (skip Plymouth)"
    )
    parser.add_argument(
        "--debug",
        action="store_true",
        help="Enable debug logging"
    )

    args = parser.parse_args()

    # Configure logging
    level = logging.DEBUG if args.debug else logging.INFO
    logging.basicConfig(
        level=level,
        format='%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )

    # Create manager
    manager = BootLogoManager(args.logo)

    # Check root
    if not manager.require_root():
        sys.exit(1)

    # Setup
    if args.boot_only:
        success = manager.setup_boot_splash()
    else:
        success = manager.setup_all()

    sys.exit(0 if success else 1)


if __name__ == "__main__":
    main()
