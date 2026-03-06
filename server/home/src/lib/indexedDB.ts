/**
 * Simple IndexedDB Wrapper für Offline-Daten-Caching
 */

const DB_NAME = 'PCWaechterPortal';
const DB_VERSION = 1;
const STORES = {
  LICENSES: 'licenses',
  TICKETS: 'tickets',
  NOTIFICATIONS: 'notifications',
  USER_PREFERENCES: 'userPreferences',
} as const;

type StoreName = (typeof STORES)[keyof typeof STORES];

class IndexedDBService {
  private db: IDBDatabase | null = null;

  /**
   * Initialisiert die Datenbank
   */
  async init(): Promise<void> {
    return new Promise((resolve, reject) => {
      const request = window.indexedDB.open(DB_NAME, DB_VERSION);

      request.onerror = () => {
        console.error('IndexedDB error:', request.error);
        reject(request.error);
      };

      request.onsuccess = () => {
        this.db = request.result;
        resolve();
      };

      request.onupgradeneeded = () => {
        const db = request.result;

        // Create object stores if they don't exist
        Object.values(STORES).forEach((storeName) => {
          if (!db.objectStoreNames.contains(storeName)) {
            db.createObjectStore(storeName, { keyPath: 'id', autoIncrement: true });
          }
        });
      };
    });
  }

  /**
   * Speichert Daten in IndexedDB
   */
  async setItem<T>(storeName: StoreName, key: string, value: T): Promise<void> {
    if (!this.db) await this.init();

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readwrite');
      const store = transaction.objectStore(storeName);
      const request = store.put({ id: key, data: value, timestamp: Date.now() });

      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Liest Daten aus IndexedDB
   */
  async getItem<T>(storeName: StoreName, key: string): Promise<T | null> {
    if (!this.db) await this.init();

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readonly');
      const store = transaction.objectStore(storeName);
      const request = store.get(key);

      request.onsuccess = () => {
        const result = request.result;
        if (result && result.data) {
          resolve(result.data as T);
        } else {
          resolve(null);
        }
      };
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Löscht einen Eintrag
   */
  async removeItem(storeName: StoreName, key: string): Promise<void> {
    if (!this.db) await this.init();

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readwrite');
      const store = transaction.objectStore(storeName);
      const request = store.delete(key);

      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Löscht alle Einträge in einem Store
   */
  async clear(storeName: StoreName): Promise<void> {
    if (!this.db) await this.init();

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readwrite');
      const store = transaction.objectStore(storeName);
      const request = store.clear();

      request.onsuccess = () => resolve();
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Gibt alle Keys in einem Store zurück
   */
  async getAllKeys(storeName: StoreName): Promise<string[]> {
    if (!this.db) await this.init();

    return new Promise((resolve, reject) => {
      const transaction = this.db!.transaction([storeName], 'readonly');
      const store = transaction.objectStore(storeName);
      const request = store.getAllKeys();

      request.onsuccess = () => {
        resolve(request.result as string[]);
      };
      request.onerror = () => reject(request.error);
    });
  }

  /**
   * Prüft ob ein Eintrag existiert
   */
  async hasItem(storeName: StoreName, key: string): Promise<boolean> {
    const item = await this.getItem(storeName, key);
    return item !== null;
  }
}

// Singleton instance
export const indexedDB = new IndexedDBService();
export { STORES };

/**
 * React Hook für IndexedDB Caching
 */
export function useIndexedDBCache<T>(storeName: StoreName, key: string) {
  const [data, setData] = React.useState<T | null>(null);
  const [loading, setLoading] = React.useState(true);
  const [error, setError] = React.useState<Error | null>(null);

  React.useEffect(() => {
    indexedDB
      .getItem<T>(storeName, key)
      .then((cachedData) => {
        setData(cachedData);
        setLoading(false);
      })
      .catch((err) => {
        setError(err);
        setLoading(false);
      });
  }, [storeName, key]);

  const updateCache = React.useCallback(
    async (newData: T) => {
      try {
        await indexedDB.setItem(storeName, key, newData);
        setData(newData);
      } catch (err) {
        setError(err as Error);
      }
    },
    [storeName, key]
  );

  const clearCache = React.useCallback(async () => {
    try {
      await indexedDB.removeItem(storeName, key);
      setData(null);
    } catch (err) {
      setError(err as Error);
    }
  }, [storeName, key]);

  return {
    data,
    loading,
    error,
    updateCache,
    clearCache,
  };
}

// Note: React import would be needed in actual usage
import * as React from 'react';
