import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Route, Routes } from 'react-router';
import { describe, expect, it } from 'vitest';
import { RequireAuth } from '../components/RequireAuth';
import { jsonResponse, mockFetch, renderWithProviders } from '../test/utils';
import { LoginPage } from './AuthPages';

const routes = (
  <Routes>
    <Route path="/login" element={<LoginPage />} />
    <Route
      path="/orders"
      element={
        <RequireAuth>
          <p>Secret orders</p>
        </RequireAuth>
      }
    />
  </Routes>
);

describe('authentication flow', () => {
  it('redirects to login and back after signing in', async () => {
    mockFetch({
      '/api/auth/login': () =>
        jsonResponse({
          accessToken: 'jwt',
          expiresAt: new Date(Date.now() + 3600_000).toISOString(),
          user: { id: 'u1', email: 'a@b.dev', displayName: 'Ada', role: 'customer' },
        }),
    });
    const user = userEvent.setup();
    renderWithProviders(routes, { route: '/orders' });

    expect(await screen.findByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
    await user.type(screen.getByLabelText('Email'), 'a@b.dev');
    await user.type(screen.getByLabelText('Password'), 'Password-123');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByText('Secret orders')).toBeInTheDocument();
    expect(JSON.parse(localStorage.getItem('shopsense.auth')!).accessToken).toBe('jwt');
  });

  it('shows the server error on bad credentials', async () => {
    mockFetch({ '/api/auth/login': () => jsonResponse({ title: 'Authentication failed', detail: 'Invalid email or password.' }, 401) });
    const user = userEvent.setup();
    renderWithProviders(routes, { route: '/login' });

    await user.type(screen.getByLabelText('Email'), 'a@b.dev');
    await user.type(screen.getByLabelText('Password'), 'nope');
    await user.click(screen.getByRole('button', { name: 'Sign in' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Invalid email or password.');
  });

  it('ignores expired stored sessions', () => {
    localStorage.setItem(
      'shopsense.auth',
      JSON.stringify({ accessToken: 'old', expiresAt: new Date(Date.now() - 1000).toISOString(), user: { id: 'u1' } }),
    );
    renderWithProviders(routes, { route: '/orders' });
    expect(screen.getByRole('heading', { name: 'Sign in' })).toBeInTheDocument();
  });
});
