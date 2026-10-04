import { useEffect, useMemo, useReducer, type ReactNode } from 'react';
import { readStorage, writeStorage } from '../lib/format';
import { cartCount, cartReducer, cartSubtotal, type CartLine } from './cart';
import { CartContext, type CartState } from './context';

const STORAGE_KEY = 'shopsense.cart';

export function CartProvider({ children }: { children: ReactNode }) {
  const [lines, dispatch] = useReducer(cartReducer, undefined, () =>
    readStorage<CartLine[]>(localStorage, STORAGE_KEY, []),
  );

  useEffect(() => writeStorage(localStorage, STORAGE_KEY, lines), [lines]);

  const value = useMemo<CartState>(
    () => ({
      lines,
      count: cartCount(lines),
      subtotal: cartSubtotal(lines),
      add: (product, quantity) => dispatch({ type: 'add', product, quantity }),
      setQuantity: (productId, quantity) => dispatch({ type: 'setQuantity', productId, quantity }),
      remove: (productId) => dispatch({ type: 'remove', productId }),
      clear: () => dispatch({ type: 'clear' }),
    }),
    [lines],
  );

  return <CartContext.Provider value={value}>{children}</CartContext.Provider>;
}
