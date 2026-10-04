import { useState, type FormEvent } from 'react';
import { Link, NavLink, Outlet, useNavigate, useSearchParams } from 'react-router';
import { useAuth } from '../auth/context';
import { useCart } from '../cart/context';
import { ChatAssistant } from './ChatAssistant';

export function Layout() {
  const { user, isAdmin, logout } = useAuth();
  const { count } = useCart();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const [search, setSearch] = useState(params.get('search') ?? '');

  const onSearch = (e: FormEvent) => {
    e.preventDefault();
    const q = search.trim();
    navigate(q ? `/?search=${encodeURIComponent(q)}` : '/');
  };

  return (
    <div className="app">
      <a href="#main" className="skip-link">
        Skip to content
      </a>
      <header className="header">
        <div className="header__inner container">
          <Link to="/" className="brand" aria-label="ShopSense home">
            <img src="/favicon.svg" alt="" width={28} height={28} />
            <span>ShopSense</span>
          </Link>

          <form className="search" role="search" onSubmit={onSearch}>
            <input
              type="search"
              placeholder="Search products…"
              aria-label="Search products"
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </form>

          <nav className="nav" aria-label="Main">
            {isAdmin && <NavLink to="/admin">Admin</NavLink>}
            {user ? (
              <>
                <NavLink to="/orders">Orders</NavLink>
                <button type="button" className="link-button" onClick={logout} title={user.email}>
                  Sign out
                </button>
              </>
            ) : (
              <NavLink to="/login">Sign in</NavLink>
            )}
            <NavLink to="/cart" className="cart-link" aria-label={`Cart, ${count} items`}>
              <svg viewBox="0 0 24 24" width="22" height="22" fill="none" stroke="currentColor" strokeWidth="1.8" aria-hidden="true">
                <path d="M3 4h2l2.4 11.2a2 2 0 0 0 2 1.6h7.7a2 2 0 0 0 2-1.5L21 8H6" />
                <circle cx="10" cy="20" r="1.3" />
                <circle cx="17" cy="20" r="1.3" />
              </svg>
              {count > 0 && <span className="cart-link__count">{count}</span>}
            </NavLink>
          </nav>
        </div>
      </header>

      <main id="main" className="container main">
        <Outlet />
      </main>

      <footer className="footer">
        <div className="container">
          <span>ShopSense demo store · React + ASP.NET Core + Claude</span>
          <span>Free shipping over €50 · 30-day returns</span>
        </div>
      </footer>

      <ChatAssistant />
    </div>
  );
}
