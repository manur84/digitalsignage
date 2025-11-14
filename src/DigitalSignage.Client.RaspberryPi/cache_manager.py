#!/usr/bin/env python3
"""
Cache Manager for Digital Signage Client
Handles local caching of layouts and data using SQLite for offline operation
"""

import json
import sqlite3
import logging
from pathlib import Path
from typing import Optional, Dict, Any, Tuple, List
from datetime import datetime

logger = logging.getLogger(__name__)


class CacheManager:
    """Manages local cache using SQLite database"""

    def __init__(self, cache_dir: Optional[Path] = None):
        """
        Initialize cache manager

        Args:
            cache_dir: Directory to store cache database (default: ~/.digitalsignage/cache)
        """
        if cache_dir is None:
            cache_dir = Path.home() / ".digitalsignage" / "cache"

        self.cache_dir = cache_dir
        self.db_path = self.cache_dir / "offline_cache.db"

        # Ensure cache directory exists
        try:
            self.cache_dir.mkdir(parents=True, exist_ok=True)
            logger.info(f"Cache directory: {self.cache_dir}")
        except Exception as e:
            logger.error(f"Failed to create cache directory: {e}")
            raise

        # Initialize database
        self._init_database()

    def _init_database(self):
        """Initialize SQLite database with required tables"""
        try:
            conn = sqlite3.connect(str(self.db_path))
            cursor = conn.cursor()

            # Create layouts table
            cursor.execute("""
                CREATE TABLE IF NOT EXISTS layouts (
                    id TEXT PRIMARY KEY,
                    name TEXT,
                    layout_json TEXT NOT NULL,
                    cached_at TEXT NOT NULL,
                    is_current INTEGER DEFAULT 0
                )
            """)

            # Create layout_data table
            cursor.execute("""
                CREATE TABLE IF NOT EXISTS layout_data (
                    layout_id TEXT NOT NULL,
                    data_source_id TEXT NOT NULL,
                    data_json TEXT NOT NULL,
                    cached_at TEXT NOT NULL,
                    PRIMARY KEY (layout_id, data_source_id)
                )
            """)

            # Create cache metadata table
            cursor.execute("""
                CREATE TABLE IF NOT EXISTS cache_metadata (
                    key TEXT PRIMARY KEY,
                    value TEXT NOT NULL,
                    updated_at TEXT NOT NULL
                )
            """)

            conn.commit()
            conn.close()

            logger.info("Cache database initialized successfully")
        except Exception as e:
            logger.error(f"Failed to initialize cache database: {e}")
            raise

    def save_layout(
        self,
        layout: Dict[str, Any],
        layout_data: Optional[Dict[str, Any]] = None,
        set_current: bool = True
    ) -> bool:
        """
        Save layout and data to cache

        Args:
            layout: Layout dictionary
            layout_data: Layout data dictionary (data source ID -> data)
            set_current: Mark this layout as the current active layout

        Returns:
            True if successful, False otherwise
        """
        try:
            if not layout:
                logger.warning("Cannot save empty layout to cache")
                return False

            layout_id = layout.get("Id")
            if not layout_id:
                logger.warning("Layout missing ID, cannot cache")
                return False

            layout_name = layout.get("Name", "Unnamed Layout")
            layout_json = json.dumps(layout)
            cached_at = datetime.utcnow().isoformat()

            conn = sqlite3.connect(str(self.db_path))
            cursor = conn.cursor()

            # If setting as current, mark all others as not current
            if set_current:
                cursor.execute("UPDATE layouts SET is_current = 0")

            # Insert or replace layout
            cursor.execute("""
                INSERT OR REPLACE INTO layouts (id, name, layout_json, cached_at, is_current)
                VALUES (?, ?, ?, ?, ?)
            """, (layout_id, layout_name, layout_json, cached_at, 1 if set_current else 0))

            # Save layout data if provided
            if layout_data:
                for data_source_id, data in layout_data.items():
                    try:
                        data_json = json.dumps(data)
                        cursor.execute("""
                            INSERT OR REPLACE INTO layout_data (layout_id, data_source_id, data_json, cached_at)
                            VALUES (?, ?, ?, ?)
                        """, (layout_id, data_source_id, data_json, cached_at))
                    except Exception as e:
                        logger.warning(f"Failed to cache data for source {data_source_id}: {e}")

            conn.commit()
            conn.close()

            logger.info(f"Layout '{layout_name}' (ID: {layout_id}) cached successfully")
            return True

        except Exception as e:
            logger.error(f"Failed to save layout to cache: {e}", exc_info=True)
            return False

    def get_current_layout(self) -> Optional[Tuple[Dict[str, Any], Dict[str, Any]]]:
        """
        Get the current active layout and its data from cache

        Returns:
            Tuple of (layout, layout_data) or None if no cached layout exists
        """
        try:
            conn = sqlite3.connect(str(self.db_path))
            cursor = conn.cursor()

            # Get current layout
            cursor.execute("""
                SELECT id, layout_json, cached_at
                FROM layouts
                WHERE is_current = 1
                ORDER BY cached_at DESC
                LIMIT 1
            """)

            row = cursor.fetchone()
            if not row:
                logger.info("No cached layout found")
                conn.close()
                return None

            layout_id, layout_json, cached_at = row

            try:
                layout = json.loads(layout_json)
            except json.JSONDecodeError as e:
                logger.error(f"Failed to parse cached layout JSON: {e}")
                conn.close()
                return None

            # Get layout data
            cursor.execute("""
                SELECT data_source_id, data_json
                FROM layout_data
                WHERE layout_id = ?
            """, (layout_id,))

            layout_data = {}
            for data_row in cursor.fetchall():
                data_source_id, data_json = data_row
                try:
                    layout_data[data_source_id] = json.loads(data_json)
                except json.JSONDecodeError as e:
                    logger.warning(f"Failed to parse cached data for source {data_source_id}: {e}")
                    layout_data[data_source_id] = {}

            conn.close()

            logger.info(f"Loaded cached layout '{layout.get('Name')}' from {cached_at}")
            return (layout, layout_data)

        except Exception as e:
            logger.error(f"Failed to get cached layout: {e}", exc_info=True)
            return None

    def get_layout_by_id(self, layout_id: str) -> Optional[Tuple[Dict[str, Any], Dict[str, Any]]]:
        """
        Get a specific layout and its data by ID

        Args:
            layout_id: Layout ID to retrieve

        Returns:
            Tuple of (layout, layout_data) or None if not found
        """
        try:
            conn = sqlite3.connect(str(self.db_path))
            cursor = conn.cursor()

            cursor.execute("""
                SELECT layout_json
                FROM layouts
                WHERE id = ?
            """, (layout_id,))

            row = cursor.fetchone()
            if not row:
                logger.info(f"Layout {layout_id} not found in cache")
                conn.close()
                return None

            layout_json = row[0]

            try:
                layout = json.loads(layout_json)
            except json.JSONDecodeError as e:
                logger.error(f"Failed to parse cached layout JSON: {e}")
                conn.close()
                return None

            # Get layout data
            cursor.execute("""
                SELECT data_source_id, data_json
                FROM layout_data
                WHERE layout_id = ?
            """, (layout_id,))

            layout_data = {}
            for data_row in cursor.fetchall():
                data_source_id, data_json = data_row
                try:
                    layout_data[data_source_id] = json.loads(data_json)
                except json.JSONDecodeError as e:
                    logger.warning(f"Failed to parse cached data for source {data_source_id}: {e}")
                    layout_data[data_source_id] = {}

            conn.close()

            logger.info(f"Loaded cached layout '{layout.get('Name')}' (ID: {layout_id})")
            return (layout, layout_data)

        except Exception as e:
            logger.error(f"Failed to get cached layout by ID: {e}", exc_info=True)
            return None

    def clear_cache(self) -> bool:
        """
        Clear all cached data

        Returns:
            True if successful, False otherwise
        """
        try:
            conn = sqlite3.connect(str(self.db_path))
            cursor = conn.cursor()

            cursor.execute("DELETE FROM layouts")
            cursor.execute("DELETE FROM layout_data")
            cursor.execute("DELETE FROM cache_metadata")

            conn.commit()
            conn.close()

            logger.info("Cache cleared successfully")
            return True

        except Exception as e:
            logger.error(f"Failed to clear cache: {e}", exc_info=True)
            return False

    def get_cache_info(self) -> Dict[str, Any]:
        """
        Get cache statistics

        Returns:
            Dictionary with cache information
        """
        try:
            conn = sqlite3.connect(str(self.db_path))
            cursor = conn.cursor()

            # Count layouts
            cursor.execute("SELECT COUNT(*) FROM layouts")
            layout_count = cursor.fetchone()[0]

            # Get current layout info
            cursor.execute("""
                SELECT id, name, cached_at
                FROM layouts
                WHERE is_current = 1
            """)
            current_layout = cursor.fetchone()

            # Get database size
            import os
            db_size = os.path.getsize(self.db_path) if self.db_path.exists() else 0

            conn.close()

            return {
                "layout_count": layout_count,
                "current_layout_id": current_layout[0] if current_layout else None,
                "current_layout_name": current_layout[1] if current_layout else None,
                "current_layout_cached_at": current_layout[2] if current_layout else None,
                "database_size_bytes": db_size,
                "database_path": str(self.db_path)
            }

        except Exception as e:
            logger.error(f"Failed to get cache info: {e}", exc_info=True)
            return {
                "error": str(e)
            }

    def set_metadata(self, key: str, value: str) -> bool:
        """
        Set cache metadata value

        Args:
            key: Metadata key
            value: Metadata value

        Returns:
            True if successful, False otherwise
        """
        try:
            conn = sqlite3.connect(str(self.db_path))
            cursor = conn.cursor()

            updated_at = datetime.utcnow().isoformat()

            cursor.execute("""
                INSERT OR REPLACE INTO cache_metadata (key, value, updated_at)
                VALUES (?, ?, ?)
            """, (key, value, updated_at))

            conn.commit()
            conn.close()

            return True

        except Exception as e:
            logger.error(f"Failed to set metadata: {e}", exc_info=True)
            return False

    def get_metadata(self, key: str) -> Optional[str]:
        """
        Get cache metadata value

        Args:
            key: Metadata key

        Returns:
            Metadata value or None if not found
        """
        try:
            conn = sqlite3.connect(str(self.db_path))
            cursor = conn.cursor()

            cursor.execute("""
                SELECT value
                FROM cache_metadata
                WHERE key = ?
            """, (key,))

            row = cursor.fetchone()
            conn.close()

            return row[0] if row else None

        except Exception as e:
            logger.error(f"Failed to get metadata: {e}", exc_info=True)
            return None

    def get_all_layouts(self) -> List[Dict[str, Any]]:
        """
        Get list of all cached layouts with metadata

        Returns:
            List of layout metadata dictionaries
        """
        try:
            conn = sqlite3.connect(str(self.db_path))
            cursor = conn.cursor()

            cursor.execute("""
                SELECT id, name, cached_at, is_current
                FROM layouts
                ORDER BY cached_at DESC
            """)

            layouts = []
            for row in cursor.fetchall():
                layout_id, name, cached_at, is_current = row
                layouts.append({
                    'id': layout_id,
                    'name': name if name else 'Unnamed Layout',
                    'cached_at': cached_at,
                    'is_current': bool(is_current)
                })

            conn.close()

            logger.debug(f"Retrieved {len(layouts)} cached layouts")
            return layouts

        except Exception as e:
            logger.error(f"Failed to get all layouts: {e}", exc_info=True)
            return []

    def set_current_layout(self, layout_id: str) -> bool:
        """
        Set a specific layout as the current active layout

        Args:
            layout_id: Layout ID to set as current

        Returns:
            True if successful, False otherwise
        """
        try:
            conn = sqlite3.connect(str(self.db_path))
            cursor = conn.cursor()

            # Check if layout exists
            cursor.execute("SELECT id FROM layouts WHERE id = ?", (layout_id,))
            if not cursor.fetchone():
                logger.warning(f"Layout {layout_id} not found in cache")
                conn.close()
                return False

            # Mark all layouts as not current
            cursor.execute("UPDATE layouts SET is_current = 0")

            # Mark specified layout as current
            cursor.execute("UPDATE layouts SET is_current = 1 WHERE id = ?", (layout_id,))

            conn.commit()
            conn.close()

            logger.info(f"Set layout {layout_id} as current")
            return True

        except Exception as e:
            logger.error(f"Failed to set current layout: {e}", exc_info=True)
            return False
