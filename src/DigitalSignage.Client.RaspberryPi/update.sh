#!/bin/bash

# Digital Signage Client - Update Script (DEPRECATED)
# This script is deprecated. Use install.sh instead, which handles both install and update.

echo "════════════════════════════════════════════════════════════"
echo "  Digital Signage Client - Update Script"
echo "════════════════════════════════════════════════════════════"
echo ""
echo "⚠️  WARNING: This script is DEPRECATED!"
echo ""
echo "The update.sh script has been merged into install.sh"
echo "install.sh now intelligently detects whether to install or update."
echo ""
echo "Please use install.sh instead:"
echo "  sudo ./install.sh"
echo ""
echo "Redirecting to install.sh in 3 seconds..."
echo ""

sleep 3

# Get the directory where this script is located
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Execute install.sh with all arguments passed to this script
exec "$SCRIPT_DIR/install.sh" "$@"
