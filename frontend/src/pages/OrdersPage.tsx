import { Link, useParams, useSearchParams } from 'react-router';
import { useQuery } from '@tanstack/react-query';
import { api } from '../api/endpoints';
import type { OrderStatus } from '../api/types';
import { EmptyState, ErrorState, Spinner } from '../components/States';
import { formatDate, formatPrice } from '../lib/format';

export function StatusBadge({ status }: { status: OrderStatus }) {
  return <span className={`badge badge--status-${status}`}>{status}</span>;
}

export function OrdersPage() {
  const orders = useQuery({ queryKey: ['orders'], queryFn: ({ signal }) => api.myOrders(signal) });

  if (orders.isPending) return <Spinner />;
  if (orders.isError) return <ErrorState error={orders.error} onRetry={() => orders.refetch()} />;
  if (orders.data.length === 0) {
    return (
      <EmptyState title="No orders yet">
        <Link className="btn btn--primary" to="/">
          Start shopping
        </Link>
      </EmptyState>
    );
  }

  return (
    <div>
      <h1>Your orders</h1>
      <ul className="order-list">
        {orders.data.map((o) => (
          <li key={o.id} className="card order-row">
            <Link to={`/orders/${o.id}`}>
              <strong>{o.orderNumber}</strong>
            </Link>
            <span className="muted">{formatDate(o.createdAt)}</span>
            <span>{o.items.reduce((n, i) => n + i.quantity, 0)} items</span>
            <StatusBadge status={o.status} />
            <strong>{formatPrice(o.total)}</strong>
          </li>
        ))}
      </ul>
    </div>
  );
}

export function OrderDetailPage() {
  const { id = '' } = useParams();
  const [params] = useSearchParams();
  const order = useQuery({ queryKey: ['orders', id], queryFn: ({ signal }) => api.myOrder(id, signal) });

  if (order.isPending) return <Spinner />;
  if (order.isError) return <ErrorState error={order.error} />;
  const o = order.data;

  return (
    <div className="order-detail">
      {params.get('placed') && (
        <div className="notice notice--success" role="status">
          <strong>Thank you!</strong> Your order has been placed.
        </div>
      )}
      <h1>
        Order {o.orderNumber} <StatusBadge status={o.status} />
      </h1>
      <p className="muted">Placed {formatDate(o.createdAt)}</p>

      <div className="two-col">
        <table className="table">
          <thead>
            <tr>
              <th>Product</th>
              <th>Qty</th>
              <th>Price</th>
              <th>Total</th>
            </tr>
          </thead>
          <tbody>
            {o.items.map((i) => (
              <tr key={i.productId}>
                <td>
                  <Link to={`/products/${i.productId}`}>{i.productName}</Link>
                </td>
                <td>{i.quantity}</td>
                <td>{formatPrice(i.unitPrice)}</td>
                <td>{formatPrice(i.lineTotal)}</td>
              </tr>
            ))}
          </tbody>
        </table>
        <aside className="summary">
          <h2>Shipping to</h2>
          <address>
            {o.shippingAddress.fullName}
            <br />
            {o.shippingAddress.line1}
            {o.shippingAddress.line2 && (
              <>
                <br />
                {o.shippingAddress.line2}
              </>
            )}
            <br />
            {o.shippingAddress.postalCode} {o.shippingAddress.city}, {o.shippingAddress.country}
          </address>
          <dl>
            <dt>Subtotal</dt>
            <dd>{formatPrice(o.subtotal)}</dd>
            <dt>Shipping</dt>
            <dd>{o.shippingCost === 0 ? 'Free' : formatPrice(o.shippingCost)}</dd>
            <dt className="summary__total">Total</dt>
            <dd className="summary__total">{formatPrice(o.total)}</dd>
          </dl>
        </aside>
      </div>
      <Link to="/orders">← All orders</Link>
    </div>
  );
}
