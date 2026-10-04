import { Link } from 'react-router';
import { useCart } from '../cart/context';
import { ProductArt } from '../components/ProductArt';
import { EmptyState } from '../components/States';
import { FREE_SHIPPING_THRESHOLD, formatPrice, shippingFor } from '../lib/format';

export function CartPage() {
  const { lines, subtotal, setQuantity, remove } = useCart();
  const shipping = shippingFor(subtotal);

  if (lines.length === 0) {
    return (
      <EmptyState title="Your cart is empty">
        <Link className="btn btn--primary" to="/">
          Continue shopping
        </Link>
      </EmptyState>
    );
  }

  return (
    <div className="cart-page">
      <h1>Your cart</h1>
      <div className="two-col">
        <ul className="cart-lines">
          {lines.map((line) => (
            <li key={line.productId} className="cart-line">
              <ProductArt product={{ id: line.productId, name: line.name, category: line.category, imageUrl: null }} size="sm" />
              <div className="cart-line__info">
                <Link to={`/products/${line.productId}`}>{line.name}</Link>
                <span className="muted">{formatPrice(line.price)} each</span>
              </div>
              <label className="sr-only" htmlFor={`qty-${line.productId}`}>
                Quantity for {line.name}
              </label>
              <input
                id={`qty-${line.productId}`}
                type="number"
                min={1}
                max={Math.min(line.stock, 20)}
                value={line.quantity}
                onChange={(e) => setQuantity(line.productId, Number(e.target.value))}
                className="qty-input"
              />
              <strong className="cart-line__total">{formatPrice(line.price * line.quantity)}</strong>
              <button type="button" className="link-button" onClick={() => remove(line.productId)} aria-label={`Remove ${line.name}`}>
                Remove
              </button>
            </li>
          ))}
        </ul>

        <aside className="summary">
          <h2>Summary</h2>
          <dl>
            <dt>Subtotal</dt>
            <dd>{formatPrice(subtotal)}</dd>
            <dt>Shipping</dt>
            <dd>{shipping === 0 ? 'Free' : formatPrice(shipping)}</dd>
            <dt className="summary__total">Total</dt>
            <dd className="summary__total">{formatPrice(subtotal + shipping)}</dd>
          </dl>
          {shipping > 0 && (
            <p className="muted">Add {formatPrice(FREE_SHIPPING_THRESHOLD - subtotal)} more for free shipping.</p>
          )}
          <Link to="/checkout" className="btn btn--primary btn--block">
            Checkout
          </Link>
          <p className="muted small">Prices and stock are confirmed when you place the order.</p>
        </aside>
      </div>
    </div>
  );
}
