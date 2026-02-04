import { sha256 } from '@noble/hashes/sha256';
import { bytesToHex } from '@noble/hashes/utils';

const DEVICE_ID_KEY = 'novatune_device_id';

/**
 * Gets or creates a persistent device ID for this installation.
 * The device ID is stored in localStorage and persists across sessions.
 */
export function getOrCreateDeviceId(): string {
  let deviceId = localStorage.getItem(DEVICE_ID_KEY);
  if (!deviceId) {
    deviceId = crypto.randomUUID();
    localStorage.setItem(DEVICE_ID_KEY, deviceId);
  }
  return deviceId;
}

/**
 * Hashes a device ID using SHA-256 for telemetry purposes.
 * This ensures privacy while still allowing device-level analytics.
 */
export function hashDeviceId(deviceId: string): string {
  return bytesToHex(sha256(new TextEncoder().encode(deviceId)));
}

/**
 * Gets the hashed device ID for telemetry.
 */
export function getHashedDeviceId(): string {
  return hashDeviceId(getOrCreateDeviceId());
}
