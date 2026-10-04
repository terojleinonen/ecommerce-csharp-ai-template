import { describe, expect, it } from 'vitest';
import { formatPrice, shippingFor } from './format';

describe('format helpers', () => {
  it('formats euro prices', () => {
    expect(formatPrice(1234.5)).toBe('€1,234.50');
  });

  it('mirrors the server shipping rule', () => {
    expect(shippingFor(0)).toBe(0);
    expect(shippingFor(49.99)).toBe(4.9);
    expect(shippingFor(50)).toBe(0);
  });
});
