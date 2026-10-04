import type { Product } from '../api/types';

export const MAX_QUANTITY_PER_ITEM = 20;

export interface CartLine {
  productId: number;
  name: string;
  category: string;
  price: number;
  stock: number;
  quantity: number;
}

export type CartAction =
  | { type: 'add'; product: Product; quantity?: number }
  | { type: 'setQuantity'; productId: number; quantity: number }
  | { type: 'remove'; productId: number }
  | { type: 'clear' };

const limitFor = (stock: number) => Math.min(stock, MAX_QUANTITY_PER_ITEM);

export function cartReducer(lines: CartLine[], action: CartAction): CartLine[] {
  switch (action.type) {
    case 'add': {
      const { product } = action;
      const qty = action.quantity ?? 1;
      const existing = lines.find((l) => l.productId === product.id);
      if (existing) {
        return lines.map((l) =>
          l.productId === product.id
            ? { ...l, price: product.price, stock: product.stockQuantity, quantity: Math.min(l.quantity + qty, limitFor(product.stockQuantity)) }
            : l,
        );
      }
      if (product.stockQuantity <= 0) return lines;
      return [
        ...lines,
        {
          productId: product.id,
          name: product.name,
          category: product.category,
          price: product.price,
          stock: product.stockQuantity,
          quantity: Math.min(qty, limitFor(product.stockQuantity)),
        },
      ];
    }
    case 'setQuantity':
      if (action.quantity <= 0) return lines.filter((l) => l.productId !== action.productId);
      return lines.map((l) =>
        l.productId === action.productId ? { ...l, quantity: Math.min(action.quantity, limitFor(l.stock)) } : l,
      );
    case 'remove':
      return lines.filter((l) => l.productId !== action.productId);
    case 'clear':
      return [];
  }
}

export const cartSubtotal = (lines: CartLine[]) =>
  Math.round(lines.reduce((sum, l) => sum + l.price * l.quantity, 0) * 100) / 100;

export const cartCount = (lines: CartLine[]) => lines.reduce((sum, l) => sum + l.quantity, 0);
