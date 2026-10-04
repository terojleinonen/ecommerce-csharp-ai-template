import { afterEach, describe, expect, it, vi } from 'vitest';
import { jsonResponse, mockFetch } from '../test/utils';
import { ApiError, apiRequest, buildQuery, setAccessToken, setUnauthorizedHandler } from './client';

afterEach(() => {
  setAccessToken(null);
  setUnauthorizedHandler(null);
});

describe('buildQuery', () => {
  it('skips empty values', () => {
    expect(buildQuery({ search: 'shoes', category: '', page: 2, inStock: undefined })).toBe('?search=shoes&page=2');
    expect(buildQuery({})).toBe('');
  });
});

describe('apiRequest', () => {
  it('sends the bearer token and JSON body', async () => {
    const fetchMock = mockFetch({ '/api/orders': () => jsonResponse({ id: 'o1' }, 201) });
    setAccessToken('token-123');

    const result = await apiRequest<{ id: string }>('/api/orders', { method: 'POST', body: { a: 1 } });

    expect(result.id).toBe('o1');
    const init = fetchMock.mock.calls[0]![1]!;
    expect((init.headers as Record<string, string>).Authorization).toBe('Bearer token-123');
    expect(init.body).toBe('{"a":1}');
  });

  it('converts problem details into ApiError with field errors', async () => {
    mockFetch({
      '/api/auth/register': () =>
        jsonResponse({ title: 'Validation', status: 400, errors: { Email: ['Invalid email.'], 'ShippingAddress.City': ['Required'] } }, 400),
    });

    const error = await apiRequest('/api/auth/register', { method: 'POST', body: {} }).catch((e: unknown) => e);

    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).fieldErrors).toEqual({ email: 'Invalid email.', 'shippingAddress.City': 'Required' });
  });

  it('calls the unauthorized handler on 401 when signed in', async () => {
    mockFetch({ '/api/orders': () => jsonResponse({ title: 'Unauthorized' }, 401) });
    const onUnauthorized = vi.fn();
    setUnauthorizedHandler(onUnauthorized);
    setAccessToken('expired');

    await expect(apiRequest('/api/orders')).rejects.toBeInstanceOf(ApiError);
    expect(onUnauthorized).toHaveBeenCalledOnce();
  });

  it('reports network failures as status 0', async () => {
    vi.spyOn(globalThis, 'fetch').mockRejectedValue(new TypeError('Failed to fetch'));
    await expect(apiRequest('/api/products')).rejects.toMatchObject({ status: 0 });
  });
});
