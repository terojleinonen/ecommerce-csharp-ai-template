import { useState, type FormEvent } from 'react';
import { Link, Navigate, useNavigate } from 'react-router';
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { ApiError } from '../api/client';
import { api } from '../api/endpoints';
import type { ShippingAddress } from '../api/types';
import { useAuth } from '../auth/context';
import { useCart } from '../cart/context';
import { formatPrice, shippingFor } from '../lib/format';

const FIELDS: { name: keyof ShippingAddress; label: string; autoComplete: string; required?: boolean }[] = [
  { name: 'fullName', label: 'Full name', autoComplete: 'name', required: true },
  { name: 'line1', label: 'Address', autoComplete: 'address-line1', required: true },
  { name: 'line2', label: 'Apartment, suite (optional)', autoComplete: 'address-line2' },
  { name: 'postalCode', label: 'Postal code', autoComplete: 'postal-code', required: true },
  { name: 'city', label: 'City', autoComplete: 'address-level2', required: true },
  { name: 'country', label: 'Country code (e.g. FI)', autoComplete: 'country', required: true },
];

export function CheckoutPage() {
  const { user } = useAuth();
  const cart = useCart();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const [address, setAddress] = useState<ShippingAddress>({
    fullName: user?.displayName ?? '',
    line1: '',
    line2: '',
    postalCode: '',
    city: '',
    country: 'FI',
  });

  const placeOrder = useMutation({
    mutationFn: api.placeOrder,
    onSuccess: (order) => {
      cart.clear();
      queryClient.invalidateQueries({ queryKey: ['orders'] });
      queryClient.invalidateQueries({ queryKey: ['products'] });
      navigate(`/orders/${order.id}?placed=1`, { replace: true });
    },
  });

  if (cart.lines.length === 0 && !placeOrder.isSuccess) return <Navigate to="/cart" replace />;

  const fieldErrors = placeOrder.error instanceof ApiError ? placeOrder.error.fieldErrors : {};
  const shipping = shippingFor(cart.subtotal);

  const onSubmit = (e: FormEvent) => {
    e.preventDefault();
    placeOrder.mutate({
      items: cart.lines.map((l) => ({ productId: l.productId, quantity: l.quantity })),
      shippingAddress: { ...address, line2: address.line2 || null, country: address.country.toUpperCase() },
    });
  };

  return (
    <div className="checkout-page">
      <h1>Checkout</h1>
      <div className="two-col">
        <form className="form card" onSubmit={onSubmit} noValidate>
          <h2>Shipping address</h2>
          {FIELDS.map((f) => {
            const error = fieldErrors[`shippingAddress.${f.name}`] ?? fieldErrors[f.name];
            return (
              <label key={f.name} className="field">
                <span>{f.label}</span>
                <input
                  name={f.name}
                  autoComplete={f.autoComplete}
                  required={f.required}
                  value={address[f.name] ?? ''}
                  onChange={(e) => setAddress((a) => ({ ...a, [f.name]: e.target.value }))}
                  aria-invalid={Boolean(error)}
                  maxLength={f.name === 'country' ? 2 : 200}
                />
                {error && <small className="field__error">{error}</small>}
              </label>
            );
          })}

          {placeOrder.isError && (
            <p className="alert" role="alert">
              {placeOrder.error.message}
            </p>
          )}

          <button type="submit" className="btn btn--primary btn--block btn--lg" disabled={placeOrder.isPending}>
            {placeOrder.isPending ? 'Placing order…' : `Place order · ${formatPrice(cart.subtotal + shipping)}`}
          </button>
          <p className="muted small">Demo store: no payment is taken.</p>
        </form>

        <aside className="summary">
          <h2>Order summary</h2>
          <ul className="summary__lines">
            {cart.lines.map((l) => (
              <li key={l.productId}>
                <span>
                  {l.quantity} × {l.name}
                </span>
                <span>{formatPrice(l.price * l.quantity)}</span>
              </li>
            ))}
          </ul>
          <dl>
            <dt>Shipping</dt>
            <dd>{shipping === 0 ? 'Free' : formatPrice(shipping)}</dd>
            <dt className="summary__total">Total</dt>
            <dd className="summary__total">{formatPrice(cart.subtotal + shipping)}</dd>
          </dl>
          <Link to="/cart">← Edit cart</Link>
        </aside>
      </div>
    </div>
  );
}
