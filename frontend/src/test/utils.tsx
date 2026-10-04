import type { ReactElement } from 'react';
import { render } from '@testing-library/react';
import { MemoryRouter } from 'react-router';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { vi } from 'vitest';
import { AuthProvider } from '../auth/AuthProvider';
import { CartProvider } from '../cart/CartProvider';
import type { Product } from '../api/types';

export function renderWithProviders(ui: ReactElement, { route = '/' }: { route?: string } = {}) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false }, mutations: { retry: false } } });
  return render(
    <QueryClientProvider client={queryClient}>
      <MemoryRouter initialEntries={[route]}>
        <AuthProvider>
          <CartProvider>{ui}</CartProvider>
        </AuthProvider>
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

export const product = (overrides: Partial<Product> = {}): Product => ({
  id: 1,
  sku: 'FTW-RUN-001',
  name: 'Trail Runner GTX',
  slug: 'trail-runner-gtx',
  description: 'Waterproof trail running shoe.',
  price: 149,
  imageUrl: null,
  categoryId: 2,
  category: 'Footwear',
  stockQuantity: 10,
  isActive: true,
  ...overrides,
});

export const jsonResponse = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

/** Routes fetch calls by URL substring → response factory. */
export function mockFetch(routes: Record<string, (init?: RequestInit) => Response>) {
  return vi.spyOn(globalThis, 'fetch').mockImplementation(async (input, init) => {
    const url = typeof input === 'string' ? input : input instanceof URL ? input.href : input.url;
    const match = Object.keys(routes)
      .sort((a, b) => b.length - a.length)
      .find((k) => url.includes(k));
    if (!match) throw new Error(`Unmocked fetch: ${url}`);
    return routes[match]!(init);
  });
}
