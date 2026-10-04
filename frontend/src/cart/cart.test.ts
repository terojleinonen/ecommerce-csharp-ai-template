import { describe, expect, it } from 'vitest';
import { product } from '../test/utils';
import { cartCount, cartReducer, cartSubtotal, MAX_QUANTITY_PER_ITEM, type CartLine } from './cart';

describe('cartReducer', () => {
  it('adds a new product and merges repeated adds', () => {
    let lines: CartLine[] = cartReducer([], { type: 'add', product: product() });
    lines = cartReducer(lines, { type: 'add', product: product(), quantity: 2 });

    expect(lines).toHaveLength(1);
    expect(lines[0]!.quantity).toBe(3);
  });

  it('never exceeds stock or the per-item limit', () => {
    const lowStock = product({ stockQuantity: 2 });
    expect(cartReducer([], { type: 'add', product: lowStock, quantity: 5 })[0]!.quantity).toBe(2);

    const plenty = product({ stockQuantity: 500 });
    const lines = cartReducer([], { type: 'add', product: plenty, quantity: 99 });
    expect(lines[0]!.quantity).toBe(MAX_QUANTITY_PER_ITEM);
  });

  it('ignores out-of-stock products', () => {
    expect(cartReducer([], { type: 'add', product: product({ stockQuantity: 0 }) })).toEqual([]);
  });

  it('removes a line when quantity is set to zero', () => {
    const lines = cartReducer([], { type: 'add', product: product() });
    expect(cartReducer(lines, { type: 'setQuantity', productId: 1, quantity: 0 })).toEqual([]);
  });

  it('computes subtotal without floating point drift', () => {
    let lines = cartReducer([], { type: 'add', product: product({ id: 1, price: 0.1 }) });
    lines = cartReducer(lines, { type: 'add', product: product({ id: 2, price: 0.2 }) });
    expect(cartSubtotal(lines)).toBe(0.3);
    expect(cartCount(lines)).toBe(2);
  });
});
