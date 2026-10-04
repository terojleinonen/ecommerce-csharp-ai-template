import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/endpoints';
import { useCart } from '../cart/context';
import { MAX_QUANTITY_PER_ITEM } from '../cart/cart';
import { ProductArt } from '../components/ProductArt';
import { ProductCard } from '../components/ProductCard';
import { ErrorState, Spinner } from '../components/States';
import { StockBadge } from '../components/StockBadge';
import { formatPrice } from '../lib/format';

export function ProductPage() {
  const id = Number(useParams().id);
  const cart = useCart();
  const [quantity, setQuantity] = useState(1);
  const [added, setAdded] = useState(false);

  const categories = useQuery({ queryKey: ['categories'], queryFn: ({ signal }) => api.categories(signal) });
  const product = useQuery({ queryKey: ['product', id], queryFn: ({ signal }) => api.product(id, signal), enabled: id > 0 });
  const recs = useQuery({
    queryKey: ['recommendations', id],
    queryFn: ({ signal }) => api.recommendations(id, signal),
    enabled: product.isSuccess,
  });

  if (product.isPending) return <Spinner />;
  if (product.isError) return <ErrorState error={product.error} />;

  const p = product.data;
  const max = Math.min(p.stockQuantity, MAX_QUANTITY_PER_ITEM);
  const categorySlug = categories.data?.find((c) => c.id === p.categoryId)?.slug;

  return (
    <div className="product-page">
      <nav className="breadcrumbs" aria-label="Breadcrumb">
        <Link to="/">Shop</Link> / <Link to={categorySlug ? `/?category=${categorySlug}` : '/'}>{p.category}</Link> /{' '}
        <span>{p.name}</span>
      </nav>

      <div className="product-detail">
        <ProductArt product={p} size="lg" />
        <div className="product-detail__info">
          <span className="product-card__category">{p.category}</span>
          <h1>{p.name}</h1>
          <p className="price price--lg">{formatPrice(p.price)}</p>
          <StockBadge stock={p.stockQuantity} />
          <p className="product-detail__desc">{p.description}</p>

          <div className="buy-box">
            <label>
              Quantity
              <select value={quantity} onChange={(e) => setQuantity(Number(e.target.value))} disabled={max === 0}>
                {Array.from({ length: Math.max(max, 1) }, (_, i) => i + 1).map((n) => (
                  <option key={n} value={n}>
                    {n}
                  </option>
                ))}
              </select>
            </label>
            <button
              type="button"
              className="btn btn--primary btn--lg"
              disabled={max === 0}
              onClick={() => {
                cart.add(p, quantity);
                setAdded(true);
              }}
            >
              {max === 0 ? 'Out of stock' : 'Add to cart'}
            </button>
          </div>
          {added && (
            <p className="notice" role="status">
              Added to cart. <Link to="/cart">View cart →</Link>
            </p>
          )}
          <p className="muted">SKU {p.sku} · Free shipping over €50 · 30-day returns</p>
        </div>
      </div>

      {recs.data && recs.data.length > 0 && (
        <section className="recommendations">
          <h2>Frequently bought together &amp; similar</h2>
          <div className="product-grid product-grid--compact">
            {recs.data.map((r) => (
              <ProductCard key={r.id} product={r} />
            ))}
          </div>
        </section>
      )}
    </div>
  );
}
