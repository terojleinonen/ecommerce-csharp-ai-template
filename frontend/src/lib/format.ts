const currency = new Intl.NumberFormat('en-IE', { style: 'currency', currency: 'EUR' });
const date = new Intl.DateTimeFormat('en-GB', { dateStyle: 'medium', timeStyle: 'short' });

export const formatPrice = (value: number) => currency.format(value);
export const formatDate = (iso: string) => date.format(new Date(iso));

export const FREE_SHIPPING_THRESHOLD = 50;
export const STANDARD_SHIPPING = 4.9;

/** Mirrors the server's pricing rule so the cart can preview totals. The server stays authoritative. */
export const shippingFor = (subtotal: number) =>
  subtotal === 0 || subtotal >= FREE_SHIPPING_THRESHOLD ? 0 : STANDARD_SHIPPING;

export function readStorage<T>(storage: Storage, key: string, fallback: T): T {
  try {
    const raw = storage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

export function writeStorage(storage: Storage, key: string, value: unknown) {
  try {
    storage.setItem(key, JSON.stringify(value));
  } catch {
    // Storage full or blocked (private mode): the app keeps working in memory.
  }
}
