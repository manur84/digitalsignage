"""
Remote Log Handler for Digital Signage Client

Sends log messages to the server via WebSocket for remote monitoring.
"""

import logging
import json
import time
from datetime import datetime
from typing import Optional, List, Dict, Any
from threading import Thread, Lock
import queue


class RemoteLogHandler(logging.Handler):
    """
    Custom logging handler that sends logs to the server via WebSocket.
    Batches logs and sends periodically to reduce network traffic.
    """

    # Map Python log levels to application log levels
    LEVEL_MAP = {
        logging.DEBUG: "Debug",
        logging.INFO: "Info",
        logging.WARNING: "Warning",
        logging.ERROR: "Error",
        logging.CRITICAL: "Critical"
    }

    def __init__(
        self,
        websocket_client,
        client_id: str,
        batch_size: int = 50,
        batch_interval: float = 5.0,
        level=logging.INFO
    ):
        """
        Initialize the remote log handler.

        Args:
            websocket_client: WebSocket client instance with send_message method
            client_id: Unique identifier for this client
            batch_size: Number of logs to batch before sending (default: 50)
            batch_interval: Time in seconds between batch sends (default: 5.0)
            level: Minimum log level to send (default: INFO)
        """
        super().__init__(level)
        self.websocket_client = websocket_client
        self.client_id = client_id
        self.batch_size = batch_size
        self.batch_interval = batch_interval

        self.log_queue: queue.Queue = queue.Queue(maxsize=1000)
        self.batch_lock = Lock()
        self.running = True

        # Start batch sender thread
        self.sender_thread = Thread(target=self._batch_sender, daemon=True)
        self.sender_thread.start()

    def emit(self, record: logging.LogRecord) -> None:
        """
        Emit a log record by adding it to the batch queue.

        Args:
            record: The log record to emit
        """
        try:
            # Don't send logs if we're not connected
            if not hasattr(self.websocket_client, 'connected') or not self.websocket_client.connected:
                return

            # FILTER OUT zeroconf asyncio task warnings - these are harmless and clutter server logs
            message = record.getMessage()
            if 'Task was destroyed but it is pending' in message and 'zeroconf' in message.lower():
                return  # Skip this log - don't send to server

            # Also filter out related asyncio warnings from zeroconf
            if record.name == 'asyncio' and 'Task was destroyed' in message:
                return  # Skip asyncio task warnings

            log_entry = self._format_log_entry(record)

            # Add to queue (non-blocking)
            try:
                self.log_queue.put_nowait(log_entry)
            except queue.Full:
                # Queue is full, drop oldest log
                try:
                    self.log_queue.get_nowait()
                    self.log_queue.put_nowait(log_entry)
                except queue.Empty:
                    pass

        except Exception as e:
            # Avoid infinite loop by not using logging here
            print(f"Error in RemoteLogHandler.emit: {e}")

    def _format_log_entry(self, record: logging.LogRecord) -> Dict[str, Any]:
        """
        Format a log record into a dictionary for JSON serialization.

        Args:
            record: The log record to format

        Returns:
            Dictionary containing formatted log data
        """
        # Get exception info if present
        exception_text = None
        if record.exc_info:
            exception_text = self.format(record)

        return {
            "timestamp": datetime.utcnow().isoformat() + "Z",
            "level": self.LEVEL_MAP.get(record.levelno, "Info"),
            "message": record.getMessage(),
            "exception": exception_text,
            "module": record.module,
            "function": record.funcName,
            "line": record.lineno
        }

    def _batch_sender(self) -> None:
        """
        Background thread that sends batched logs periodically.
        """
        batch: List[Dict[str, Any]] = []
        last_send_time = time.time()

        while self.running:
            try:
                # Try to get a log entry (with timeout)
                try:
                    log_entry = self.log_queue.get(timeout=0.5)
                    batch.append(log_entry)
                except queue.Empty:
                    pass

                current_time = time.time()
                time_since_last_send = current_time - last_send_time

                # Send batch if it's full or enough time has passed
                should_send = (
                    len(batch) >= self.batch_size or
                    (len(batch) > 0 and time_since_last_send >= self.batch_interval)
                )

                if should_send:
                    self._send_batch(batch)
                    batch.clear()
                    last_send_time = current_time

            except Exception as e:
                print(f"Error in batch sender: {e}")
                time.sleep(1)  # Avoid tight loop on error

    def _send_batch(self, batch: List[Dict[str, Any]]) -> None:
        """
        Send a batch of log entries to the server.

        Args:
            batch: List of log entry dictionaries
        """
        if not batch:
            return

        try:
            # Send each log entry as a separate message
            for log_entry in batch:
                message = {
                    "Type": "LOG",
                    "ClientId": self.client_id,
                    "Timestamp": log_entry["timestamp"],
                    "Level": log_entry["level"],
                    "Message": log_entry["message"],
                    "Exception": log_entry.get("exception")
                }

                # Send via WebSocket
                if hasattr(self.websocket_client, 'send_message'):
                    self.websocket_client.send_message(message)

        except Exception as e:
            print(f"Error sending log batch: {e}")

    def flush(self) -> None:
        """
        Flush any pending logs immediately.
        """
        batch = []
        try:
            # Get all queued items
            while not self.log_queue.empty():
                try:
                    batch.append(self.log_queue.get_nowait())
                except queue.Empty:
                    break

            if batch:
                self._send_batch(batch)

        except Exception as e:
            print(f"Error flushing logs: {e}")

    def close(self) -> None:
        """
        Close the handler and stop the sender thread.
        """
        self.running = False
        self.flush()
        if self.sender_thread.is_alive():
            self.sender_thread.join(timeout=2.0)
        super().close()


def setup_remote_logging(
    logger: logging.Logger,
    websocket_client,
    client_id: str,
    level=logging.INFO,
    batch_size: int = 50,
    batch_interval: float = 5.0
) -> RemoteLogHandler:
    """
    Set up remote logging for a logger.

    Args:
        logger: The logger to configure
        websocket_client: WebSocket client instance
        client_id: Unique identifier for this client
        level: Minimum log level to send (default: INFO)
        batch_size: Number of logs to batch before sending (default: 50)
        batch_interval: Time in seconds between batch sends (default: 5.0)

    Returns:
        The configured RemoteLogHandler instance
    """
    handler = RemoteLogHandler(
        websocket_client=websocket_client,
        client_id=client_id,
        batch_size=batch_size,
        batch_interval=batch_interval,
        level=level
    )

    # Set format
    formatter = logging.Formatter(
        '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
    )
    handler.setFormatter(formatter)

    # Add handler to logger
    logger.addHandler(handler)

    return handler


def remove_remote_logging(logger: logging.Logger, handler: RemoteLogHandler) -> None:
    """
    Remove remote logging from a logger.

    Args:
        logger: The logger to remove the handler from
        handler: The RemoteLogHandler instance to remove
    """
    handler.close()
    logger.removeHandler(handler)
