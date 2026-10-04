import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { jsonResponse, mockFetch, product, renderWithProviders } from '../test/utils';
import { ProductForm } from './AdminPage';

describe('ProductForm', () => {
  it('labels the description field and fills it with AI-generated copy', async () => {
    const fetchMock = mockFetch({
      '/api/categories': () => jsonResponse([{ id: 2, name: 'Footwear', slug: 'footwear', description: null, productCount: 3 }]),
      '/api/admin/ai/product-description': () => jsonResponse({ description: 'Fresh AI copy.', provider: 'claude' }),
    });
    const user = userEvent.setup();
    renderWithProviders(<ProductForm product={product()} onClose={vi.fn()} onSaved={vi.fn()} />);

    const description = screen.getByLabelText('Description');
    expect(description.tagName).toBe('TEXTAREA');

    await user.click(screen.getByRole('button', { name: '✦ Generate with AI' }));

    expect(await screen.findByDisplayValue('Fresh AI copy.')).toBe(description);
    const call = fetchMock.mock.calls.find(([url]) => String(url).includes('/product-description'))!;
    expect(JSON.parse(call[1]!.body as string)).toMatchObject({ name: 'Trail Runner GTX', categoryId: 2, price: 149 });
  });

  it('disables AI generation until name and category are set', () => {
    mockFetch({ '/api/categories': () => jsonResponse([]) });
    renderWithProviders(<ProductForm product={null} onClose={vi.fn()} onSaved={vi.fn()} />);
    expect(screen.getByRole('button', { name: '✦ Generate with AI' })).toBeDisabled();
  });
});
