import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { jsonResponse, mockFetch, product, renderWithProviders } from '../test/utils';
import { CatalogPage } from './CatalogPage';

const page = (items = [product()]) => ({ items, page: 1, pageSize: 12, totalCount: items.length, totalPages: 1, hasNextPage: false });

describe('CatalogPage', () => {
  it('renders products and filters by category', async () => {
    const fetchMock = mockFetch({
      '/api/categories': () => jsonResponse([{ id: 2, name: 'Footwear', slug: 'footwear', description: null, productCount: 3 }]),
      '/api/products': () => jsonResponse(page()),
    });
    const user = userEvent.setup();
    renderWithProviders(<CatalogPage />);

    expect(await screen.findByRole('heading', { name: 'Trail Runner GTX' })).toBeInTheDocument();

    await user.click(await screen.findByRole('button', { name: /Footwear/ }));

    const urls = fetchMock.mock.calls.map(([url]) => String(url));
    expect(urls.some((u) => u.includes('/api/products?') && u.includes('category=footwear'))).toBe(true);
  });

  it('adds products to the cart', async () => {
    mockFetch({
      '/api/categories': () => jsonResponse([]),
      '/api/products': () => jsonResponse(page([product(), product({ id: 2, name: 'Winter Boot', stockQuantity: 0 })])),
    });
    const user = userEvent.setup();
    renderWithProviders(<CatalogPage />);

    await user.click(await screen.findByRole('button', { name: 'Add Trail Runner GTX to cart' }));

    expect(screen.getByRole('button', { name: 'Add Winter Boot to cart' })).toBeDisabled();
    expect(JSON.parse(localStorage.getItem('shopsense.cart')!)).toMatchObject([{ productId: 1, quantity: 1 }]);
  });

  it('shows an empty state', async () => {
    mockFetch({ '/api/categories': () => jsonResponse([]), '/api/products': () => jsonResponse(page([])) });
    renderWithProviders(<CatalogPage />, { route: '/?search=unicorn' });
    expect(await screen.findByText('No products found')).toBeInTheDocument();
  });
});
